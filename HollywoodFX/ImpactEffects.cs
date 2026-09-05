using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT.Ballistics;
using HollywoodFX.Impact.Sparks;
using HollywoodFX.Particles;
using HollywoodFX.Render;
using Systems.Effects;
using UnityEngine;
using EFT.InventoryLogic;

namespace System.Runtime.CompilerServices
{
    // ReSharper disable once UnusedType.Global
    internal static class IsExternalInit;
}

namespace HollywoodFX
{
    /// <summary>
    /// Verily, this is a spaghetti monster class. Refactor at some point once sanity levels have been restored.
    /// </summary>
    internal class ImpactEffects
    {
        private readonly List<EffectSystem>[] _mainImpacts;
        private readonly TracerImpactEffects _tracerImpacts;
        private readonly BallisticSparkEffects _ballisticSparks;

        public ImpactEffects(Effects eftEffects, Dictionary<string, EffectBundle> mainEffects, GameObject tracerPrefab)
        {
            var tracerEffects = EffectBundle.LoadPrefab(eftEffects, tracerPrefab, false);

            _mainImpacts = DefineMainEffects(mainEffects);
            _ballisticSparks = new BallisticSparkEffects(mainEffects);
            _tracerImpacts = new TracerImpactEffects(eftEffects, tracerEffects);
        }

        public void Emit(ImpactKinetics kinetics)
        {
            var currentSystems = _mainImpacts[(int)kinetics.Material];

            if (currentSystems != null)
            {
                for (var i = 0; i < currentSystems.Count; i++)
                {
                    var impactSystem = currentSystems[i];
                    impactSystem.Emit(kinetics, Plugin.EffectSize.Value);
                }

                var isLocalShot = kinetics.Bullet.Info?.Player is { iPlayer.IsYourPlayer: true };

                if (Plugin.SuppressionEnabled.Value && !isLocalShot && Singleton<PostProcessing>.Instance != null)
                {
                    var duration = Plugin.SuppressionDuration.Value;
                    var distanceNorm = 3f * kinetics.Bullet.SizeScale * Plugin.SuppressionRange.Value;
                    Singleton<PostProcessing>.Instance.Concussion.Apply(kinetics.DistanceToImpact, duration, distanceNorm, 2f * duration);
                }
            }

            var isTracer = kinetics.Bullet.Info?.Ammo is Ammo { Tracer: true };
            _ballisticSparks.Emit(kinetics, isTracer);

            if (Plugin.TracerImpactsEnabled.Value && isTracer && kinetics.Bullet.Info.Ammo is Ammo ammo)
                _tracerImpacts.Emit(kinetics, ammo);
        }

        public void Dispose()
        {
            _ballisticSparks.Dispose();
        }

        private static List<EffectSystem>[] DefineMainEffects(Dictionary<string, EffectBundle> effectMap)
        {
            Plugin.Log.LogInfo("Constructing impact systems");

            // Define major building blocks for systems
            Plugin.Log.LogInfo("Building frontal puffs");
            var puffFront = effectMap["Puff_Front"];
            var puffFrontDusty = effectMap["Puff_Front_Dusty"];
            var puffFrontRock = EffectBundle.Merge(puffFront, puffFrontDusty);

            Plugin.Log.LogInfo("Building generic puffs");
            var puffGeneric = effectMap["Puff"];

            Plugin.Log.LogInfo("Building horizontal puffs");
            var puffGenericHorRight = effectMap["Puff_Dusty_Hor_Right"];
            var puffGenericHorLeft = effectMap["Puff_Dusty_Hor_Left"];

            Plugin.Log.LogInfo("Building dirt debris");
            var debrisDirtVert = effectMap["Debris_Dirt_Vert"];

            Plugin.Log.LogInfo("Building mud debris");
            var debrisMudVert = EffectBundle.Merge(debrisDirtVert, effectMap["Debris_Mud_Vert"]);

            Plugin.Log.LogInfo("Building rock debris");
            var debrisRock = effectMap["Debris_Rock"];

            Plugin.Log.LogInfo("Building generic debris");
            var debrisGeneric = effectMap["Debris_Generic"];

            Plugin.Log.LogInfo("Building dust spray");
            var sprayDust = effectMap["Spray_Dust"];

            Plugin.Log.LogInfo("Building misc stuff");
            var bulletHoleSmoke = effectMap["Impact_Smoke"];

            var fallingDust = effectMap["Falling_Dust"];

            Plugin.Log.LogInfo("Defining material specific impacts");
            var softRockImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                // Various debris and splashes
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFrontRock),
                        new DirectionalEffect(sprayDust, chance: 1f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(debrisGeneric, chance: 0.5f, isChanceScaledByKinetics: true, pacing: 0.25f),
                        new DirectionalEffect(debrisRock, chance: 0.5f, isChanceScaledByKinetics: true, pacing: 0.5f),
                        new DirectionalEffect(debrisDirtVert, worldDir: WorldDir.Vertical | WorldDir.Up),
                        new DirectionalEffect(fallingDust, worldDir: WorldDir.Vertical | WorldDir.Down, chance: 0.2f, isChanceScaledByKinetics: true),
                        new DirectionalEffect(bulletHoleSmoke, chance: 0.05f, isChanceScaledByKinetics: true),
                    ]
                )
            };

            var hardRockImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.75f,
                    useOffsetNormals: true
                ),
                // Various debris and splashes
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFrontRock),
                        new DirectionalEffect(sprayDust, chance: 0.8f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(debrisGeneric, chance: 0.5f, isChanceScaledByKinetics: true, pacing: 0.25f),
                        new DirectionalEffect(debrisRock, chance: 0.35f, isChanceScaledByKinetics: true, pacing: 0.5f),
                        new DirectionalEffect(debrisDirtVert, worldDir: WorldDir.Vertical | WorldDir.Up),
                        new DirectionalEffect(fallingDust, worldDir: WorldDir.Vertical | WorldDir.Down, chance: 0.1f, isChanceScaledByKinetics: true),
                        new DirectionalEffect(bulletHoleSmoke, chance: 0.05f, isChanceScaledByKinetics: true),
                    ]
                )
            };

            var mudImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                // Various debris and splashes
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFrontDusty),
                        new DirectionalEffect(sprayDust, chance: 1f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(debrisGeneric, chance: 0.5f, isChanceScaledByKinetics: true, pacing: 0.25f),
                        new DirectionalEffect(debrisMudVert, worldDir: WorldDir.Vertical | WorldDir.Up)
                    ]
                )
            };

            var grassImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                // Various debris and splashes
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFrontDusty),
                        new DirectionalEffect(sprayDust, chance: 0.75f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(effectMap["Debris_Grass"], chance: 0.5f, isChanceScaledByKinetics: true, pacing: 0.25f),
                        new DirectionalEffect(debrisMudVert, worldDir: WorldDir.Vertical | WorldDir.Up),
                    ]
                )
            };

            var softGenericImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFront),
                        new DirectionalEffect(sprayDust, chance: 0.75f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(debrisGeneric, chance: 0.75f, isChanceScaledByKinetics: true, pacing: 0.25f),
                    ]
                )
            };

            var hardGenericImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFront),
                        new DirectionalEffect(sprayDust, chance: 0.6f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(bulletHoleSmoke, chance: 0.05f, isChanceScaledByKinetics: true)
                    ]
                )
            };

            var woodImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional:
                    [
                        new DirectionalEffect(puffGenericHorRight, camDir: CamDir.Angled | CamDir.Right, worldDir: WorldDir.Horizontal),
                        new DirectionalEffect(puffGenericHorLeft, camDir: CamDir.Angled | CamDir.Left, worldDir: WorldDir.Horizontal),
                    ],
                    generic: puffGeneric,
                    forceGeneric: 0.33f,
                    useOffsetNormals: true
                ),
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFrontDusty),
                        new DirectionalEffect(sprayDust, chance: 0.75f, isChanceScaledByKinetics: true, pacing: 0.05f),
                        new DirectionalEffect(effectMap["Debris_Wood"], chance: 1f, isChanceScaledByKinetics: true, pacing: 0.3f),
                        new DirectionalEffect(fallingDust, worldDir: WorldDir.Vertical | WorldDir.Down, chance: 0.15f,
                            isChanceScaledByKinetics: true),
                        new DirectionalEffect(bulletHoleSmoke, chance: 0.05f, isChanceScaledByKinetics: true)
                    ]
                )
            };
            var metalImpact = new List<EffectSystem>
            {
                // Main puff
                new(
                    directional: [],
                    generic: puffGeneric,
                    forceGeneric: 1.0f,
                    useOffsetNormals: true
                ),
                new(
                    directional:
                    [
                        new DirectionalEffect(puffFront),
                        new DirectionalEffect(debrisGeneric, chance: 0.3f, isChanceScaledByKinetics: true, pacing: 0.2f),
                        new DirectionalEffect(bulletHoleSmoke, chance: 0.05f, isChanceScaledByKinetics: true)
                    ]
                )
            };

            var impactSystems = new List<EffectSystem>[Enum.GetNames(typeof(MaterialType)).Length];

            // Assign impact systems to materials
            impactSystems[(int)MaterialType.Asphalt] = softRockImpact;
            impactSystems[(int)MaterialType.Cardboard] = softGenericImpact;
            impactSystems[(int)MaterialType.Chainfence] = metalImpact;
            impactSystems[(int)MaterialType.Concrete] = hardRockImpact;
            impactSystems[(int)MaterialType.None] = hardRockImpact;
            impactSystems[(int)MaterialType.Fabric] = softGenericImpact;
            impactSystems[(int)MaterialType.GarbageMetal] = metalImpact;
            impactSystems[(int)MaterialType.GarbagePaper] = softGenericImpact;
            impactSystems[(int)MaterialType.GenericSoft] = softGenericImpact;
            impactSystems[(int)MaterialType.Glass] = hardGenericImpact;
            impactSystems[(int)MaterialType.GlassShattered] = hardGenericImpact;
            impactSystems[(int)MaterialType.Grate] = metalImpact;
            impactSystems[(int)MaterialType.GrassHigh] = grassImpact;
            impactSystems[(int)MaterialType.GrassLow] = grassImpact;
            impactSystems[(int)MaterialType.Gravel] = softRockImpact;
            impactSystems[(int)MaterialType.MetalThin] = metalImpact;
            impactSystems[(int)MaterialType.MetalThick] = metalImpact;
            impactSystems[(int)MaterialType.Mud] = mudImpact;
            impactSystems[(int)MaterialType.Pebbles] = softRockImpact;
            impactSystems[(int)MaterialType.Plastic] = softGenericImpact;
            impactSystems[(int)MaterialType.Stone] = hardRockImpact;
            impactSystems[(int)MaterialType.Soil] = mudImpact;
            impactSystems[(int)MaterialType.SoilForest] = mudImpact;
            impactSystems[(int)MaterialType.Tile] = softRockImpact;
            impactSystems[(int)MaterialType.WoodThick] = woodImpact;
            impactSystems[(int)MaterialType.WoodThin] = woodImpact;
            impactSystems[(int)MaterialType.Tyre] = softGenericImpact;
            impactSystems[(int)MaterialType.Rubber] = softGenericImpact;
            impactSystems[(int)MaterialType.GenericHard] = hardGenericImpact;
            impactSystems[(int)MaterialType.MetalNoDecal] = metalImpact;
            impactSystems[(int)MaterialType.None] = hardGenericImpact;
            impactSystems[(int)MaterialType.BodyArmor] = null;
            impactSystems[(int)MaterialType.Helmet] = null;
            impactSystems[(int)MaterialType.GlassVisor] = null;
            impactSystems[(int)MaterialType.Body] = null;
            impactSystems[(int)MaterialType.HelmetRicochet] = null;
            
            return impactSystems;
        }
    }
}
