namespace HollywoodFX.Impact.Sparks;

public enum BallisticSparkRejectionReason
{
    None,
    Disabled,
    InvalidGeometry,
    InvalidRawEnergy,
    NegligibleEnergy,
    Material,
    Distance,
    NoParticles
}

public readonly struct BallisticSparkPlan
{
    public readonly bool ShouldAttemptEmission;
    public readonly float Probability;
    public readonly int MinimumParticles;
    public readonly int MaximumParticles;
    public readonly float SizeMultiplier;
    public readonly float VelocityMultiplier;
    public readonly float LifetimeMultiplier;
    public readonly float SpreadDegrees;
    public readonly float NormalDirectionWeight;
    public readonly float ReflectionDirectionWeight;
    public readonly float TangentDirectionWeight;
    public readonly BallisticSparkVisualProfile VisualProfile;
    public readonly BallisticSparkImpactState ImpactState;
    public readonly BallisticSparkRejectionReason RejectionReason;

    public BallisticSparkPlan(
        bool shouldAttemptEmission,
        float probability,
        int minimumParticles,
        int maximumParticles,
        float sizeMultiplier,
        float velocityMultiplier,
        float lifetimeMultiplier,
        float spreadDegrees,
        float normalDirectionWeight,
        float reflectionDirectionWeight,
        float tangentDirectionWeight,
        BallisticSparkVisualProfile visualProfile,
        BallisticSparkImpactState impactState,
        BallisticSparkRejectionReason rejectionReason)
    {
        ShouldAttemptEmission = shouldAttemptEmission;
        Probability = probability;
        MinimumParticles = minimumParticles;
        MaximumParticles = maximumParticles;
        SizeMultiplier = sizeMultiplier;
        VelocityMultiplier = velocityMultiplier;
        LifetimeMultiplier = lifetimeMultiplier;
        SpreadDegrees = spreadDegrees;
        NormalDirectionWeight = normalDirectionWeight;
        ReflectionDirectionWeight = reflectionDirectionWeight;
        TangentDirectionWeight = tangentDirectionWeight;
        VisualProfile = visualProfile;
        ImpactState = impactState;
        RejectionReason = rejectionReason;
    }

    public static BallisticSparkPlan Reject(
        BallisticSparkImpactState impactState,
        BallisticSparkRejectionReason reason)
    {
        return new BallisticSparkPlan(false, 0f, 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            BallisticSparkVisualProfile.None, impactState, reason);
    }
}
