using System;

namespace HollywoodFX.Impact.Sparks;

public static class BallisticSparkPolicy
{
    public const int PerImpactParticleCap = 24;
    public const float DefaultMaximumDistance = 140f;
    public const float NegligibleEnergyThresholdJoules = 0.25f;
    public const float LowEnergyFullResponseJoules = 12f;

    public static BallisticSparkPlan CreatePlan(
        BallisticSparkSurfaceClass surface,
        BallisticSparkImpactState impactState,
        float kineticEnergyJoules,
        float chanceScale,
        float normalIncidenceCosine,
        bool isForwardHit,
        float distanceMetres,
        float intensity,
        float maximumDistance,
        bool geometryIsValid)
    {
        if (!geometryIsValid || !IsFinite(chanceScale) || !IsFinite(normalIncidenceCosine) ||
            !IsFinite(distanceMetres))
        {
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.InvalidGeometry);
        }

        if (!IsFinite(kineticEnergyJoules) || kineticEnergyJoules < 0f)
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.InvalidRawEnergy);

        if (kineticEnergyJoules <= NegligibleEnergyThresholdJoules)
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.NegligibleEnergy);

        if (!IsFinite(intensity) || intensity <= 0f)
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.Disabled);

        if (!BallisticSparkProfile.TryResolve(surface, out var profile))
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.Material);

        var distanceAttenuation = ResolveDistanceAttenuation(distanceMetres, maximumDistance);
        if (distanceAttenuation <= 0f)
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.Distance);

        var energy = kineticEnergyJoules;
        var energyResponse = (float)Math.Sqrt(energy / (energy + 700f));
        var lowEnergyGate = ResolveLowEnergyGate(energy);
        var energyCountScale = (0.25f + 0.75f * energyResponse) * lowEnergyGate;
        var boundedChanceScale = Clamp(chanceScale, 0.4f, 1.35f);
        var boundedIntensity = Clamp(intensity, 0f, 2f);
        var grazingAmount = 1f - Clamp(normalIncidenceCosine, 0f, 1f);

        ResolveImpactState(
            impactState,
            isForwardHit,
            grazingAmount,
            out var probabilityScale,
            out var countScale,
            out var velocityScale,
            out var lifetimeScale,
            out var spreadAddition,
            out var normalWeight,
            out var reflectionWeight,
            out var tangentWeight);

        var angleProbabilityScale = impactState == BallisticSparkImpactState.Ricochet
            ? 0.65f + 0.35f * grazingAmount
            : 0.85f + 0.15f * (1f - grazingAmount);
        var probability = Lerp(profile.MinimumProbability, profile.MaximumProbability, energyResponse) *
                          boundedChanceScale * boundedIntensity * distanceAttenuation *
                          probabilityScale * angleProbabilityScale * lowEnergyGate;
        probability = Clamp(probability, 0f, 0.95f);

        var countIntensity = boundedIntensity <= 1f
            ? boundedIntensity
            : 1f + 0.5f * (boundedIntensity - 1f);
        var maximumParticles = (int)Math.Floor(
            profile.MaximumParticles * energyCountScale * countScale * countIntensity *
            (float)Math.Sqrt(boundedChanceScale) * distanceAttenuation + 0.0001f);

        var stateCap = ResolveStateCap(surface, impactState);
        maximumParticles = Clamp(maximumParticles, 0, Math.Min(stateCap, PerImpactParticleCap));
        if (maximumParticles <= 0 || probability <= 0f)
            return BallisticSparkPlan.Reject(impactState, BallisticSparkRejectionReason.NoParticles);

        var minimumParticles = impactState == BallisticSparkImpactState.PenetrationExit ||
                               distanceAttenuation < 0.35f || boundedIntensity < 0.5f
            ? 0
            : Math.Min(profile.MinimumParticles, maximumParticles);

        var visualProfile = impactState == BallisticSparkImpactState.Ricochet &&
                            surface is BallisticSparkSurfaceClass.PrimaryMetal or
                                BallisticSparkSurfaceClass.HelmetRicochet
            ? BallisticSparkVisualProfile.MetalRicochet
            : profile.VisualProfile;

        var sizeMultiplier = profile.SizeMultiplier * (0.82f + 0.18f * energyResponse);
        var spread = Clamp(profile.SpreadDegrees + spreadAddition + 14f * grazingAmount, 8f, 55f);

        return new BallisticSparkPlan(
            true,
            probability,
            minimumParticles,
            maximumParticles,
            sizeMultiplier,
            profile.VelocityMultiplier * velocityScale,
            profile.LifetimeMultiplier * lifetimeScale,
            spread,
            normalWeight,
            reflectionWeight,
            tangentWeight,
            visualProfile,
            impactState,
            BallisticSparkRejectionReason.None);
    }

    public static float ResolveDistanceAttenuation(float distanceMetres, float maximumDistance)
    {
        if (!IsFinite(distanceMetres) || !IsFinite(maximumDistance) || maximumDistance <= 0f)
            return 0f;

        var distance = Math.Max(0f, distanceMetres);
        if (distance >= maximumDistance)
            return 0f;

        var normalizedDistance = distance / maximumDistance;
        if (normalizedDistance <= 0.2f)
            return 1f;

        var transition = Clamp((normalizedDistance - 0.2f) / 0.8f, 0f, 1f);
        var smoothStep = transition * transition * (3f - 2f * transition);
        return 1f - smoothStep;
    }

    public static float ResolveLowEnergyGate(float incomingEnergyJoules)
    {
        if (!IsFinite(incomingEnergyJoules) || incomingEnergyJoules <= NegligibleEnergyThresholdJoules)
            return 0f;
        if (incomingEnergyJoules >= LowEnergyFullResponseJoules)
            return 1f;

        var transition = Clamp(
            (incomingEnergyJoules - NegligibleEnergyThresholdJoules) /
            (LowEnergyFullResponseJoules - NegligibleEnergyThresholdJoules),
            0f,
            1f);
        return transition * transition * (3f - 2f * transition);
    }

    private static int ResolveStateCap(
        BallisticSparkSurfaceClass surface,
        BallisticSparkImpactState impactState)
    {
        if (impactState == BallisticSparkImpactState.PenetrationExit)
        {
            return surface switch
            {
                BallisticSparkSurfaceClass.PrimaryMetal => 3,
                BallisticSparkSurfaceClass.HelmetRicochet => 3,
                _ => 1
            };
        }

        if (impactState == BallisticSparkImpactState.Ricochet)
        {
            return surface is BallisticSparkSurfaceClass.PrimaryMetal or
                BallisticSparkSurfaceClass.HelmetRicochet ? 20 : 6;
        }

        return surface switch
        {
            BallisticSparkSurfaceClass.PrimaryMetal => 14,
            BallisticSparkSurfaceClass.SecondaryMineral => 5,
            BallisticSparkSurfaceClass.LowMineral => 3,
            BallisticSparkSurfaceClass.BodyArmor => 4,
            BallisticSparkSurfaceClass.Helmet => 5,
            BallisticSparkSurfaceClass.HelmetRicochet => 14,
            _ => 0
        };
    }

    private static void ResolveImpactState(
        BallisticSparkImpactState impactState,
        bool isForwardHit,
        float grazingAmount,
        out float probabilityScale,
        out float countScale,
        out float velocityScale,
        out float lifetimeScale,
        out float spreadAddition,
        out float normalWeight,
        out float reflectionWeight,
        out float tangentWeight)
    {
        switch (impactState)
        {
            case BallisticSparkImpactState.Ricochet:
                probabilityScale = 1.2f;
                countScale = 1.35f;
                velocityScale = 1.16f;
                lifetimeScale = 1.18f;
                spreadAddition = 6f;
                normalWeight = 0.12f;
                reflectionWeight = 0.46f;
                tangentWeight = 0.42f;
                return;
            case BallisticSparkImpactState.Stopped:
                probabilityScale = 1f;
                countScale = 1f;
                velocityScale = 1f;
                lifetimeScale = 1f;
                spreadAddition = 10f * grazingAmount;
                normalWeight = 0.56f;
                reflectionWeight = 0.12f;
                tangentWeight = 0.32f;
                return;
            case BallisticSparkImpactState.PenetrationEntry:
                probabilityScale = 0.62f;
                countScale = 0.65f;
                velocityScale = 0.88f;
                lifetimeScale = 0.78f;
                spreadAddition = 0f;
                normalWeight = 0.68f;
                reflectionWeight = 0.08f;
                tangentWeight = 0.24f;
                return;
            case BallisticSparkImpactState.PenetrationExit:
                probabilityScale = 0.14f;
                countScale = 0.15f;
                velocityScale = 0.72f;
                lifetimeScale = 0.62f;
                spreadAddition = -2f;
                normalWeight = isForwardHit ? 0.7f : 0.82f;
                reflectionWeight = 0.03f;
                tangentWeight = isForwardHit ? 0.27f : 0.15f;
                return;
            default:
                probabilityScale = 0.25f;
                countScale = 0.35f;
                velocityScale = 0.72f;
                lifetimeScale = 0.65f;
                spreadAddition = 0f;
                normalWeight = 0.7f;
                reflectionWeight = 0.05f;
                tangentWeight = 0.25f;
                return;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
