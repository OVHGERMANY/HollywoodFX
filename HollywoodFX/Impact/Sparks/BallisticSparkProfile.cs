namespace HollywoodFX.Impact.Sparks;

public enum BallisticSparkVisualProfile
{
    None,
    MetalCompact,
    MetalRicochet,
    MineralFleck,
    ArmorFleck
}

public readonly struct BallisticSparkProfile
{
    public readonly float MinimumProbability;
    public readonly float MaximumProbability;
    public readonly int MinimumParticles;
    public readonly int MaximumParticles;
    public readonly float SizeMultiplier;
    public readonly float VelocityMultiplier;
    public readonly float LifetimeMultiplier;
    public readonly float SpreadDegrees;
    public readonly BallisticSparkVisualProfile VisualProfile;

    public BallisticSparkProfile(
        float minimumProbability,
        float maximumProbability,
        int minimumParticles,
        int maximumParticles,
        float sizeMultiplier,
        float velocityMultiplier,
        float lifetimeMultiplier,
        float spreadDegrees,
        BallisticSparkVisualProfile visualProfile)
    {
        MinimumProbability = minimumProbability;
        MaximumProbability = maximumProbability;
        MinimumParticles = minimumParticles;
        MaximumParticles = maximumParticles;
        SizeMultiplier = sizeMultiplier;
        VelocityMultiplier = velocityMultiplier;
        LifetimeMultiplier = lifetimeMultiplier;
        SpreadDegrees = spreadDegrees;
        VisualProfile = visualProfile;
    }

    public static bool TryResolve(BallisticSparkSurfaceClass surface, out BallisticSparkProfile profile)
    {
        switch (surface)
        {
            case BallisticSparkSurfaceClass.PrimaryMetal:
                profile = new BallisticSparkProfile(0.18f, 0.72f, 2, 14, 0.82f, 1f, 0.82f, 22f,
                    BallisticSparkVisualProfile.MetalCompact);
                return true;
            case BallisticSparkSurfaceClass.SecondaryMineral:
                profile = new BallisticSparkProfile(0.03f, 0.22f, 0, 5, 0.55f, 0.68f, 0.52f, 18f,
                    BallisticSparkVisualProfile.MineralFleck);
                return true;
            case BallisticSparkSurfaceClass.LowMineral:
                profile = new BallisticSparkProfile(0.01f, 0.08f, 0, 3, 0.45f, 0.55f, 0.42f, 14f,
                    BallisticSparkVisualProfile.MineralFleck);
                return true;
            case BallisticSparkSurfaceClass.BodyArmor:
                profile = new BallisticSparkProfile(0.02f, 0.15f, 0, 4, 0.58f, 0.72f, 0.58f, 17f,
                    BallisticSparkVisualProfile.ArmorFleck);
                return true;
            case BallisticSparkSurfaceClass.Helmet:
                profile = new BallisticSparkProfile(0.04f, 0.21f, 0, 5, 0.62f, 0.78f, 0.64f, 18f,
                    BallisticSparkVisualProfile.ArmorFleck);
                return true;
            case BallisticSparkSurfaceClass.HelmetRicochet:
                profile = new BallisticSparkProfile(0.24f, 0.68f, 3, 14, 0.72f, 1.08f, 0.92f, 28f,
                    BallisticSparkVisualProfile.MetalRicochet);
                return true;
            default:
                profile = default;
                return false;
        }
    }
}
