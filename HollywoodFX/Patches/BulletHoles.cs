using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Comfort.Common;
using DeferredDecals;
using EFT.Ballistics;
using HarmonyLib;
using HollywoodFX.Decal;
using SPT.Reflection.Patching;
using Systems.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollywoodFX.Patches;

/*
 * Sizes bullet holes by caliber, and widens the hole where the round leaves the surface.
 *
 * Three stages, because the game separates queueing an impact from drawing its decal:
 *
 *   AddEffectEmit  - runs while the shot is still current, so the caliber is measured and parked here
 *   Effect.Emit    - the direct caller of DrawDecal, so the matching size is armed around it
 *   AddCubeToMesh  - the size is applied to the shared SingleDecal and restored immediately after
 *   GetDynamicDecal- same, for decals landing on moving geometry
 */

public class EffectQueuedBulletHolePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Effects).GetMethod(nameof(Effects.AddEffectEmit));
    }

    [PatchPrefix]
    public static void Prefix(Vector3 position, ref bool withDecal, bool isKnife, bool isGrenade)
    {
        if (withDecal && !isKnife && !isGrenade && SurfaceImpactMarks.TryConsumeStandardDecalReplacement(position))
        {
            withDecal = false;
            return;
        }

        if (!Plugin.BulletHoleScalingEnabled.Value || !withDecal || isKnife || isGrenade)
            return;

        var shot = ImpactStatic.Kinetics.Bullet.Info;

        if (shot == null)
            return;

        BulletHoles.Record(position, shot);
    }
}

public class EffectEmitBulletHolePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Effects.Effect).GetMethod(nameof(Effects.Effect.Emit));
    }

    [PatchPrefix]
    public static void Prefix(Vector3 position, bool withDecal, bool isGrenade)
    {
        BulletHoles.Armed = false;

        if (!Plugin.BulletHoleScalingEnabled.Value || !withDecal || isGrenade)
            return;

        if (!BulletHoles.TryTake(position, out var scale, out var kind))
        {
            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write($"bullet-hole draw unmatched position={position.ToString("F4")}");

            return;
        }

        BulletHoles.Current = scale;
        BulletHoles.CurrentKind = kind;
        BulletHoles.Armed = true;
    }

    [PatchPostfix]
    public static void Postfix()
    {
        BulletHoles.Armed = false;
    }
}

public class StaticDecalSizePatch : ModulePatch
{
    private static readonly HashSet<int> ReportedMaterials = [];

    private static Vector2 _original;
    private static int _originalRows;
    private static int _originalColumns;
    private static bool _borrowed;
    private static bool _atlasLocked;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(DeferredDecalRenderer).GetMethod(nameof(DeferredDecalRenderer.AddCubeToMesh));
    }

    [PatchPrefix]
    public static void Prefix(
        DeferredDecalRenderer __instance,
        DeferredDecalRenderer.ManagedMesh mesh,
        DeferredDecalRenderer.SingleDecal decal)
    {
        Singleton<DecalPainter>.Instance?.ObserveVanillaStaticWrite(__instance, mesh);
        _borrowed = false;
        _atlasLocked = false;

        if (!BulletHoles.Armed)
            return;

        // SingleDecal is shared per material type, so the size is borrowed and handed straight back
        _borrowed = true;
        _original = decal.DecalSize;
        decal.DecalSize = BulletHoles.ResolveDecalSize(_original);

        if (BulletHoles.ShouldLockAtlasTile && decal.IsTiled)
        {
            // TileUSize/TileVSize remain at their initialized atlas-cell dimensions. Restricting the random
            // row/column range to one therefore selects cell 0,0 without stretching the whole texture.
            _originalRows = decal.TileSheetRows;
            _originalColumns = decal.TileSheetColumns;
            decal.TileSheetRows = 1;
            decal.TileSheetColumns = 1;
            _atlasLocked = true;
        }

        if (Plugin.DebugLoggingEnabled && decal.DecalMaterial != null && ReportedMaterials.Add(decal.DecalMaterial.GetInstanceID()))
        {
            Plugin.Log.LogInfo(
                $"Bullet-hole decal range: material={decal.DecalMaterial.name}, " +
                $"base={_original.x:0.###}-{_original.y:0.###}, " +
                $"shotScale={BulletHoles.Current:0.###}, resolved={decal.DecalSize.x:0.###}, " +
                $"kind={BulletHoles.CurrentKind}, atlasLocked={_atlasLocked}, " +
                $"staticLimit={BulletHoles.GetStaticDecalSizeLimit():0.###}"
            );
        }
    }

    [PatchPostfix]
    public static void Postfix(DeferredDecalRenderer.SingleDecal decal)
    {
        if (!_borrowed)
            return;

        decal.DecalSize = _original;

        if (_atlasLocked)
        {
            decal.TileSheetRows = _originalRows;
            decal.TileSheetColumns = _originalColumns;
        }

        _borrowed = false;
        _atlasLocked = false;
    }

    [PatchTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var clamp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp), [typeof(float), typeof(float), typeof(float)]);
        var dynamicLimit = AccessTools.Method(typeof(BulletHoles), nameof(BulletHoles.GetStaticDecalSizeLimit));

        for (var i = 0; i < code.Count - 1; i++)
        {
            if (code[i].opcode != OpCodes.Ldc_R4 || code[i].operand is not float value ||
                !Mathf.Approximately(value, 0.4f) || !code[i + 1].Calls(clamp))
                continue;

            // Mutating the instruction preserves any labels and exception blocks attached to the original constant.
            code[i].opcode = OpCodes.Call;
            code[i].operand = dynamicLimit;

            return code;
        }

        throw new InvalidOperationException(
            "DeferredDecalRenderer.AddCubeToMesh no longer contains the expected 0.4f Mathf.Clamp ceiling."
        );
    }
}

public class DynamicDecalSizePatch : ModulePatch
{
    private static Vector2 _original;
    private static int _originalRows;
    private static int _originalColumns;
    private static bool _borrowed;
    private static bool _atlasLocked;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(DeferredDecalRenderer).GetMethod(nameof(DeferredDecalRenderer.GetDynamicDecal));
    }

    [PatchPrefix]
    public static void Prefix(
        DeferredDecalRenderer __instance,
        DeferredDecalRenderer.SingleDecal currentDecal,
        Material currentMaterial,
        Vector3 position,
        BallisticCollider hitCollider,
        int ____currentDynamicDecalIndex,
        List<DynamicDeferredDecalRenderer> ____dynamicDecals,
        out DynamicDecalWriteDiagnosticState __state)
    {
        __state = default;
        PrepareDynamicProjector(
            __instance,
            currentDecal,
            currentMaterial,
            position,
            hitCollider,
            ____currentDynamicDecalIndex,
            ____dynamicDecals,
            out __state
        );
        _borrowed = false;
        _atlasLocked = false;

        if (!BulletHoles.Armed)
            return;

        _borrowed = true;
        _original = currentDecal.DecalSize;
        currentDecal.DecalSize = BulletHoles.ResolveDecalSize(_original);

        if (BulletHoles.ShouldLockAtlasTile && currentDecal.IsTiled)
        {
            _originalRows = currentDecal.TileSheetRows;
            _originalColumns = currentDecal.TileSheetColumns;
            currentDecal.TileSheetRows = 1;
            currentDecal.TileSheetColumns = 1;
            _atlasLocked = true;
        }
    }

    private static void PrepareDynamicProjector(
        DeferredDecalRenderer renderer,
        DeferredDecalRenderer.SingleDecal currentDecal,
        Material currentMaterial,
        Vector3 position,
        BallisticCollider hitCollider,
        int currentDynamicDecalIndex,
        List<DynamicDeferredDecalRenderer> dynamicDecals,
        out DynamicDecalWriteDiagnosticState diagnosticState)
    {
        diagnosticState = default;

        if (dynamicDecals == null || dynamicDecals.Count == 0 ||
            currentDynamicDecalIndex < 0 || currentDynamicDecalIndex >= dynamicDecals.Count)
        {
            if (BulletHoles.Armed && Plugin.DebugLoggingEnabled)
            {
                RuntimeDebugTrace.Write(
                    $"vanilla dynamic projector skipped: invalid slot={currentDynamicDecalIndex} " +
                    $"pool={dynamicDecals?.Count ?? 0} kind={BulletHoles.CurrentKind} " +
                    $"position={position.ToString("F4")}"
                );
            }

            return;
        }

        var dynamicDecal = dynamicDecals[currentDynamicDecalIndex];

        if (dynamicDecal == null)
        {
            if (BulletHoles.Armed && Plugin.DebugLoggingEnabled)
            {
                RuntimeDebugTrace.Write(
                    $"vanilla dynamic projector skipped: null slot={currentDynamicDecalIndex} " +
                    $"kind={BulletHoles.CurrentKind} position={position.ToString("F4")}"
                );
            }

            return;
        }

        var previousSphere = dynamicDecal.CullingGroupSphereIndex;
        dynamicDecal.CullingGroupSphereIndex = currentDynamicDecalIndex;

        Singleton<DecalPainter>.Instance?.ObserveVanillaDynamicWrite(renderer, dynamicDecal);

        if (BulletHoles.Armed && Plugin.DebugLoggingEnabled)
        {
            var colliderName = hitCollider == null ? "null" : hitCollider.name;
            var materialName = currentMaterial == null ? "NULL" : currentMaterial.name;
            var staticMaterialName = currentDecal?.DecalMaterial == null
                ? "NULL"
                : currentDecal.DecalMaterial.name;
            var dynamicMaterialName = currentDecal?.DynamicDecalMaterial == null
                ? "NULL"
                : currentDecal.DynamicDecalMaterial.name;
            var isTiled = currentDecal != null && currentDecal.IsTiled ? "true" : "false";

            diagnosticState = new DynamicDecalWriteDiagnosticState
            {
                Active = true,
                Slot = currentDynamicDecalIndex,
                Kind = BulletHoles.CurrentKind.ToString(),
                ColliderName = colliderName,
                Position = position
            };

            RuntimeDebugTrace.Write(
                $"vanilla dynamic projector prepared kind={BulletHoles.CurrentKind} " +
                $"material={materialName} staticMaterial={staticMaterialName} " +
                $"dynamicField={dynamicMaterialName} isTiled={isTiled} " +
                $"slot={currentDynamicDecalIndex} previousSphere={previousSphere} " +
                $"assignedSphere={dynamicDecal.CullingGroupSphereIndex} " +
                $"collider={colliderName} position={position.ToString("F4")}"
            );
        }
    }

    [PatchPostfix]
    public static void Postfix(
        DeferredDecalRenderer __instance,
        DeferredDecalRenderer.SingleDecal currentDecal,
        DynamicDecalWriteDiagnosticState __state)
    {
        if (__state.Active)
        {
            DynamicDecalRenderDiagnostics.Arm(
                __instance,
                __state.Slot,
                __state.Kind,
                __state.ColliderName,
                __state.Position
            );
        }

        if (!_borrowed)
            return;

        currentDecal.DecalSize = _original;

        if (_atlasLocked)
        {
            currentDecal.TileSheetRows = _originalRows;
            currentDecal.TileSheetColumns = _originalColumns;
        }

        _borrowed = false;
        _atlasLocked = false;
    }
}

public struct DynamicDecalWriteDiagnosticState
{
    public bool Active;
    public int Slot;
    public string Kind;
    public string ColliderName;
    public Vector3 Position;
}

internal static class DynamicDecalRenderDiagnostics
{
    private const int TargetLifetimeFrames = 8;
    private const float CaptureCooldownSeconds = 1f;

    private static readonly object Sync = new();
    private static readonly List<Target> Targets = [];

    private static bool _failureReported;
    private static long _nextTraceId;
    private static float _nextCaptureTime;

    private sealed class Target
    {
        public long TraceId;
        public int RendererId;
        public int Slot;
        public int ArmedFrame;
        public string Kind;
        public string ColliderName;
        public Vector3 Position;
        public readonly HashSet<int> CameraCallbacks = [];
        public readonly HashSet<int> DrawCallbacks = [];
    }

    public static void Arm(
        DeferredDecalRenderer renderer,
        int slot,
        string kind,
        string colliderName,
        Vector3 position)
    {
        if (!Plugin.DebugLoggingEnabled || renderer == null)
            return;

        try
        {
            lock (Sync)
            {
                var rendererId = renderer.GetInstanceID();
                var frame = Time.frameCount;

                PruneExpired(frame);

                if (Targets.Count != 0 || Time.unscaledTime < _nextCaptureTime)
                    return;

                var target = new Target
                {
                    TraceId = ++_nextTraceId,
                    RendererId = rendererId,
                    Slot = slot,
                    ArmedFrame = frame,
                    Kind = kind,
                    ColliderName = colliderName,
                    Position = position
                };

                Targets.Add(target);
                _nextCaptureTime = Time.unscaledTime + CaptureCooldownSeconds;

                RuntimeDebugTrace.Write(
                    $"dynamic render diagnostic armed traceId={target.TraceId} rendererId={rendererId} " +
                    $"slot={slot} kind={target.Kind} collider={colliderName} " +
                    $"position={position.ToString("F4")} frame={frame}"
                );
            }
        }
        catch (Exception exception)
        {
            ReportFailureOnce("arm", exception);
        }
    }

    public static void ObserveCameraCallback(
        DeferredDecalRenderer renderer,
        Camera currentCamera,
        CommandBuffer sharedDynamicBuffer,
        Dictionary<Camera, DeferredDecalRenderer.CameraData> cameras,
        bool updateBuffersInNewCamera)
    {
        if (!Plugin.DebugLoggingEnabled || renderer == null || currentCamera == null)
            return;

        try
        {
            string message;

            lock (Sync)
            {
                var rendererId = renderer.GetInstanceID();
                var cameraId = currentCamera.GetInstanceID();
                var frame = Time.frameCount;
                var targetLabels = new List<string>();

                PruneExpired(frame);

                foreach (var target in Targets)
                {
                    if (target.RendererId != rendererId || !target.CameraCallbacks.Add(cameraId))
                        continue;

                    targetLabels.Add($"{target.TraceId}:{target.Slot}:{target.Kind}");
                }

                if (targetLabels.Count == 0)
                    return;

                DeferredDecalRenderer.CameraData cameraData = null;
                var tracked = cameras != null && cameras.TryGetValue(currentCamera, out cameraData);
                var cameraBuffer = tracked ? cameraData.DynamicDecalsBuffer : null;
                var bufferMatchesCamera = tracked && ReferenceEquals(sharedDynamicBuffer, cameraBuffer);
                var dirty = tracked && cameraData.IsDynamicBufferDirty;

                message =
                    $"dynamic camera buffer camera={currentCamera.name} cameraId={cameraId} " +
                    $"cameraType={currentCamera.cameraType} active={currentCamera.isActiveAndEnabled} " +
                    $"tracked={tracked} dirty={dirty} updateNew={updateBuffersInNewCamera} " +
                    $"bufferMatchesCamera={bufferMatchesCamera} sharedBuffer={DescribeBuffer(sharedDynamicBuffer)} " +
                    $"cameraBuffer={DescribeBuffer(cameraBuffer)} targets={string.Join(",", targetLabels)} frame={frame}";
            }

            RuntimeDebugTrace.Write(message);
        }
        catch (Exception exception)
        {
            ReportFailureOnce("camera", exception);
        }
    }

    public static void ObserveDraw(
        DeferredDecalRenderer renderer,
        Camera currentCamera,
        CommandBuffer buffer,
        List<DynamicDeferredDecalRenderer> dynamicDecals,
        Dictionary<Camera, DeferredDecalRenderer.CameraData> cameras,
        int cullDistance)
    {
        if (!Plugin.DebugLoggingEnabled || renderer == null || currentCamera == null)
            return;

        try
        {
            var messages = new List<string>();

            lock (Sync)
            {
                var rendererId = renderer.GetInstanceID();
                var cameraId = currentCamera.GetInstanceID();
                var frame = Time.frameCount;
                var targets = new List<Target>();

                PruneExpired(frame);

                foreach (var target in Targets)
                {
                    if (target.RendererId == rendererId && target.DrawCallbacks.Add(cameraId))
                        targets.Add(target);
                }

                if (targets.Count == 0)
                    return;

                DeferredDecalRenderer.CameraData cameraData = null;
                var tracked = cameras != null && cameras.TryGetValue(currentCamera, out cameraData);
                var cameraBuffer = tracked ? cameraData.DynamicDecalsBuffer : null;
                var cullGroup = tracked ? cameraData.CullGroup : null;
                var bufferMatchesCamera = tracked && ReferenceEquals(buffer, cameraBuffer);
                var poolCount = dynamicDecals?.Count ?? 0;
                var enabledCount = 0;
                var passedCount = 0;
                var distanceReadFailures = 0;

                if (dynamicDecals != null)
                {
                    foreach (var dynamicDecal in dynamicDecals)
                    {
                        if (dynamicDecal == null || !dynamicDecal.enabled)
                            continue;

                        enabledCount++;

                        if (!TryReadDistanceBand(
                                cullGroup,
                                dynamicDecal.CullingGroupSphereIndex,
                                out var distanceBand))
                        {
                            distanceReadFailures++;
                            continue;
                        }

                        if (distanceBand < cullDistance)
                            passedCount++;
                    }
                }

                foreach (var target in targets)
                {
                    var targetPresent = dynamicDecals != null && target.Slot >= 0 && target.Slot < dynamicDecals.Count;
                    var targetDecal = targetPresent ? dynamicDecals[target.Slot] : null;
                    var targetAlive = targetDecal != null;
                    var targetEnabled = targetAlive && targetDecal.enabled;
                    var targetSphere = targetAlive ? targetDecal.CullingGroupSphereIndex : -1;
                    var targetDistanceBand = -1;
                    var targetDistanceKnown = targetAlive && TryReadDistanceBand(
                        cullGroup,
                        targetSphere,
                        out targetDistanceBand
                    );
                    var targetPassed = targetEnabled && targetDistanceKnown && targetDistanceBand < cullDistance;
                    var targetMaterial = targetAlive && targetDecal.DecalMaterial != null
                        ? targetDecal.DecalMaterial.name
                        : "NULL";

                    messages.Add(
                        $"dynamic draw submission traceId={target.TraceId} rendererId={rendererId} " +
                        $"camera={currentCamera.name} cameraId={cameraId} " +
                        $"bufferMatchesCamera={bufferMatchesCamera} buffer={DescribeBuffer(buffer)} " +
                        $"cameraBuffer={DescribeBuffer(cameraBuffer)} pool={poolCount} enabled={enabledCount} " +
                        $"passed={passedCount} distanceReadFailures={distanceReadFailures} cullLimit={cullDistance} " +
                        $"targetSlot={target.Slot} targetPresent={targetPresent} targetAlive={targetAlive} " +
                        $"targetEnabled={targetEnabled} targetSphere={targetSphere} " +
                        $"targetDistanceKnown={targetDistanceKnown} targetDistanceBand={targetDistanceBand} " +
                        $"targetPassed={targetPassed} targetMaterial={targetMaterial} kind={target.Kind} " +
                        $"collider={target.ColliderName} position={target.Position.ToString("F4")} " +
                        $"armedFrame={target.ArmedFrame} drawFrame={frame}"
                    );
                }
            }

            foreach (var message in messages)
                RuntimeDebugTrace.Write(message);
        }
        catch (Exception exception)
        {
            ReportFailureOnce("draw", exception);
        }
    }

    private static bool TryReadDistanceBand(CullingGroup cullGroup, int sphereIndex, out int distanceBand)
    {
        distanceBand = -1;

        if (cullGroup == null || sphereIndex < 0)
            return false;

        try
        {
            distanceBand = cullGroup.GetDistance(sphereIndex);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeBuffer(CommandBuffer buffer)
    {
        return buffer == null
            ? "NULL"
            : $"{buffer.name}#{RuntimeHelpers.GetHashCode(buffer)}";
    }

    private static void PruneExpired(int currentFrame)
    {
        for (var i = Targets.Count - 1; i >= 0; i--)
        {
            var age = unchecked(currentFrame - Targets[i].ArmedFrame);

            if (age >= 0 && age <= TargetLifetimeFrames)
                continue;

            var target = Targets[i];
            RuntimeDebugTrace.Write(
                $"dynamic render diagnostic expired traceId={target.TraceId} rendererId={target.RendererId} " +
                $"slot={target.Slot} cameras={target.CameraCallbacks.Count} draws={target.DrawCallbacks.Count} " +
                $"armedFrame={target.ArmedFrame} expiredFrame={currentFrame}"
            );
            Targets.RemoveAt(i);
        }
    }

    private static void ReportFailureOnce(string stage, Exception exception)
    {
        if (_failureReported)
            return;

        _failureReported = true;
        RuntimeDebugTrace.Write(
            $"dynamic render diagnostic failed stage={stage} " +
            $"exception={exception.GetType().Name} message={exception.Message}"
        );
    }
}

public class DynamicDecalCameraBufferDiagnosticPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(
            typeof(DeferredDecalRenderer),
            nameof(DeferredDecalRenderer.OnPreCameraRender),
            [typeof(Camera)]
        );
    }

    [PatchPrefix]
    public static void Prefix(
        DeferredDecalRenderer __instance,
        Camera currentCamera,
        CommandBuffer ____dynamicBuf,
        Dictionary<Camera, DeferredDecalRenderer.CameraData> ____cameras,
        bool ____updateBuffersInNewCamera)
    {
        DynamicDecalRenderDiagnostics.ObserveCameraCallback(
            __instance,
            currentCamera,
            ____dynamicBuf,
            ____cameras,
            ____updateBuffersInNewCamera
        );
    }
}

public class DynamicDecalDrawSubmissionDiagnosticPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(
            typeof(DeferredDecalRenderer),
            nameof(DeferredDecalRenderer.DrawDynamicDecals),
            [typeof(Camera), typeof(CommandBuffer)]
        );
    }

    [PatchPrefix]
    public static void Prefix(
        DeferredDecalRenderer __instance,
        Camera currentCamera,
        CommandBuffer buffer,
        List<DynamicDeferredDecalRenderer> ____dynamicDecals,
        Dictionary<Camera, DeferredDecalRenderer.CameraData> ____cameras)
    {
        DynamicDecalRenderDiagnostics.ObserveDraw(
            __instance,
            currentCamera,
            buffer,
            ____dynamicDecals,
            ____cameras,
            __instance._cullDistance
        );
    }
}
