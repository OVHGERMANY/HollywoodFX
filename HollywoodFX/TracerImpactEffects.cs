using System;
using System.Collections.Generic;
using Comfort.Common;
using DeferredDecals;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using HollywoodFX.Decal;
using HollywoodFX.Particles;
using JsonType;
using Systems.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX;

internal class TracerImpact(EffectSystem[] systems, float chance, float residueSuppressionChance, bool decal = false)
{
    public readonly EffectSystem[] Systems = systems;
    public readonly float Chance = chance;
    public readonly float ResidueSuppressionChance = residueSuppressionChance;
    public readonly bool Decal = decal;
}

internal class TracerImpactEffects
{
    private readonly TracerImpact[] _impacts;

    private readonly EffectBundle _tracerGreen;
    private readonly EffectBundle _tracerRed;
    private readonly EffectBundle _tracerYellow;
    private readonly EffectBundle _tracerWhite;

    private readonly LightPool _lightPool;

    public TracerImpactEffects(Effects eftEffects, Dictionary<string, EffectBundle> tracerEffects)
    {
        Plugin.Log.LogInfo("Defining tracer-only impact effect bundles");
        var sparksFlammable = tracerEffects["Sparks_Flammable"];
        var debrisFlammable = tracerEffects["Debris_Flammable"];
        var flame = tracerEffects["Flame"];

        _tracerGreen = tracerEffects["Tracer_Green"];
        _tracerRed = tracerEffects["Tracer_Red"];
        _tracerYellow = tracerEffects["Tracer_Yellow"];
        _tracerWhite = tracerEffects["Tracer_White"];

        var noCombustion = Array.Empty<EffectSystem>();
        var combustible = new[]
        {
            new EffectSystem(
                directional:
                [
                    new DirectionalEffect(sparksFlammable, chance: 0.2f, isChanceScaledByKinetics: true),
                    new DirectionalEffect(debrisFlammable, chance: 0.25f, isChanceScaledByKinetics: true),
                    new DirectionalEffect(flame, worldDir: WorldDir.Vertical | WorldDir.Up, chance: 0.25f,
                        isChanceScaledByKinetics: true)
                ])
        };

        _impacts = new TracerImpact[Enum.GetNames(typeof(MaterialType)).Length];

        _impacts[(int)MaterialType.Asphalt] = new TracerImpact(noCombustion, 0.45f, 0.6f, decal: true);
        _impacts[(int)MaterialType.Cardboard] = new TracerImpact(combustible, 0.6f, 0.1f, decal: true);
        _impacts[(int)MaterialType.Chainfence] = new TracerImpact(noCombustion, 0.35f, 0.35f);
        _impacts[(int)MaterialType.Concrete] = new TracerImpact(noCombustion, 0.6f, 0.75f);
        _impacts[(int)MaterialType.Fabric] = new TracerImpact(combustible, 0.5f, 0.1f, decal: true);
        _impacts[(int)MaterialType.GarbageMetal] = new TracerImpact(noCombustion, 0.5f, 0.7f);
        _impacts[(int)MaterialType.GarbagePaper] = new TracerImpact(combustible, 0.6f, 0.1f, decal: true);
        _impacts[(int)MaterialType.GenericSoft] = new TracerImpact(combustible, 0.4f, 0.1f, decal: true);
        _impacts[(int)MaterialType.Glass] = new TracerImpact(noCombustion, 0.35f, 0.35f);
        _impacts[(int)MaterialType.GlassShattered] = new TracerImpact(noCombustion, 0.35f, 0.35f);
        _impacts[(int)MaterialType.Grate] = new TracerImpact(noCombustion, 0.35f, 0.6f);
        _impacts[(int)MaterialType.GrassHigh] = new TracerImpact(combustible, 0.3f, 0.2f, decal: true);
        _impacts[(int)MaterialType.GrassLow] = new TracerImpact(combustible, 0.3f, 0.3f, decal: true);
        _impacts[(int)MaterialType.Gravel] = new TracerImpact(noCombustion, 0.5f, 0.4f);
        _impacts[(int)MaterialType.MetalThin] = new TracerImpact(noCombustion, 0.6f, 0.6f);
        _impacts[(int)MaterialType.MetalThick] = new TracerImpact(noCombustion, 0.6f, 0.8f);
        _impacts[(int)MaterialType.Pebbles] = new TracerImpact(noCombustion, 0.35f, 0.4f);
        _impacts[(int)MaterialType.Plastic] = new TracerImpact(combustible, 0.5f, 0.1f, decal: true);
        _impacts[(int)MaterialType.Stone] = new TracerImpact(noCombustion, 0.45f, 0.5f);
        _impacts[(int)MaterialType.Tile] = new TracerImpact(noCombustion, 0.5f, 0.5f);
        _impacts[(int)MaterialType.WoodThick] = new TracerImpact(combustible, 0.6f, 0.1f, decal: true);
        _impacts[(int)MaterialType.WoodThin] = new TracerImpact(combustible, 0.45f, 0.1f, decal: true);
        _impacts[(int)MaterialType.Tyre] = new TracerImpact(combustible, 0.5f, 0.1f, decal: true);
        _impacts[(int)MaterialType.Rubber] = new TracerImpact(combustible, 0.5f, 0.1f, decal: true);
        _impacts[(int)MaterialType.GenericHard] = new TracerImpact(noCombustion, 0.35f, 0.5f);
        _impacts[(int)MaterialType.MetalNoDecal] = new TracerImpact(noCombustion, 0.45f, 0.6f);

        _lightPool = Traverse.Create(eftEffects).Field("_lightPool").GetValue<LightPool>();
    }

    public void Emit(ImpactKinetics kinetics, Ammo ammo)
    {
        var impactDef = _impacts[(int)kinetics.Material];
        if (impactDef == null || Random.value >= impactDef.Chance * kinetics.Bullet.ChanceScale)
            return;

        if (impactDef.Decal)
        {
            Singleton<DecalPainter>.Instance.DrawDecal(
                Decals.TracerScorchMark, kinetics.Position, kinetics.Normal, kinetics.Bullet.Info.HittedBallisticCollider);
        }

        for (var i = 0; i < impactDef.Systems.Length; i++)
            impactDef.Systems[i].Emit(kinetics, Plugin.EffectSize.Value);

        var lightColor = Color.white;
        var tracer = _tracerWhite;
        switch (ammo.TracerColor)
        {
            case TaxonomyColor.green or TaxonomyColor.tracerGreen:
                tracer = _tracerGreen;
                lightColor = new Color(0.9132687f, 1f, 0.7955974f);
                break;
            case TaxonomyColor.red or TaxonomyColor.tracerRed:
                tracer = _tracerRed;
                lightColor = new Color(1f, 0.8307356f, 0.7960784f);
                break;
            case TaxonomyColor.yellow or TaxonomyColor.tracerYellow:
                tracer = _tracerYellow;
                lightColor = new Color(1f, 0.9540824f, 0.7960784f);
                break;
        }

        if (Random.value >= impactDef.ResidueSuppressionChance * kinetics.Bullet.ChanceScale)
            tracer.EmitDirect(kinetics.Position, kinetics.Normal, kinetics.Bullet.SizeScale * Plugin.EffectSize.Value);

        if (kinetics.DistanceToImpact <= 50f && !HasEnhancedNativeImpactLight(kinetics.Material))
            _lightPool.Add(kinetics.Position, lightColor, 2.5f);
    }

    private static bool HasEnhancedNativeImpactLight(MaterialType material)
    {
        return material is MaterialType.Chainfence or MaterialType.GarbageMetal or MaterialType.Grate or
            MaterialType.MetalThin or MaterialType.MetalThick or MaterialType.MetalNoDecal;
    }
}
