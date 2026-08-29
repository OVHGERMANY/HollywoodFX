namespace HollywoodFX.Gore;

public readonly struct BloodRenderOwnership
{
    internal BloodRenderOwnership(bool traumaCoreLoaded)
    {
        TraumaCoreLoaded = traumaCoreLoaded;
        AllowTransientImpactPuffsAndSprays = !traumaCoreLoaded;
        AllowBodyWoundTextureDecals = true;
        AllowBodyWoundTextureEmission = !traumaCoreLoaded;
        AllowImpactSquirts = !traumaCoreLoaded;
        AllowDeathBloodEffects = !traumaCoreLoaded;
        AllowParticleCollisionEnvironmentDeposits = !traumaCoreLoaded;
        AllowEnvironmentDecalOverrides = !traumaCoreLoaded;
    }

    public bool TraumaCoreLoaded { get; }
    public bool AllowTransientImpactPuffsAndSprays { get; }
    public bool AllowBodyWoundTextureDecals { get; }
    public bool AllowBodyWoundTextureEmission { get; }
    public bool AllowImpactSquirts { get; }
    public bool AllowDeathBloodEffects { get; }
    public bool AllowParticleCollisionEnvironmentDeposits { get; }
    public bool AllowEnvironmentDecalOverrides { get; }
}

public static class BloodRenderOwnershipPolicy
{
    public const string TraumaCorePluginGuid = "com.hysocs.traumacore";

    public static BloodRenderOwnership Resolve(bool traumaCoreLoaded)
    {
        return new BloodRenderOwnership(traumaCoreLoaded);
    }
}
