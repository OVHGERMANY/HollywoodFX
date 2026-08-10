using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Comfort.Common;
using DeferredDecals;
using HarmonyLib;
using HollywoodFX.Decal;
using SPT.Reflection.Patching;
using Systems.Effects;
using UnityEngine;

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
        DeferredDecalRenderer.ManagedMesh mesh,
        DeferredDecalRenderer.SingleDecal decal)
    {
        Singleton<DecalPainter>.Instance?.ObserveVanillaStaticWrite(mesh);
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
    public static void Prefix(DeferredDecalRenderer.SingleDecal currentDecal)
    {
        Singleton<DecalPainter>.Instance?.ObserveVanillaDynamicWrite();
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

    [PatchPostfix]
    public static void Postfix(DeferredDecalRenderer.SingleDecal currentDecal)
    {
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
