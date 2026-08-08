using System.Reflection;
using DeferredDecals;
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
    public static void Prefix(Vector3 position, bool withDecal, bool isKnife, bool isGrenade)
    {
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

        if (!BulletHoles.TryTake(position, out var scale))
            return;

        BulletHoles.Current = scale;
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
    private static Vector2 _original;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(DeferredDecalRenderer).GetMethod(nameof(DeferredDecalRenderer.AddCubeToMesh));
    }

    [PatchPrefix]
    public static void Prefix(DeferredDecalRenderer.SingleDecal decal)
    {
        if (!BulletHoles.Armed)
            return;

        // SingleDecal is shared per material type, so the size is borrowed and handed straight back
        _original = decal.DecalSize;
        decal.DecalSize = Vector2.Scale(decal.DecalSize, BulletHoles.Current);
    }

    [PatchPostfix]
    public static void Postfix(DeferredDecalRenderer.SingleDecal decal)
    {
        if (!BulletHoles.Armed)
            return;

        decal.DecalSize = _original;
    }
}

public class DynamicDecalSizePatch : ModulePatch
{
    private static Vector2 _original;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(DeferredDecalRenderer).GetMethod(nameof(DeferredDecalRenderer.GetDynamicDecal));
    }

    [PatchPrefix]
    public static void Prefix(DeferredDecalRenderer.SingleDecal currentDecal)
    {
        if (!BulletHoles.Armed)
            return;

        _original = currentDecal.DecalSize;
        currentDecal.DecalSize = Vector2.Scale(currentDecal.DecalSize, BulletHoles.Current);
    }

    [PatchPostfix]
    public static void Postfix(DeferredDecalRenderer.SingleDecal currentDecal)
    {
        if (!BulletHoles.Armed)
            return;

        currentDecal.DecalSize = _original;
    }
}
