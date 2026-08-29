using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using DeferredDecals;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using HollywoodFX.Decal;
using HollywoodFX.Muzzle;
using HollywoodFX.Muzzle.Patches;
using SPT.Reflection.Patching;
using Systems.Effects;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollywoodFX.Patches;

public class EffectsAwakePrefixPatch : ModulePatch
{
    private static readonly List<Material> OwnedBloodMaterials = new();

    protected override MethodBase GetTargetMethod()
    {
        return typeof(Effects).GetMethod(nameof(Effects.Awake));
    }

    [PatchPrefix]
    // ReSharper disable once InconsistentNaming
    public static void Prefix(Effects __instance)
    {
        if (__instance.name.Contains("HFX"))
        {
            Plugin.Log.LogInfo($"Skipping EffectsAwakePrefixPatch Reentrancy for HFX effects {__instance.name}");
            return;
        }

        if (GameWorldAwakePrefixPatch.IsHideout)
        {
            Plugin.Log.LogInfo("Skipping EffectsAwakePrefixPatch for the Hideout");
            return;
        }

        try
        {
            SetDecalLimits(__instance);
            SetDecalsProps(__instance);
            WipeDefaultParticles(__instance);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"EffectsAwakePrefixPatch Exception: {e}");
            throw;
        }
    }

    private static void SetDecalsProps(Effects eftEffects)
    {
        var decalsHfxPrefab = AssetRegistry.AssetBundle.LoadAsset<GameObject>("Assets/HollywoodFX/Particles/Prefabs/HFX Decals.prefab");
        Plugin.Log.LogInfo("Instantiating Decal Effects Prefab");
        var decalsHfxInstance = Object.Instantiate(decalsHfxPrefab);
        Plugin.Log.LogInfo("Getting Effects Component");
        var decalsHfxEffects = decalsHfxInstance.GetComponent<Effects>();

        if (Plugin.WoundDecalsEnabled.Value && Plugin.BloodRenderOwnership.AllowBodyWoundTextureDecals)
        {
            var texDecalsOrigTraverse = Traverse.Create(eftEffects.TexDecals);

            texDecalsOrigTraverse.Field("_renderTexDimension").SetValue(PowOfTwoDimensions._1024);

            var bloodDecalsHfx = Traverse.Create(decalsHfxEffects.TexDecals).Field("_bloodDecalTexture").GetValue();
            var vestDecalsHfx = Traverse.Create(decalsHfxEffects.TexDecals).Field("_vestDecalTexture").GetValue();
            var backDecalsHfx = Traverse.Create(decalsHfxEffects.TexDecals).Field("_backDecalTexture").GetValue();
            if (bloodDecalsHfx != null)
            {
                Plugin.Log.LogInfo("Overriding blood decal textures");
                texDecalsOrigTraverse.Field("_bloodDecalTexture").SetValue(bloodDecalsHfx);
                texDecalsOrigTraverse.Field("_vestDecalTexture").SetValue(vestDecalsHfx);
                texDecalsOrigTraverse.Field("_backDecalTexture").SetValue(backDecalsHfx);
                texDecalsOrigTraverse.Field("_decalSize").SetValue(new Vector2(0.1f, 0.115f) * Plugin.WoundDecalsSize.Value);
            }
        }

        if (Plugin.BloodSplatterDecalsEnabled.Value &&
            Plugin.BloodRenderOwnership.AllowEnvironmentDecalOverrides)
        {
            var decalRenderer = eftEffects.DeferredDecals;

            if (decalRenderer == null) return;

            var bleedingDecalOrig = Traverse.Create(decalRenderer).Field("_bleedingDecal").GetValue<DeferredDecalRenderer.SingleDecal>();
            var bleedingDecalNew = Traverse.Create(decalsHfxEffects.DeferredDecals).Field("_bleedingDecal").GetValue<DeferredDecalRenderer.SingleDecal>();

            if (bleedingDecalOrig == null || bleedingDecalNew == null) return;

            var matteStaticMaterial = CreateMatteBloodMaterial(bleedingDecalNew.DecalMaterial, "static");
            var matteDynamicMaterial = ReferenceEquals(bleedingDecalNew.DecalMaterial, bleedingDecalNew.DynamicDecalMaterial)
                ? matteStaticMaterial
                : CreateMatteBloodMaterial(bleedingDecalNew.DynamicDecalMaterial, "dynamic");

            if (matteStaticMaterial != null)
                bleedingDecalOrig.DecalMaterial = matteStaticMaterial;

            if (matteDynamicMaterial != null)
                bleedingDecalOrig.DynamicDecalMaterial = matteDynamicMaterial;

            bleedingDecalOrig.TileSheetRows = bleedingDecalNew.TileSheetRows;
            bleedingDecalOrig.TileSheetColumns = bleedingDecalNew.TileSheetColumns;
            BloodDecalPresentation.ResolveBleedingDecalSize(
                Plugin.BloodSplatterDecalsSize.Value,
                out var bleedingDecalWidth,
                out var bleedingDecalHeight);
            bleedingDecalOrig.DecalSize = new Vector2(bleedingDecalWidth, bleedingDecalHeight);

            var splatterDecalOrig = Traverse.Create(decalRenderer).Field("_environmentBlood").GetValue<DeferredDecalRenderer.SingleDecal>();
            var splatterDecalNew = Traverse.Create(decalsHfxEffects.DeferredDecals).Field("_environmentBlood").GetValue<DeferredDecalRenderer.SingleDecal>();

            if (splatterDecalOrig == null || splatterDecalNew == null) return;

            var matteSplatterStaticMaterial = CreateMatteBloodMaterial(
                splatterDecalNew.DecalMaterial, "static environment-splatter");
            var matteSplatterDynamicMaterial = ReferenceEquals(
                splatterDecalNew.DecalMaterial, splatterDecalNew.DynamicDecalMaterial)
                ? matteSplatterStaticMaterial
                : CreateMatteBloodMaterial(
                    splatterDecalNew.DynamicDecalMaterial, "dynamic environment-splatter");

            if (matteSplatterStaticMaterial != null)
                splatterDecalOrig.DecalMaterial = matteSplatterStaticMaterial;

            if (matteSplatterDynamicMaterial != null)
                splatterDecalOrig.DynamicDecalMaterial = matteSplatterDynamicMaterial;

            splatterDecalOrig.TileSheetRows = splatterDecalNew.TileSheetRows;
            splatterDecalOrig.TileSheetColumns = splatterDecalNew.TileSheetColumns;
            splatterDecalOrig.DecalSize *=
                BloodDecalPresentation.ResolveEnvironmentDecalScale(
                    Plugin.BloodSplatterDecalsSize.Value);
        }

        var impactDecals = Traverse.Create(decalsHfxEffects.DeferredDecals).Field("_decals").GetValue<DeferredDecalRenderer.SingleDecal[]>();
        Decals.TracerScorchMark = impactDecals[0];
        Plugin.Log.LogInfo($"Extracted decal: {Decals.TracerScorchMark} > {Decals.TracerScorchMark.DecalMaterial.name}");

        Plugin.Log.LogInfo("Decal overrides complete");
    }

    private static Material CreateMatteBloodMaterial(Material source, string usage)
    {
        if (source == null)
        {
            Plugin.Log.LogWarning($"Keeping the existing {usage} blood decal material because the HFX source was null");
            return null;
        }

        Material material = null;
        try
        {
            material = new Material(source)
            {
                name = $"{source.name} HFX Matte Blood"
            };

            SetFloatIfPresent(material, "_Glossiness", BloodDecalPresentation.MatteGlossiness);
            SetFloatIfPresent(material, "Glossiness", BloodDecalPresentation.MatteGlossiness);
            SetFloatIfPresent(material, "_Metallic", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "Metallic", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "_SpecularHighlights", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "SpecularHighlights", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "_GlossyReflections", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "GlossyReflections", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "_Emission", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "Emission", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "_NormalPower", BloodDecalPresentation.MatteNormalPower);
            SetFloatIfPresent(material, "NormalPower", BloodDecalPresentation.MatteNormalPower);
            SetFloatIfPresent(material, "_SpecSmoothness", BloodDecalPresentation.DisabledSurfaceResponse);
            SetFloatIfPresent(material, "SpecSmoothness", BloodDecalPresentation.DisabledSurfaceResponse);
            MultiplyColorIfPresent(material, "_Color");
            MultiplyColorIfPresent(material, "_TintColor");
            MultiplyColorIfPresent(material, "_BaseColor");
            SetColorIfPresent(material, "_SpecularColor", Color.clear);
            SetColorIfPresent(material, "SpecularColor", Color.clear);
            SetColorIfPresent(material, "_EmissionColor", Color.black);
            SetColorIfPresent(material, "EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");

            OwnedBloodMaterials.Add(material);
            Plugin.Log.LogInfo($"Prepared one matte {usage} blood decal material from {source.name}");
            return material;
        }
        catch (Exception exception)
        {
            if (material != null)
                Object.Destroy(material);

            Plugin.Log.LogWarning($"Keeping the existing {usage} blood decal material because matte preparation failed: {exception.Message}");
            return null;
        }
    }

    internal static void ReleaseOwnedBloodMaterials()
    {
        var released = OwnedBloodMaterials.Count;
        foreach (var material in OwnedBloodMaterials)
        {
            if (material != null)
                Object.Destroy(material);
        }

        OwnedBloodMaterials.Clear();
        if (released > 0)
            Plugin.Log.LogInfo($"Released {released} owned blood decal material clone(s)");
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    private static void MultiplyColorIfPresent(Material material, string propertyName)
    {
        if (!material.HasProperty(propertyName))
            return;

        var sourceColor = material.GetColor(propertyName);
        BloodDecalPresentation.ResolveAbsorbedTint(
            sourceColor.r,
            sourceColor.g,
            sourceColor.b,
            sourceColor.a,
            out var red,
            out var green,
            out var blue,
            out var alpha);
        material.SetColor(propertyName, new Color(red, green, blue, alpha));
    }

    private static void SetDecalLimits(Effects effects)
    {
        if (!Plugin.MiscDecalsEnabled.Value)
            return;

        Plugin.Log.LogInfo("Adjusting decal limits");

        var decalRenderer = effects.DeferredDecals;

        if (decalRenderer == null) return;

        var newDecalLimit = Plugin.MiscMaxDecalCount.Value;

        var decalRendererTraverse = Traverse.Create(decalRenderer);

        var maxStaticDecalsValue = decalRendererTraverse.Field("_maxDecals").GetValue<int>();
        Plugin.Log.LogWarning($"Current static decals limit is: {maxStaticDecalsValue}");
        if (maxStaticDecalsValue != newDecalLimit)
        {
            Plugin.Log.LogWarning($"Setting max static decals to {newDecalLimit}");
            decalRendererTraverse.Field("_maxDecals").SetValue(newDecalLimit);
        }

        var maxDynamicDecalsValue = decalRendererTraverse.Field("_maxDynamicDecals").GetValue<int>();
        Plugin.Log.LogWarning($"Current dynamic decals limit is: {maxDynamicDecalsValue}");
        if (maxDynamicDecalsValue != newDecalLimit)
        {
            Plugin.Log.LogWarning($"Setting max dynamic decals to {newDecalLimit}");
            decalRendererTraverse.Field("_maxDynamicDecals").SetValue(newDecalLimit);
        }
    }

    private static void WipeDefaultParticles(Effects effects)
    {
        Plugin.Log.LogInfo("Dropping default impact effects");

        foreach (var effect in effects.EffectsArray)
        {
            // Skip effects which have no material attached
            var name = effect.Name.ToLower();
            
            if (effect.MaterialTypes.Length == 0 || name.Contains("water") || name.Contains("swamp"))
            {
                Plugin.Log.LogInfo($"Skipping {effect.Name}");
                continue;
            }

            if (name.Contains("metal"))
            {
                Plugin.Log.LogInfo("Enhancing lighting");
                effect.FlashMaxDist *= 2f;
                effect.LightIntensity *= 2f;
                effect.LightRange *= 1.5f;
                effect.LightMaxDist *= 2f;
            }

            Plugin.Log.LogInfo($"Wiping {effect.Name}");
            effect.Particles = [];
        }
    }
}

public class EffectsAwakePostfixPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Effects).GetMethod(nameof(Effects.Awake));
    }

    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(Effects __instance)
    {
        if (__instance.name.Contains("HFX"))
        {
            Plugin.Log.LogInfo($"Skipping EffectsAwakePostfixPatch Reentrancy for HFX effects {__instance.name}");
            return;
        }

        if (GameWorldAwakePrefixPatch.IsHideout)
        {
            Plugin.Log.LogInfo("Skipping EffectsAwakePostfixPatch for the Hideout");
            return;
        }

        try
        {
            Singleton<ImpactController>.Create(new ImpactController(__instance));
            Singleton<DecalPainter>.Create(new DecalPainter(__instance.DeferredDecals));
            PenetrationApertures.Initialize(__instance);
            
            if (Plugin.MuzzleEffectsEnabled.Value)
            {
                Singleton<FirearmsEffectsCache>.Create(new FirearmsEffectsCache());
                Singleton<MuzzleStatic>.Create(new MuzzleStatic());
                Singleton<MuzzleEffects>.Create(new MuzzleEffects(__instance, true));
                Singleton<LocalPlayerMuzzleEffects>.Create(new LocalPlayerMuzzleEffects(__instance));
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"EffectsAwakePostfixPatch Exception: {e}");
            throw;
        }
    }
}
