using EFT;
using EFT.Ballistics;
using HollywoodFX.Gore;
using HollywoodFX.Particles;
using Systems.Effects;
using UnityEngine;

namespace HollywoodFX;

internal class ImpactController
{
    private readonly BattleAmbience _battleAmbience;
    private readonly ImpactEffects _impactEffects;
    private readonly GoreController _goreController;

    public ImpactController(Effects eftEffects)
    {
        Plugin.Log.LogInfo("Loading Impacts Prefabs");

        var ambiencePrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Ambience.prefab");
        var ambiencePuffsPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Ambience Puffs.prefab");
        _battleAmbience = new BattleAmbience(eftEffects, ambiencePrefab, ambiencePuffsPrefab);

        var impactsMainPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Impacts.prefab");
        var impactsTracerPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Impacts Tracer.prefab");

        var impactEffectsMap = EffectBundle.LoadPrefab(eftEffects, impactsMainPrefab, true);
        
        _impactEffects = new ImpactEffects(eftEffects, impactEffectsMap, impactsTracerPrefab);

        GameObject bloodMainPrefab = null;
        GameObject bloodSquirtsPrefab = null;
        GameObject bloodBleedoutPrefab = null;
        GameObject bloodFinishersPrefab = null;

        if (Plugin.BloodRenderOwnership.AllowTransientImpactPuffsAndSprays)
            bloodMainPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Blood Main.prefab");

        if (Plugin.BloodRenderOwnership.AllowImpactSquirts)
            bloodSquirtsPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Blood Squirts.prefab");

        if (Plugin.BloodRenderOwnership.AllowDeathBloodEffects)
        {
            bloodBleedoutPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Blood Bleedout.prefab");
            bloodFinishersPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Blood Finishers.prefab");
        }

        _goreController = new GoreController(eftEffects, impactEffectsMap, bloodMainPrefab, bloodSquirtsPrefab, bloodBleedoutPrefab, bloodFinishersPrefab);
    }

    public void Emit(ImpactKinetics kinetics)
    {
        var hitColliderRoot = kinetics.Bullet.HitColliderRoot;

        // Don't render effects on the local player in first person view
        var localPlayer = ImpactStatic.LocalPlayer;
        if (hitColliderRoot != null && hitColliderRoot == localPlayer.Transform.Original && localPlayer.PointOfView == EPointOfView.FirstPerson)
            return;

        var materialLooksLikeBody = kinetics.Material is MaterialType.Body or MaterialType.BodyArmor or MaterialType.Helmet or MaterialType.HelmetRicochet;
        var isBodyShot = BodyTargetClassifier.ShouldEmitGore(materialLooksLikeBody, hitColliderRoot, out var bodyOwner);

        if (materialLooksLikeBody && !isBodyShot && Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"rejected body-material hit without player/corpse owner material={kinetics.Material} " +
                $"owner={bodyOwner} collider={kinetics.Bullet.Info?.HitCollider?.name ?? "<null>"} " +
                $"root={hitColliderRoot?.name ?? "<null>"} penetrated={kinetics.Bullet.Penetrated} " +
                $"position={kinetics.Position}." );
        }

        if (kinetics.IsHitPointVisible)
        {
            _impactEffects.Emit(kinetics);
            
            if (isBodyShot)
                _goreController.Apply(kinetics);
        }

        if (kinetics.IsHitPointVisible || kinetics.DistanceToImpact < Plugin.AmbientSimulationRange.Value)
            _battleAmbience.Emit(kinetics, isBodyShot ? 0.3f : 0.6f);
    }
}
