using HollywoodFX.Decal;
using HollywoodFX.Gore;
using HollywoodFX.Impact.Sparks;

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
    ("repeated non-body hits never become gore", RepeatedNonBodyHitsNeverBecomeGore),
    ("primary metal is spark eligible", PrimaryMetalIsSparkEligible),
    ("body never produces ballistic sparks", BodyNeverProducesBallisticSparks),
    ("soft surfaces remain spark ineligible", SoftSurfacesRemainSparkIneligible),
    ("mineral output remains below metal", MineralOutputRemainsBelowMetal),
    ("spark probability is monotonic with energy", SparkProbabilityIsMonotonicWithEnergy),
    ("spark count is monotonic with energy", SparkCountIsMonotonicWithEnergy),
    ("spark plans stay inside hard bounds", SparkPlansStayInsideHardBounds),
    ("ricochet favors reflection and tangent", RicochetFavorsReflectionAndTangent),
    ("penetration exit stays below entry", PenetrationExitStaysBelowEntry),
    ("invalid spark geometry fails closed", InvalidSparkGeometryFailsClosed),
    ("zero spark intensity disables emission", ZeroSparkIntensityDisablesEmission),
    ("spark distance attenuation is monotonic", SparkDistanceAttenuationIsMonotonic),
    ("sparks stop beyond maximum distance", SparksStopBeyondMaximumDistance),
    ("extreme energy cannot exceed impact cap", ExtremeEnergyCannotExceedImpactCap),
    ("helmet ricochet remains spark eligible", HelmetRicochetRemainsSparkEligible),
    ("generic armor spark profiles stay conservative", GenericArmorProfilesStayConservative),
    ("spark configuration is focused and live", SparkConfigurationIsFocusedAndLive),
    ("potato template reduces spark load", PotatoTemplateReducesSparkLoad),
    ("legacy extra-flash ownership is retired", LegacyExtraFlashOwnershipIsRetired),
    ("main impact lists no longer own contact sparks", MainImpactListsNoLongerOwnContactSparks),
    ("tracer impacts no longer own generic contact sparks", TracerImpactsNoLongerOwnGenericContactSparks),
    ("tracer light defers to enhanced metal impact light", TracerLightDefersToEnhancedMetalImpactLight),
    ("armor sparks bypass missing main impact lists", ArmorSparksBypassMissingMainImpactLists),
    ("one Effects Emit patch remains", OneEffectsEmitPatchRemains),
    ("spark budget enforces the per-impact cap", SparkBudgetEnforcesPerImpactCap),
    ("spark budget enforces the per-frame cap", SparkBudgetEnforcesPerFrameCap),
    ("spark rolling budget refills predictably", SparkRollingBudgetRefillsPredictably),
    ("spark emitter budgets one leaf particle system", SparkEmitterBudgetsOneLeafParticleSystem),
    ("spark cleanup reports and resets raid state", SparkCleanupReportsAndResetsRaidState),
    ("raw energy resolves 0.1 g at 400 m/s to 8 J", RawEnergyResolvesPointOneGram),
    ("raw energy resolves 1.0 g at 400 m/s to 80 J", RawEnergyResolvesOneGram),
    ("raw energy resolves 3.5 g at 400 m/s to 280 J", RawEnergyResolvesThreePointFiveGrams),
    ("invalid raw energy inputs fail conservatively", InvalidRawEnergyInputsFailConservatively),
    ("zero incoming energy cannot emit", ZeroIncomingEnergyCannotEmit),
    ("low incoming energy gate is smooth and monotonic", LowIncomingEnergyGateIsSmoothAndMonotonic),
    ("spark context has no legacy mass or energy fallback", SparkContextHasNoLegacyEnergyFallback),
    ("eight pellet family obeys cluster caps", EightPelletFamilyObeysClusterCaps),
    ("twelve pellet family obeys cluster caps", TwelvePelletFamilyObeysClusterCaps),
    ("independent shots retain independent cluster budgets", IndependentShotsRetainIndependentClusterBudgets),
    ("fragment family obeys cluster caps", FragmentFamilyObeysClusterCaps),
    ("cluster window expires deterministically", ClusterWindowExpiresDeterministically),
    ("active cluster survives an earlier expired slot", ActiveClusterSurvivesEarlierExpiredSlot),
    ("cluster storage is fixed size value data", ClusterStorageIsFixedSizeValueData),
    ("spark PRNG repeats the complete sample stream", SparkPrngRepeatsCompleteSampleStream),
    ("spark PRNG varies with family and sequence", SparkPrngVariesWithFamilyAndSequence),
    ("new spark path does not consume global randomness", NewSparkPathDoesNotConsumeGlobalRandomness),
    ("shot family key uses verified EFT metadata", ShotFamilyKeyUsesVerifiedEftMetadata),
    ("spark diagnostics cover hardening decisions", SparkDiagnosticsCoverHardeningDecisions),
    ("particle leaf validation logs cached metadata", ParticleLeafValidationLogsCachedMetadata),
    ("emission axis retains finite outward guard", EmissionAxisRetainsFiniteOutwardGuard),
    ("SPT 4.1.4 release metadata stays aligned", Spt414ReleaseMetadataStaysAligned)
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

static void PrimaryMetalIsSparkEligible()
{
    var plan = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped);
    Require(plan.ShouldAttemptEmission, "primary metal was rejected");
    Require(plan.MaximumParticles >= 2, "primary metal requested no visible particles");
}

static void BodyNeverProducesBallisticSparks()
{
    var plan = CreateSparkPlan(BallisticSparkSurfaceClass.Prohibited, BallisticSparkImpactState.Stopped);
    Require(!plan.ShouldAttemptEmission, "a prohibited body surface received sparks");
    Require(plan.RejectionReason == BallisticSparkRejectionReason.Material,
        "body rejection was not attributed to material policy");
}

static void SoftSurfacesRemainSparkIneligible()
{
    var contextSource = ReadEmbeddedSource("BallisticSparkContextBuilder.cs");
    foreach (var material in new[]
             {
                 "Fabric", "WoodThin", "WoodThick", "Soil", "SoilForest", "Mud", "Rubber", "Tyre",
                 "Plastic", "Cardboard", "GarbagePaper", "GenericSoft", "GrassHigh", "GrassLow", "Glass",
                 "GlassShattered", "GlassVisor", "Body"
             })
    {
        Require(contextSource.Contains("MaterialType." + material, StringComparison.Ordinal),
            $"runtime material classifier omitted {material}");
    }

    Require(contextSource.Contains("BallisticSparkSurfaceClass.Prohibited", StringComparison.Ordinal),
        "soft-surface classifier no longer routes to the prohibited policy class");
    var plan = CreateSparkPlan(BallisticSparkSurfaceClass.Prohibited, BallisticSparkImpactState.Ricochet);
    Require(!plan.ShouldAttemptEmission, "a prohibited soft surface bypassed policy during ricochet state");
}

static void MineralOutputRemainsBelowMetal()
{
    var metal = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped);
    var mineral = CreateSparkPlan(BallisticSparkSurfaceClass.SecondaryMineral, BallisticSparkImpactState.Stopped);
    Require(mineral.ShouldAttemptEmission, "secondary mineral was entirely disabled");
    Require(mineral.Probability < metal.Probability, "mineral probability reached metal probability");
    Require(mineral.MaximumParticles < metal.MaximumParticles, "mineral count reached metal count");
}

static void SparkProbabilityIsMonotonicWithEnergy()
{
    var previous = -1f;
    for (var energy = 0f; energy <= 10000f; energy += 25f)
    {
        var plan = CreateSparkPlan(
            BallisticSparkSurfaceClass.PrimaryMetal,
            BallisticSparkImpactState.Stopped,
            energy);
        Require(plan.Probability + 0.000001f >= previous,
            $"probability fell at {energy} J: {plan.Probability} after {previous}");
        previous = plan.Probability;
    }
}

static void SparkCountIsMonotonicWithEnergy()
{
    var previous = -1;
    for (var energy = 0f; energy <= 10000f; energy += 25f)
    {
        var plan = CreateSparkPlan(
            BallisticSparkSurfaceClass.PrimaryMetal,
            BallisticSparkImpactState.Stopped,
            energy);
        Require(plan.MaximumParticles >= previous,
            $"maximum particle count fell at {energy} J: {plan.MaximumParticles} after {previous}");
        previous = plan.MaximumParticles;
    }
}

static void SparkPlansStayInsideHardBounds()
{
    var surfaces = new[]
    {
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkSurfaceClass.SecondaryMineral,
        BallisticSparkSurfaceClass.LowMineral,
        BallisticSparkSurfaceClass.BodyArmor,
        BallisticSparkSurfaceClass.Helmet,
        BallisticSparkSurfaceClass.HelmetRicochet
    };
    var states = Enum.GetValues<BallisticSparkImpactState>();
    var energies = new[] { 0f, 50f, 350f, 1600f, 10000f, 1000000000f };
    var intensities = new[] { 0.1f, 1f, 2f };
    var distances = new[] { 0f, 70f, 139f };

    foreach (var surface in surfaces)
    foreach (var state in states)
    foreach (var energy in energies)
    foreach (var intensity in intensities)
    foreach (var distance in distances)
    {
        var plan = BallisticSparkPolicy.CreatePlan(
            surface, state, energy, 1f, 0.7f, true, distance, intensity, 140f, true);
        if (!plan.ShouldAttemptEmission)
            continue;

        Require(plan.Probability is >= 0f and <= 1f, "probability escaped [0,1]");
        Require(plan.MinimumParticles >= 0, "minimum particle count became negative");
        Require(plan.MaximumParticles <= BallisticSparkPolicy.PerImpactParticleCap,
            "maximum particle count exceeded the hard cap");
        Require(plan.MinimumParticles <= plan.MaximumParticles, "particle range inverted");
    }
}

static void RicochetFavorsReflectionAndTangent()
{
    var stopped = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped);
    var ricochet = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Ricochet);
    var stoppedLateral = stopped.ReflectionDirectionWeight + stopped.TangentDirectionWeight;
    var ricochetLateral = ricochet.ReflectionDirectionWeight + ricochet.TangentDirectionWeight;
    Require(ricochetLateral > stoppedLateral, "ricochet did not increase reflection/tangent bias");
    Require(ricochet.NormalDirectionWeight < stopped.NormalDirectionWeight,
        "ricochet retained a stopped-impact normal bias");
}

static void PenetrationExitStaysBelowEntry()
{
    var entry = CreateSparkPlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.PenetrationEntry,
        2000f);
    var exit = CreateSparkPlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.PenetrationExit,
        2000f,
        isForwardHit: false);
    Require(entry.ShouldAttemptEmission && exit.ShouldAttemptEmission, "entry or exit plan was unavailable");
    Require(exit.Probability < entry.Probability * 0.3f, "exit probability was not substantially reduced");
    Require(exit.MaximumParticles <= Math.Max(1, entry.MaximumParticles / 4),
        "exit count exceeded one quarter of entry output");
}

static void InvalidSparkGeometryFailsClosed()
{
    var plan = BallisticSparkPolicy.CreatePlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.Stopped,
        1600f,
        1f,
        float.NaN,
        true,
        10f,
        1f,
        140f,
        geometryIsValid: false);
    Require(!plan.ShouldAttemptEmission, "invalid geometry produced a spark plan");
    Require(plan.RejectionReason == BallisticSparkRejectionReason.InvalidGeometry,
        "invalid geometry did not fail with the geometry reason");
}

static void ZeroSparkIntensityDisablesEmission()
{
    var plan = BallisticSparkPolicy.CreatePlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.Stopped,
        1600f,
        1f,
        0.8f,
        true,
        10f,
        0f,
        140f,
        true);
    Require(!plan.ShouldAttemptEmission, "zero intensity retained spark emission");
    Require(plan.RejectionReason == BallisticSparkRejectionReason.Disabled,
        "zero intensity did not use the disabled reason");
}

static void SparkDistanceAttenuationIsMonotonic()
{
    var previous = 1.000001f;
    for (var distance = 0f; distance <= 140f; distance += 1f)
    {
        var attenuation = BallisticSparkPolicy.ResolveDistanceAttenuation(distance, 140f);
        Require(attenuation <= previous + 0.000001f,
            $"distance response rose at {distance} m: {attenuation} after {previous}");
        Require(attenuation is >= 0f and <= 1f, "distance response escaped [0,1]");
        previous = attenuation;
    }
}

static void SparksStopBeyondMaximumDistance()
{
    var plan = BallisticSparkPolicy.CreatePlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.Stopped,
        1600f,
        1f,
        0.8f,
        true,
        140.01f,
        1f,
        140f,
        true);
    Require(!plan.ShouldAttemptEmission, "impact beyond maximum distance retained sparks");
    Require(plan.RejectionReason == BallisticSparkRejectionReason.Distance,
        "distant impact did not report distance rejection");
}

static void ExtremeEnergyCannotExceedImpactCap()
{
    var plan = BallisticSparkPolicy.CreatePlan(
        BallisticSparkSurfaceClass.PrimaryMetal,
        BallisticSparkImpactState.Ricochet,
        float.MaxValue,
        100f,
        0.05f,
        true,
        0f,
        2f,
        140f,
        true);
    Require(plan.ShouldAttemptEmission, "extreme energy was rejected instead of bounded");
    Require(plan.MaximumParticles <= BallisticSparkPolicy.PerImpactParticleCap,
        "extreme energy bypassed the per-impact cap");
}

static void HelmetRicochetRemainsSparkEligible()
{
    var plan = CreateSparkPlan(
        BallisticSparkSurfaceClass.HelmetRicochet,
        BallisticSparkImpactState.Ricochet);
    Require(plan.ShouldAttemptEmission, "HelmetRicochet was rejected");
    Require(plan.VisualProfile == BallisticSparkVisualProfile.MetalRicochet,
        "HelmetRicochet lost the restrained ricochet profile");
}

static void GenericArmorProfilesStayConservative()
{
    var metal = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped);
    var bodyArmor = CreateSparkPlan(BallisticSparkSurfaceClass.BodyArmor, BallisticSparkImpactState.Stopped);
    var helmet = CreateSparkPlan(BallisticSparkSurfaceClass.Helmet, BallisticSparkImpactState.Stopped);
    Require(bodyArmor.Probability < metal.Probability && helmet.Probability < metal.Probability,
        "generic armor probability reached primary metal");
    Require(bodyArmor.MaximumParticles <= 4 && helmet.MaximumParticles <= 5,
        "generic armor exceeded its conservative count cap");
}

static void SparkConfigurationIsFocusedAndLive()
{
    var source = ReadEmbeddedSource("Plugin.cs");
    Require(source.Contains("HollywoodFXVersion = $\"{MajorMinorVersion}.17\"", StringComparison.Ordinal),
        "plugin version is not 2.0.17");
    Require(source.Contains("MajorMinorVersion = \"2.0\"", StringComparison.Ordinal),
        "minor update changed the configuration compatibility version");
    Require(source.Contains("\"Enable Ballistic Impact Sparks\", true", StringComparison.Ordinal),
        "spark enable control or default changed");
    Require(source.Contains("\"Ballistic Impact Spark Intensity\", 1f", StringComparison.Ordinal) &&
            source.Contains("new AcceptableValueRange<float>(0f, 2f)", StringComparison.Ordinal),
        "spark intensity default or range changed");
    Require(source.Contains("\"Ballistic Impact Spark Maximum Distance\", 140f", StringComparison.Ordinal) &&
            source.Contains("new AcceptableValueRange<float>(25f, 250f)", StringComparison.Ordinal),
        "spark distance default or range changed");
}

static void PotatoTemplateReducesSparkLoad()
{
    var source = ReadEmbeddedSource("ConfigurationTemplates.cs");
    Require(source.Contains("BallisticImpactSparkIntensity.Value = 0.5f", StringComparison.Ordinal),
        "Potato template does not halve spark intensity");
    Require(source.Contains("BallisticImpactSparkMaximumDistance.Value = 90f", StringComparison.Ordinal),
        "Potato template does not reduce spark distance");
}

static void LegacyExtraFlashOwnershipIsRetired()
{
    var source = ReadEmbeddedSource("ImpactEffects.cs");
    Require(!source.Contains("_extraFlashes", StringComparison.Ordinal), "_extraFlashes still owns sparks");
    Require(!source.Contains("_extraFlashChances", StringComparison.Ordinal),
        "_extraFlashChances still owns material rolls");
}

static void MainImpactListsNoLongerOwnContactSparks()
{
    var source = ReadEmbeddedSource("ImpactEffects.cs");
    foreach (var retiredKey in new[] { "Flash_Sparks", "Spray_Sparks_Light", "Spray_Sparks_Metal" })
    {
        Require(!source.Contains(retiredKey, StringComparison.Ordinal),
            $"main impact lists still contain contact-spark key {retiredKey}");
    }
}

static void TracerImpactsNoLongerOwnGenericContactSparks()
{
    var source = ReadEmbeddedSource("TracerImpactEffects.cs");
    foreach (var retiredKey in new[]
             {
                 "Sparks_Generic", "Sparks_Wide", "Sparks_Ground", "Sparks_Hor_Right", "Sparks_Hor_Left",
                 "Sparks_Falling", "Flash_Sparks"
             })
    {
        Require(!source.Contains(retiredKey, StringComparison.Ordinal),
            $"tracer impacts still contain generic contact-spark key {retiredKey}");
    }

    Require(source.Contains("Sparks_Flammable", StringComparison.Ordinal),
        "tracer-specific combustion residue was removed with generic contact sparks");
}

static void TracerLightDefersToEnhancedMetalImpactLight()
{
    var source = ReadEmbeddedSource("TracerImpactEffects.cs");
    Require(source.Contains("!HasEnhancedNativeImpactLight(kinetics.Material)", StringComparison.Ordinal),
        "tracer light can still stack with the enhanced native metal light");
    foreach (var material in new[]
             {
                 "Chainfence", "GarbageMetal", "Grate", "MetalThin", "MetalThick", "MetalNoDecal"
             })
    {
        Require(source.Contains("MaterialType." + material, StringComparison.Ordinal),
            $"native-light guard omitted {material}");
    }
}

static void ArmorSparksBypassMissingMainImpactLists()
{
    var source = ReadEmbeddedSource("ImpactEffects.cs");
    Require(!source.Contains("if (currentSystems == null)\r\n                return", StringComparison.Ordinal) &&
            !source.Contains("if (currentSystems == null)\n                return", StringComparison.Ordinal),
        "missing main effects still return before independent spark evaluation");
    var optionalMainIndex = source.IndexOf("if (currentSystems != null)", StringComparison.Ordinal);
    var sparksIndex = source.IndexOf("_ballisticSparks.Emit(kinetics, isTracer)", StringComparison.Ordinal);
    Require(optionalMainIndex >= 0 && sparksIndex > optionalMainIndex,
        "ballistic sparks are not evaluated independently after the optional main list");
}

static void OneEffectsEmitPatchRemains()
{
    var lifecycle = ReadEmbeddedSource("ShotLifecycle.cs");
    var impact = ReadEmbeddedSource("ImpactEffects.cs");
    var sparkEffects = ReadEmbeddedSource("BallisticSparkEffects.cs");
    Require(CountOccurrences(lifecycle, "class EffectsEmitPatch") == 1,
        "the existing Effects.Emit patch count changed");
    Require(!impact.Contains("ModulePatch", StringComparison.Ordinal) &&
            !sparkEffects.Contains("ModulePatch", StringComparison.Ordinal),
        "the spark owner introduced another Harmony event patch");
}

static void SparkBudgetEnforcesPerImpactCap()
{
    var budget = new BallisticSparkBudget();
    Require(budget.Consume(1000, 0f, 1) == BallisticSparkPolicy.PerImpactParticleCap,
        "one impact exceeded the per-impact particle cap");
}

static void SparkBudgetEnforcesPerFrameCap()
{
    var budget = new BallisticSparkBudget();
    var emitted = 0;
    for (var index = 0; index < 8; index++)
        emitted += budget.Consume(24, 0f, 7);
    Require(emitted == BallisticSparkBudget.PerFrameParticleCap,
        $"one frame emitted {emitted}, expected {BallisticSparkBudget.PerFrameParticleCap}");
    Require(budget.Consume(1, 0f, 7) == 0, "exhausted frame budget emitted another particle");
}

static void SparkRollingBudgetRefillsPredictably()
{
    var budget = new BallisticSparkBudget();
    var emitted = 0;
    for (var frame = 0; frame < 8; frame++)
        emitted += budget.Consume(24, 0f, frame);
    Require(emitted == BallisticSparkBudget.RollingCapacity,
        "initial rolling budget did not match capacity");
    Require(budget.Consume(24, 0f, 9) == 0, "empty rolling budget emitted without elapsed time");
    Require(budget.Consume(24, 0.5f, 10) == 24, "rolling budget did not refill after half a second");
}

static void SparkEmitterBudgetsOneLeafParticleSystem()
{
    var source = ReadEmbeddedSource("EffectBundle.cs");
    Require(source.Contains("ResolveBallisticSystem(Main, effectKey)", StringComparison.Ordinal),
        "ballistic emission does not select a cached particle system");
    Require(source.Contains("renderer == null || !renderer.enabled", StringComparison.Ordinal) &&
            source.Contains("subEmitters.enabled && subEmitters.subEmittersCount > 0", StringComparison.Ordinal),
        "ballistic emission can trigger an unbudgeted sub-emitter tree");
    Require(source.Contains("ScoreBallisticCandidate", StringComparison.Ordinal) &&
            source.Contains("string.CompareOrdinal(path, selectedPath)", StringComparison.Ordinal),
        "multiple visible leaves are not resolved by stable cached criteria");
    Require(source.Contains("_ballisticSystem.Emit(emitParams, 1)", StringComparison.Ordinal),
        "ballistic emission no longer counts each emitted particle");
    Require(source.Contains("lights.enabled = false", StringComparison.Ordinal),
        "ballistic spark emitters can retain per-particle lights");
    Require(source.Contains("MaximumBallisticLifetimeSeconds = 0.9f", StringComparison.Ordinal),
        "ballistic particles no longer have a short absolute lifetime cap");
}

static void SparkCleanupReportsAndResetsRaidState()
{
    var disposeSource = ReadEmbeddedSource("Dispose.cs");
    var diagnosticsSource = ReadEmbeddedSource("BallisticSparkDiagnostics.cs");
    var disposeIndex = disposeSource.IndexOf("Singleton<ImpactController>.Instance?.Dispose()", StringComparison.Ordinal);
    var releaseIndex = disposeSource.IndexOf("Singleton<ImpactController>.Release", StringComparison.Ordinal);
    Require(disposeIndex >= 0 && releaseIndex > disposeIndex,
        "spark state is not cleared before ImpactController release");
    Require(diagnosticsSource.Contains("spark-summary attempts=", StringComparison.Ordinal),
        "raid teardown no longer writes the bounded spark summary");
    Require(!diagnosticsSource.Contains("tracerDuplicatePrevented=", StringComparison.Ordinal),
        "diagnostics still claim every tracer prevented a duplicate");
    var sparkEffectsSource = ReadEmbeddedSource("BallisticSparkEffects.cs");
    Require(sparkEffectsSource.Contains("_clusterBudget.Reset()", StringComparison.Ordinal) &&
            sparkEffectsSource.Contains("_impactSequence = 0", StringComparison.Ordinal),
        "cluster or deterministic sequence state is not reset at raid disposal");
}

static void RawEnergyResolvesPointOneGram()
{
    Require(BallisticSparkEnergy.TryCalculateIncomingEnergy(0.1f, 400f, out var energy),
        "valid pellet energy was rejected");
    RequireNear(energy, 8f, 0.0001f, "0.1 g pellet energy");
}

static void RawEnergyResolvesOneGram()
{
    Require(BallisticSparkEnergy.TryCalculateIncomingEnergy(1f, 400f, out var energy),
        "valid 1 g projectile energy was rejected");
    RequireNear(energy, 80f, 0.0001f, "1.0 g projectile energy");
}

static void RawEnergyResolvesThreePointFiveGrams()
{
    Require(BallisticSparkEnergy.TryCalculateIncomingEnergy(3.5f, 400f, out var energy),
        "valid 3.5 g projectile energy was rejected");
    RequireNear(energy, 280f, 0.0001f, "3.5 g projectile energy");
}

static void InvalidRawEnergyInputsFailConservatively()
{
    foreach (var mass in new[] { -1f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        Require(!BallisticSparkEnergy.TryCalculateIncomingEnergy(mass, 400f, out var energy) && energy == 0f,
            $"invalid mass {mass} produced energy");
    }

    foreach (var speed in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        Require(!BallisticSparkEnergy.TryCalculateIncomingEnergy(1f, speed, out var energy) && energy == 0f,
            $"invalid speed {speed} produced energy");
    }

    Require(!BallisticSparkEnergy.TryCalculateIncomingEnergy(float.MaxValue, float.MaxValue, out _),
        "overflowing raw energy was accepted");
}

static void ZeroIncomingEnergyCannotEmit()
{
    Require(BallisticSparkEnergy.TryCalculateIncomingEnergy(1f, 0f, out var energy) && energy == 0f,
        "stationary valid projectile did not resolve to zero energy");
    var plan = CreateSparkPlan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped, energy);
    Require(!plan.ShouldAttemptEmission &&
            plan.RejectionReason == BallisticSparkRejectionReason.NegligibleEnergy,
        "zero incoming energy retained spark emission");
}

static void LowIncomingEnergyGateIsSmoothAndMonotonic()
{
    var previous = BallisticSparkPolicy.ResolveLowEnergyGate(
        BallisticSparkPolicy.NegligibleEnergyThresholdJoules);
    Require(previous == 0f, "low-energy gate did not start at zero");

    for (var energy = BallisticSparkPolicy.NegligibleEnergyThresholdJoules + 0.1f;
         energy <= BallisticSparkPolicy.LowEnergyFullResponseJoules;
         energy += 0.1f)
    {
        var current = BallisticSparkPolicy.ResolveLowEnergyGate(energy);
        Require(current + 0.000001f >= previous, $"low-energy gate fell at {energy:F2} J");
        Require(current - previous < 0.02f, $"low-energy gate jumped at {energy:F2} J");
        previous = current;
    }

    Require(BallisticSparkPolicy.ResolveLowEnergyGate(
        BallisticSparkPolicy.LowEnergyFullResponseJoules) == 1f,
        "low-energy gate did not reach full response");
}

static void SparkContextHasNoLegacyEnergyFallback()
{
    var source = ReadEmbeddedSource("BallisticSparkContextBuilder.cs");
    Require(source.Contains("BallisticSparkEnergy.TryCalculateIncomingEnergy", StringComparison.Ordinal),
        "runtime context does not calculate energy from raw impact data");
    foreach (var forbidden in new[] { "kinetics.Bullet.Energy", "ResolveSparkEnergy", "/ 3.5f", "1620f" })
        Require(!source.Contains(forbidden, StringComparison.Ordinal), $"runtime spark context retained {forbidden}");
    Require(source.Contains("BallisticSparkEnergySource.RawIncomingImpactData", StringComparison.Ordinal),
        "runtime context does not label the raw incoming energy source");
}

static void EightPelletFamilyObeysClusterCaps()
{
    VerifyClusterFamily(8, 0x8100UL, "eight-pellet shell");
}

static void TwelvePelletFamilyObeysClusterCaps()
{
    VerifyClusterFamily(12, 0x1212UL, "twelve-pellet shell");
}

static void IndependentShotsRetainIndependentClusterBudgets()
{
    var budget = new BallisticSparkClusterBudget();
    var first = budget.Consume(true, 0x1111UL, 14, 2f);
    var second = budget.Consume(true, 0x2222UL, 14, 2f);
    Require(first.AllowedParticles == 14 && second.AllowedParticles == 14,
        "independent family keys shared one cluster budget in the same frame");
}

static void FragmentFamilyObeysClusterCaps()
{
    VerifyClusterFamily(16, 0xF12A6UL, "fragment family");
}

static void ClusterWindowExpiresDeterministically()
{
    var budget = new BallisticSparkClusterBudget();
    var key = 0xCAFEUL;
    Require(budget.Consume(true, key, 14, 1f).AllowedParticles == 14, "first cluster event was rejected");
    Require(budget.Consume(true, key, 14, 1.01f).AllowedParticles == 4,
        "second cluster event did not consume the remaining particle allowance");
    Require(budget.Consume(true, key, 14, 1.02f).AllowedParticles == 0,
        "cluster event cap did not suppress the third event");
    Require(budget.Consume(true, key, 14,
                1f + BallisticSparkClusterBudget.ClusterWindowSeconds + 0.001f).AllowedParticles == 14,
        "expired cluster window did not start a new allowance");
}

static void ActiveClusterSurvivesEarlierExpiredSlot()
{
    var budget = new BallisticSparkClusterBudget();
    Require(budget.Consume(true, 0x10UL, 14, 0f).AllowedParticles == 14,
        "first slot setup failed");
    Require(budget.Consume(true, 0x20UL, 14, 1f).AllowedParticles == 14,
        "active family setup failed");
    Require(budget.Consume(true, 0x20UL, 14, 1.01f).AllowedParticles == 4,
        "expired earlier slot hid the active matching family");
}

static void ClusterStorageIsFixedSizeValueData()
{
    var source = ReadEmbeddedSource("BallisticSparkClusterBudget.cs");
    Require(source.Contains("new Slot[SlotCount]", StringComparison.Ordinal),
        "cluster budget does not use a fixed slot array");
    Require(source.Contains("private struct Slot", StringComparison.Ordinal),
        "cluster slot is not value data");
    foreach (var forbidden in new[] { "Dictionary<", "List<", "Shot ", "Player ", "Collider", "Transform" })
        Require(!source.Contains(forbidden, StringComparison.Ordinal), $"cluster budget retains forbidden {forbidden}");
}

static void SparkPrngRepeatsCompleteSampleStream()
{
    var first = BuildDeterministicSparkSample(0xA55AUL, 7U);
    var second = BuildDeterministicSparkSample(0xA55AUL, 7U);
    Require(first.SequenceEqual(second), "identical family and sequence changed the spark sample stream");
}

static void SparkPrngVariesWithFamilyAndSequence()
{
    var baseline = BuildDeterministicSparkSample(0xA55AUL, 7U);
    Require(!baseline.SequenceEqual(BuildDeterministicSparkSample(0xA55BUL, 7U)),
        "different shot family produced the same spark sample stream");
    Require(!baseline.SequenceEqual(BuildDeterministicSparkSample(0xA55AUL, 8U)),
        "different impact sequence produced the same spark sample stream");
}

static void NewSparkPathDoesNotConsumeGlobalRandomness()
{
    var effectsSource = ReadEmbeddedSource("BallisticSparkEffects.cs");
    Require(!effectsSource.Contains("UnityEngine.Random", StringComparison.Ordinal) &&
            !effectsSource.Contains("Random.", StringComparison.Ordinal) &&
            !effectsSource.Contains("System.Random", StringComparison.Ordinal),
        "ballistic spark owner still consumes global or allocated randomness");

    var bundleSource = ReadEmbeddedSource("EffectBundle.cs");
    var ballisticStart = bundleSource.IndexOf("public int EmitBallistic(", StringComparison.Ordinal);
    var ballisticEnd = bundleSource.IndexOf("public bool PrepareBallistic", ballisticStart, StringComparison.Ordinal);
    Require(ballisticStart >= 0 && ballisticEnd > ballisticStart, "ballistic emitter method could not be isolated");
    var ballisticEmitter = bundleSource.Substring(ballisticStart, ballisticEnd - ballisticStart);
    Require(ballisticEmitter.Contains("ref BallisticSparkPrng random", StringComparison.Ordinal) &&
            !ballisticEmitter.Contains("Random.", StringComparison.Ordinal),
        "ballistic emitter does not exclusively use the local PRNG state");
}

static void ShotFamilyKeyUsesVerifiedEftMetadata()
{
    var source = ReadEmbeddedSource("BallisticSparkContextBuilder.cs");
    foreach (var required in new[]
             {
                 "shot.FireIndex", "shot.PlayerProfileID", "shot.Weapon?.Id", "shot.Ammo?.StringTemplateId",
                 "shot.MasterOrigin", "shot.FragmentIndex", "ProjectileCount"
             })
    {
        Require(source.Contains(required, StringComparison.Ordinal), $"shot family key omitted {required}");
    }
    var clusterSource = ReadEmbeddedSource("BallisticSparkClusterBudget.cs");
    Require(source.Contains("FallbackClusterCellMetres", StringComparison.Ordinal) &&
            clusterSource.Contains("ClusterWindowSeconds", StringComparison.Ordinal),
        "invalid family identity has no bounded spatial-temporal fallback");
}

static void SparkDiagnosticsCoverHardeningDecisions()
{
    var source = ReadEmbeddedSource("BallisticSparkDiagnostics.cs");
    foreach (var required in new[]
             {
                 "negligibleEnergyRejected=", "invalidRawEnergy=", "rawEnergyFallback=0", "clusterReduced=",
                 "clusterParticlesRejected=", "clusterEventsRejected=", "selectedProfiles=",
                 "missingOrInvalidParticleLeaves=", "emittedBySurface=", "spark-detail material=",
                 "incomingEnergyJ=", "energySource=", "directionSource=Shot.CurrentDirection",
                 "incomingDirectionCandidate=", "clusterAllowed=", "globalAllowed=", "particleSystem="
             })
    {
        Require(source.Contains(required, StringComparison.Ordinal), $"spark diagnostics omitted {required}");
    }
    Require(source.Contains("if (value < long.MaxValue)", StringComparison.Ordinal),
        "diagnostic counters can wrap instead of saturating");
}

static void ParticleLeafValidationLogsCachedMetadata()
{
    var source = ReadEmbeddedSource("EffectBundle.cs");
    foreach (var required in new[]
             {
                 "ballistic-spark-leaf effect=", "particleSystem=", "path=", "rendererEnabled=",
                 "simulationSpace=", "scalingMode=", "startSize=", "startSpeed=", "startLifetime=",
                 "trails=", "particleLight=", "subEmitters="
             })
    {
        Require(source.Contains(required, StringComparison.Ordinal), $"particle-leaf validation omitted {required}");
    }
    Require(source.Contains("if (_ballisticPrepared)", StringComparison.Ordinal),
        "particle-leaf inspection is not cached at initialization");
}

static void EmissionAxisRetainsFiniteOutwardGuard()
{
    var source = ReadEmbeddedSource("BallisticSparkContextBuilder.cs");
    Require(source.Contains("var normalComponent = Vector3.Dot(axis, context.Normal)", StringComparison.Ordinal) &&
            source.Contains("if (normalComponent < 0f)", StringComparison.Ordinal) &&
            source.Contains("return TryNormalize(axis, out axis) ? axis : context.Normal", StringComparison.Ordinal),
        "final spark axis can become non-finite or point into the struck surface");
}

static void VerifyClusterFamily(int projectileCount, ulong key, string label)
{
    var budget = new BallisticSparkClusterBudget();
    var emittedEvents = 0;
    var emittedParticles = 0;
    var rejectedEvents = 0;
    for (var index = 0; index < projectileCount; index++)
    {
        var allowance = budget.Consume(true, key, 14, 5f + index * 0.001f);
        if (allowance.AllowedParticles > 0)
            emittedEvents++;
        if (allowance.EventRejected)
            rejectedEvents++;
        emittedParticles += allowance.AllowedParticles;
    }

    Require(emittedEvents == BallisticSparkClusterBudget.PerClusterEventCap,
        $"{label} emitted {emittedEvents} visible events");
    Require(emittedParticles == BallisticSparkClusterBudget.PerClusterParticleCap,
        $"{label} emitted {emittedParticles} particles");
    Require(rejectedEvents == projectileCount - BallisticSparkClusterBudget.PerClusterEventCap,
        $"{label} did not gracefully suppress later impacts");
}

static uint[] BuildDeterministicSparkSample(ulong familyKey, uint impactSequence)
{
    var seed = BallisticSparkSeed.Add(BallisticSparkSeed.OffsetBasis, familyKey);
    seed = BallisticSparkSeed.Add(seed, impactSequence);
    var random = new BallisticSparkPrng(seed);
    return
    [
        random.NextUInt(), // eligibility
        (uint)random.NextInt(2, 15), // requested count
        (uint)random.NextInt(0, 4), // bundle emitter
        random.NextUInt(), // cone x
        random.NextUInt(), // cone y
        random.NextUInt(), // cone z
        random.NextUInt(), // speed
        random.NextUInt() // lifetime
    ];
}

static BallisticSparkPlan CreateSparkPlan(
    BallisticSparkSurfaceClass surface,
    BallisticSparkImpactState state,
    float energy = 1600f,
    bool isForwardHit = true)
{
    return BallisticSparkPolicy.CreatePlan(
        surface,
        state,
        energy,
        1f,
        0.75f,
        isForwardHit,
        20f,
        1f,
        BallisticSparkPolicy.DefaultMaximumDistance,
        true);
}

static int CountOccurrences(string source, string value)
{
    var count = 0;
    var index = 0;
    while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
    {
        count++;
        index += value.Length;
    }

    return count;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void Spt414ReleaseMetadataStaysAligned()
{
    Require(ReadEmbeddedSource("New-ReleasePackage.ps1").Contains("$Version = '2.0.17'", StringComparison.Ordinal),
        "package default does not match the plugin release");
    Require(ReadEmbeddedSource("HollywoodFX.csproj").Contains("official SPT 4.1.4 path", StringComparison.Ordinal),
        "build instructions do not target SPT 4.1.4");
    var readme = ReadEmbeddedSource("README.md");
    Require(readme.Contains("official SPT 4.1.4", StringComparison.Ordinal) &&
            readme.Contains("HollywoodFX-2.0.17.zip", StringComparison.Ordinal) &&
            !readme.Contains("SPT 4.1.3", StringComparison.Ordinal),
        "installation or package documentation has a stale release target");
    var bugForm = ReadEmbeddedSource("bug_report.yml");
    Require(bugForm.Contains("placeholder: 4.1.4", StringComparison.Ordinal) &&
            bugForm.Contains("placeholder: 2.0.17", StringComparison.Ordinal),
        "bug report version examples have a stale release target");
}

static string ReadEmbeddedSource(string fileName)
{
    var assembly = typeof(Program).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .SingleOrDefault(name => name.EndsWith("." + fileName, StringComparison.Ordinal));

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
