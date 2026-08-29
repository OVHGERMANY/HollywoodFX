using HollywoodFX.Decal;
using HollywoodFX.Gore;

var tests = new (string Name, Action Run)[]
{
    ("confirmed penetration creates distinct paired faces", ConfirmedPenetrationCreatesPair),
    ("stopped impact creates no aperture and preserves impact", StoppedImpactCreatesNoAperture),
    ("unmatched exit creates no aperture", UnmatchedExitCreatesNoAperture),
    ("tracker clear rejects stale pooled shot", ClearRejectsStaleShot),
    ("normal incidence keeps a circular physical aperture", NormalIncidenceIsCircular),
    ("grazing incidence stretch is bounded", GrazingIncidenceIsBounded),
    ("compound bullet-hole replacement is retired", CompoundBulletHoleReplacementIsRetired),
    ("character surfaces use attached marks without cut-through apertures", CharacterSurfacesUseAttachedMarks),
    ("character mark scale is small and bounded", CharacterMarkScaleIsSmallAndBounded),
    ("blood splatter default is reduced by thirty-five percent", BloodSplatterDefaultIsReduced),
    ("bleeding decal dimensions use the reduced default", BleedingDecalDimensionsUseReducedDefault),
    ("environment splatter has no hidden enlargement", EnvironmentSplatterHasNoHiddenEnlargement),
    ("blood materials use a matte surface response", BloodMaterialsUseMatteSurfaceResponse),
    ("absorbed blood tint darkens rgb and preserves alpha", AbsorbedBloodTintDarkensRgbAndPreservesAlpha),
    ("TraumaCore plugin GUID is exact", TraumaCorePluginGuidIsExact),
    ("TraumaCore owns sustained and deposited blood", TraumaCoreOwnsSustainedAndDepositedBlood),
    ("TraumaCore owns corpse wound emission without losing textures", TraumaCoreOwnsCorpseWoundEmission),
    ("TraumaCore keeps non-blood impact effects", TraumaCoreKeepsNonBloodImpactEffects),
    ("TraumaCore skips dormant HFX blood pools", TraumaCoreSkipsDormantHfxBloodPools),
    ("standalone HFX retains its blood layers", StandaloneHfxRetainsItsBloodLayers),
    ("tent body material never emits gore", TentBodyMaterialNeverEmitsGore),
    ("player body hit emits gore", PlayerBodyHitEmitsGore),
    ("corpse body hit emits gore", CorpseBodyHitEmitsGore),
    ("armor material keeps armor effects eligible", ArmorMaterialKeepsArmorEffectsEligible),
    ("repeated non-body hits never become gore", RepeatedNonBodyHitsNeverBecomeGore)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + exception.Message);
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static void ConfirmedPenetrationCreatesPair()
{
    var tracker = new PenetrationApertureTracker();
    var shot = new object();
    var entry = tracker.Record(shot, isForwardHit: true, isConfirmedPenetration: true);
    var exit = tracker.Record(shot, isForwardHit: false, isConfirmedPenetration: false);

    Require(entry.CreateAperture, "entry was not created");
    Require(exit.CreateAperture, "exit was not created");
    Require(entry.Face == PenetrationApertureFace.Entry, "near face was misclassified");
    Require(exit.Face == PenetrationApertureFace.Exit, "far face was misclassified");
    Require(entry.PairIdentity == exit.PairIdentity, "faces did not retain one pair identity");
    Require(entry.Identity != exit.Identity, "entry and exit reused one aperture identity");
    Require(entry.PreserveImpact && exit.PreserveImpact, "stock impact preservation was lost");
}

static void StoppedImpactCreatesNoAperture()
{
    var tracker = new PenetrationApertureTracker();
    var result = tracker.Record(new object(), isForwardHit: true, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "a stopped impact created a see-through opening");
    Require(result.PreserveImpact, "the existing stopped-impact mark must remain untouched");
}

static void UnmatchedExitCreatesNoAperture()
{
    var tracker = new PenetrationApertureTracker();
    var result = tracker.Record(new object(), isForwardHit: false, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "an unpaired far-face event created an opening");
}

static void ClearRejectsStaleShot()
{
    var tracker = new PenetrationApertureTracker();
    var shot = new object();
    tracker.Record(shot, isForwardHit: true, isConfirmedPenetration: true);
    tracker.Clear();
    var result = tracker.Record(shot, isForwardHit: false, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "world cleanup left a stale pending entry");
}

static void NormalIncidenceIsCircular()
{
    PenetrationApertureGeometry.ResolveRadii(7.62f, 1f, out var minor, out var major);
    RequireNear(minor, 0.0043815f, 0.000001f, "physical radius");
    RequireNear(major, minor, 0.000001f, "normal-incidence major radius");
}

static void GrazingIncidenceIsBounded()
{
    PenetrationApertureGeometry.ResolveRadii(7.62f, 0.01f, out var minor, out var major);
    RequireNear(
        major,
        minor * PenetrationApertureGeometry.MaximumIncidenceStretch,
        0.000001f,
        "grazing stretch");
}

static void CompoundBulletHoleReplacementIsRetired()
{
    var impactSource = ReadEmbeddedSource("SurfaceImpactMarks.cs");
    var pluginSource = ReadEmbeddedSource("Plugin.cs");

    foreach (var retiredIdentifier in new[]
             {
                 "TryBuildCompoundMark",
                 "MergeOverlappingBulletHoles",
                 "BulletHoleMergeDistance"
             })
    {
        Require(!impactSource.Contains(retiredIdentifier, StringComparison.Ordinal),
            $"impact runtime still contains retired identifier {retiredIdentifier}");
        Require(!pluginSource.Contains(retiredIdentifier, StringComparison.Ordinal),
            $"plugin config still contains retired identifier {retiredIdentifier}");
    }

    Require(impactSource.Contains("TryDrawRicochet", StringComparison.Ordinal),
        "ricochet-specific replacement was removed with the merger");
    Require(impactSource.Contains("shouldDrawOblique", StringComparison.Ordinal),
        "oblique-impact replacement was removed with the merger");
    Require(impactSource.Contains("RegisterReplacement(kinetics.Position, \"oblique-stop\")", StringComparison.Ordinal),
        "oblique replacement no longer suppresses only its matching stock decal");
}

static void CharacterSurfacesUseAttachedMarks()
{
    Require(!CustomImpactGeometryPolicy.ShouldUseCustomGeometry(
            isBodyPartCollider: true, hasPlayerOrCorpseOwner: false),
        "a body-part, armor, helmet, or face-mask collider accepted custom geometry");
    Require(!CustomImpactGeometryPolicy.ShouldUseCustomGeometry(
            isBodyPartCollider: false, hasPlayerOrCorpseOwner: true),
        "a player- or corpse-owned collider accepted custom geometry");
    Require(CustomImpactGeometryPolicy.ShouldUseCustomGeometry(
            isBodyPartCollider: false, hasPlayerOrCorpseOwner: false),
        "an unowned world surface lost custom impact geometry");

    var marksSource = ReadEmbeddedSource("SurfaceImpactMarks.cs");
    var aperturesSource = ReadEmbeddedSource("PenetrationApertures.cs");
    foreach (var source in new[] { marksSource, aperturesSource })
    {
        var guardIndex = source.IndexOf(
            "CustomImpactGeometryPolicy.ShouldUseCustomGeometry",
            StringComparison.Ordinal);
        Require(guardIndex >= 0,
            "a custom impact path is missing the character-surface guard");
    }

    var markGuard = marksSource.IndexOf(
        "CustomImpactGeometryPolicy.ShouldUseCustomGeometry",
        StringComparison.Ordinal);
    var drawIndex = marksSource.IndexOf("DrawOrientedDecal(",
        StringComparison.Ordinal);
    var replacementIndex = marksSource.IndexOf("RegisterReplacement(",
        StringComparison.Ordinal);
    Require(markGuard >= 0 && drawIndex > markGuard &&
            replacementIndex > markGuard,
        "the character guard runs after custom drawing or native-mark suppression");

    var apertureGuard = aperturesSource.IndexOf(
        "CustomImpactGeometryPolicy.ShouldUseCustomGeometry",
        StringComparison.Ordinal);
    var trackerIndex = aperturesSource.IndexOf("Tracker.Record(",
        StringComparison.Ordinal);
    Require(apertureGuard >= 0 && trackerIndex > apertureGuard,
        "the character guard runs after penetration tracking");

    Require(CharacterImpactMarkPolicy.ShouldDraw(
            isBodyPartCollider: true,
            materialLooksLikeCharacterSurface: false,
            hasPlayerOrCorpseOwner: true,
            isDynamicCollider: true,
            hasShot: true,
            hasValidGeometry: true),
        "logical armor over a moving body-part collider did not receive an attached mark");
    Require(!CharacterImpactMarkPolicy.ShouldDraw(
            isBodyPartCollider: true,
            materialLooksLikeCharacterSurface: true,
            hasPlayerOrCorpseOwner: false,
            isDynamicCollider: true,
            hasShot: true,
            hasValidGeometry: true),
        "an unowned tarp or world prop accepted a character impact mark");
    Require(!CharacterImpactMarkPolicy.ShouldDraw(
            isBodyPartCollider: true,
            materialLooksLikeCharacterSurface: true,
            hasPlayerOrCorpseOwner: true,
            isDynamicCollider: false,
            hasShot: true,
            hasValidGeometry: true),
        "a static collider accepted a character-attached projector");

    var characterSource = ReadEmbeddedSource("CharacterImpactMarks.cs");
    var lifecycleSource = ReadEmbeddedSource("ShotLifecycle.cs");
    Require(characterSource.Contains("Decals.TracerScorchMark", StringComparison.Ordinal),
        "character impact marks no longer use restrained dark impact artwork");
    Require(characterSource.Contains("DrawOrientedDecal(", StringComparison.Ordinal) &&
            characterSource.Contains("hitCollider,", StringComparison.Ordinal),
        "character impact mark is not anchored through the exact hit collider");
    Require(!characterSource.Contains("ResolveSurfaceRenderer", StringComparison.Ordinal) &&
            !characterSource.Contains("GetComponent<Renderer>", StringComparison.Ordinal),
        "character impact mark resumed guessing a renderer");
    RequireGuardBefore(characterSource,
        "if (drawn)",
        "SurfaceImpactMarks.RegisterReplacement",
        "the stock decal is suppressed before an attached character mark succeeds");
    RequireGuardBefore(lifecycleSource,
        "CharacterImpactMarks.TryDraw",
        "PenetrationApertures.TryDraw",
        "character-attached mark is evaluated after the unsafe aperture path");
}

static void CharacterMarkScaleIsSmallAndBounded()
{
    RequireNear(CharacterImpactMarkPolicy.ResolveScale(0.1f),
        CharacterImpactMarkPolicy.MinimumScale, 0.000001f, "minimum character mark scale");
    RequireNear(CharacterImpactMarkPolicy.ResolveScale(1f),
        CharacterImpactMarkPolicy.BaseScale, 0.000001f, "reference character mark scale");
    RequireNear(CharacterImpactMarkPolicy.ResolveScale(10f),
        CharacterImpactMarkPolicy.MaximumScale, 0.000001f, "maximum character mark scale");
    Require(CharacterImpactMarkPolicy.ProjectorHeight <= 0.06f,
        "character projector is deep enough to paint unrelated body surfaces");
}

static void BloodSplatterDefaultIsReduced()
{
    RequireNear(BloodDecalPresentation.DefaultSizeMultiplier, 0.65f, 0.000001f, "blood splatter default");
}

static void BleedingDecalDimensionsUseReducedDefault()
{
    BloodDecalPresentation.ResolveBleedingDecalSize(
        BloodDecalPresentation.DefaultSizeMultiplier,
        out var width,
        out var height);

    RequireNear(width, 0.08125f, 0.000001f, "bleeding decal width");
    RequireNear(height, 0.11375f, 0.000001f, "bleeding decal height");
}

static void EnvironmentSplatterHasNoHiddenEnlargement()
{
    float scale = BloodDecalPresentation.ResolveEnvironmentDecalScale(
        BloodDecalPresentation.DefaultSizeMultiplier);
    RequireNear(scale, 0.65f, 0.000001f,
        "environment splatter scale");
}

static void BloodMaterialsUseMatteSurfaceResponse()
{
    RequireNear(BloodDecalPresentation.MatteGlossiness, 0.06f, 0.000001f, "matte glossiness");
    RequireNear(BloodDecalPresentation.MatteNormalPower, 1f, 0.000001f, "matte normal power");
    RequireNear(BloodDecalPresentation.DisabledSurfaceResponse, 0f, 0.000001f, "disabled surface response");
}

static void AbsorbedBloodTintDarkensRgbAndPreservesAlpha()
{
    BloodDecalPresentation.ResolveAbsorbedTint(
        red: 0.8f,
        green: 0.4f,
        blue: 0.2f,
        alpha: 0.37f,
        out var red,
        out var green,
        out var blue,
        out var alpha);

    RequireNear(red, 0.544f, 0.000001f, "absorbed tint red");
    RequireNear(green, 0.208f, 0.000001f, "absorbed tint green");
    RequireNear(blue, 0.096f, 0.000001f, "absorbed tint blue");
    RequireNear(alpha, 0.37f, 0.000001f, "absorbed tint alpha");
}

static void TraumaCorePluginGuidIsExact()
{
    Require(BloodRenderOwnershipPolicy.TraumaCorePluginGuid == "com.hysocs.traumacore",
        "the compatibility check would query the wrong BepInEx plugin GUID");
}

static void TraumaCoreOwnsSustainedAndDepositedBlood()
{
    var ownership = BloodRenderOwnershipPolicy.Resolve(traumaCoreLoaded: true);

    Require(ownership.TraumaCoreLoaded, "TraumaCore detection was not retained");
    Require(!ownership.AllowTransientImpactPuffsAndSprays,
        "TraumaCore compatibility left unsafe HFX transient blood materials enabled");
    Require(ownership.AllowBodyWoundTextureDecals,
        "TraumaCore compatibility removed HFX body wound textures");
    Require(!ownership.AllowBodyWoundTextureEmission,
        "TraumaCore compatibility left duplicate HFX corpse wound emission enabled");
    Require(!ownership.AllowImpactSquirts,
        "TraumaCore compatibility left HFX impact gushers enabled");
    Require(!ownership.AllowDeathBloodEffects,
        "TraumaCore compatibility left HFX death blood enabled");
    Require(!ownership.AllowParticleCollisionEnvironmentDeposits,
        "TraumaCore compatibility left HFX particle collision deposits enabled");
    Require(!ownership.AllowEnvironmentDecalOverrides,
        "TraumaCore compatibility left HFX environment decal overrides enabled");
}

static void TraumaCoreOwnsCorpseWoundEmission()
{
    var ownership = BloodRenderOwnershipPolicy.Resolve(traumaCoreLoaded: true);
    var controllerSource = ReadEmbeddedSource("GoreController.cs");
    Require(ownership.AllowBodyWoundTextureDecals,
        "the shared HFX wound artwork was disabled");
    Require(!ownership.AllowBodyWoundTextureEmission,
        "HFX still owns corpse wound emission with TraumaCore loaded");
    RequireGuardBefore(controllerSource,
        "AllowBodyWoundTextureEmission",
        "EffectsCommutator.PlayerMeshesHit",
        "the duplicate corpse wound call runs before its ownership guard");
}

static void TraumaCoreKeepsNonBloodImpactEffects()
{
    var controllerSource = ReadEmbeddedSource("GoreController.cs");
    var effectsSource = ReadEmbeddedSource("BodyImpactEffects.cs");
    var emitMethodIndex = effectsSource.IndexOf("public void Emit(ImpactKinetics", StringComparison.Ordinal);
    Require(emitMethodIndex >= 0, "BodyImpactEffects.Emit source was not found");
    var emitSource = effectsSource[emitMethodIndex..];
    var armorIndex = emitSource.IndexOf("case MaterialType.BodyArmor", StringComparison.Ordinal);
    var stoppedIndex = emitSource.IndexOf("if (!bullet.Penetrated)", StringComparison.Ordinal);
    var ownershipIndex = emitSource.IndexOf(
        "if (!Plugin.BloodRenderOwnership.AllowTransientImpactPuffsAndSprays)",
        StringComparison.Ordinal);
    var sprayIndex = emitSource.IndexOf("_sprays?.Emit", StringComparison.Ordinal);

    Require(controllerSource.Contains("_bodyImpactEffects.Emit(kinetics, rigidbody)", StringComparison.Ordinal),
        "GoreController no longer routes body impacts to the material-specific effect layer");
    Require(!controllerSource.Contains("AllowTransientImpactPuffsAndSprays", StringComparison.Ordinal),
        "GoreController still suppresses non-blood impact effects at the outer gate");
    Require(armorIndex >= 0 && stoppedIndex > armorIndex && ownershipIndex > stoppedIndex && sprayIndex > ownershipIndex,
        "the TraumaCore ownership gate does not preserve armor debris, helmet sparks, and stopped-round dust before blood suppression");
}

static void TraumaCoreSkipsDormantHfxBloodPools()
{
    var controllerSource = ReadEmbeddedSource("ImpactController.cs");
    var effectsSource = ReadEmbeddedSource("BodyImpactEffects.cs");

    RequireGuardBefore(controllerSource,
        "AllowTransientImpactPuffsAndSprays",
        "HFX Blood Main.prefab",
        "main blood prefab is loaded without an ownership gate");
    RequireGuardBefore(controllerSource,
        "AllowImpactSquirts",
        "HFX Blood Squirts.prefab",
        "squirt prefab is loaded without an ownership gate");
    RequireGuardBefore(controllerSource,
        "AllowDeathBloodEffects",
        "HFX Blood Bleedout.prefab",
        "bleedout prefab is loaded without an ownership gate");
    RequireGuardBefore(effectsSource,
        "AllowTransientImpactPuffsAndSprays",
        "EffectBundle.LoadPrefab(eftEffects, prefabMain, false)",
        "transient blood systems are built without an ownership gate");
    RequireGuardBefore(effectsSource,
        "AllowImpactSquirts",
        "_squirts = eftEffects.gameObject.AddComponent<RigidbodyEffects>()",
        "squirt pool is built without an ownership gate");
    RequireGuardBefore(effectsSource,
        "AllowDeathBloodEffects",
        "_bleedouts = eftEffects.gameObject.AddComponent<RigidbodyEffects>()",
        "death-blood pools are built without an ownership gate");
}

static void StandaloneHfxRetainsItsBloodLayers()
{
    var ownership = BloodRenderOwnershipPolicy.Resolve(traumaCoreLoaded: false);

    Require(!ownership.TraumaCoreLoaded, "standalone HFX incorrectly detected TraumaCore");
    Require(ownership.AllowTransientImpactPuffsAndSprays,
        "standalone HFX lost instantaneous impact blood");
    Require(ownership.AllowBodyWoundTextureDecals,
        "standalone HFX lost body wound textures");
    Require(ownership.AllowBodyWoundTextureEmission,
        "standalone HFX lost body wound emission");
    Require(ownership.AllowImpactSquirts, "standalone HFX lost impact squirts");
    Require(ownership.AllowDeathBloodEffects, "standalone HFX lost death blood");
    Require(ownership.AllowParticleCollisionEnvironmentDeposits,
        "standalone HFX lost particle collision deposits");
    Require(ownership.AllowEnvironmentDecalOverrides,
        "standalone HFX lost environment decal overrides");
}

static void TentBodyMaterialNeverEmitsGore()
{
    Require(!GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody: true, hasPlayerOrCorpseOwner: false),
        "a body-material tent hit emitted gore without a body owner");
}

static void PlayerBodyHitEmitsGore()
{
    Require(GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody: true, hasPlayerOrCorpseOwner: true),
        "a player body hit was rejected");
}

static void CorpseBodyHitEmitsGore()
{
    Require(GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody: true, hasPlayerOrCorpseOwner: true),
        "a corpse body hit was rejected");
}

static void ArmorMaterialKeepsArmorEffectsEligible()
{
    Require(GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody: true, hasPlayerOrCorpseOwner: true),
        "a player-owned armor or helmet hit was rejected");
}

static void RepeatedNonBodyHitsNeverBecomeGore()
{
    for (var index = 0; index < 128; index++)
    {
        Require(!GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody: true, hasPlayerOrCorpseOwner: false),
            "a repeated world hit became eligible for gore");
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string ReadEmbeddedSource(string fileName)
{
    var assembly = typeof(Program).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.Ordinal));

    Require(resourceName != null, $"embedded source {fileName} was not found");
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Require(stream != null, $"embedded source {fileName} could not be opened");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

static void RequireNear(float actual, float expected, float tolerance, string label)
{
    if (Math.Abs(actual - expected) > tolerance)
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}

static void RequireGuardBefore(string source, string guard, string guardedOperation, string message)
{
    var guardIndex = source.IndexOf(guard, StringComparison.Ordinal);
    var operationIndex = source.IndexOf(guardedOperation, StringComparison.Ordinal);
    Require(guardIndex >= 0 && operationIndex > guardIndex, message);
}
