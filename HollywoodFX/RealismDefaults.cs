namespace HollywoodFX;

// Restrained presentation defaults, not measured material or weapon constants.
// Config.Bind still owns each value: existing saved settings and controls remain intact.
internal static class RealismDefaults
{
    public const float SparkIntensity = 0.7f;
    public const float ImpactSize = 0.65f;
    public const float FireballDensity = 0.5f;
    public const float ExplosionSparkDensity = 0.5f;
    public const float MuzzleJetSize = 0.8f;
    public const float MuzzleSparkSize = 0.65f;
    public const float MuzzleSparkEmission = 0.5f;
    public const float MuzzleSmokeSize = 0.85f;
    public const float MuzzleSmokeEmission = 0.8f;
    public const float ConcussionDuration = 0.75f;
    public const bool SuppressionEnabled = false;
    public const float BattleBlurIntensity = 0.35f;
    public const float AmbientEmission = 0.7f;
    public const bool CinematicRagdolls = false;
    public const float ShellSize = 1f;
    public const float ShellVelocity = 1f;
}
