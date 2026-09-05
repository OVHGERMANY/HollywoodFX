using System;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace HollywoodFX.Impact.Sparks;

internal readonly struct BallisticSparkRuntimeContext
{
    public readonly MaterialType Material;
    public readonly BallisticSparkSurfaceClass Surface;
    public readonly BallisticSparkImpactState ImpactState;
    public readonly Shot.EBulletState EftBulletState;
    public readonly float ProjectileMassGram;
    public readonly float SpeedMetresPerSecond;
    public readonly float IncomingEnergyJoules;
    public readonly BallisticSparkEnergySource EnergySource;
    public readonly float ChanceScale;
    public readonly float PhysicalSizeScale;
    public readonly float NormalIncidenceCosine;
    public readonly bool IsForwardHit;
    public readonly float Distance;
    public readonly bool IsTracer;
    public readonly bool UsesClusterBudget;
    public readonly bool HasStableShotFamily;
    public readonly ulong ShotFamilyKey;
    public readonly ulong ShooterHash;
    public readonly ulong ProjectileClassHash;
    public readonly Vector3 ShotCurrentDirection;
    public readonly Vector3 Normal;
    public readonly Vector3 Reflection;
    public readonly Vector3 Tangent;

    public BallisticSparkRuntimeContext(
        MaterialType material,
        BallisticSparkSurfaceClass surface,
        BallisticSparkImpactState impactState,
        Shot.EBulletState eftBulletState,
        float projectileMassGram,
        float speedMetresPerSecond,
        float incomingEnergyJoules,
        BallisticSparkEnergySource energySource,
        float chanceScale,
        float physicalSizeScale,
        float normalIncidenceCosine,
        bool isForwardHit,
        float distance,
        bool isTracer,
        bool usesClusterBudget,
        bool hasStableShotFamily,
        ulong shotFamilyKey,
        ulong shooterHash,
        ulong projectileClassHash,
        Vector3 shotCurrentDirection,
        Vector3 normal,
        Vector3 reflection,
        Vector3 tangent)
    {
        Material = material;
        Surface = surface;
        ImpactState = impactState;
        EftBulletState = eftBulletState;
        ProjectileMassGram = projectileMassGram;
        SpeedMetresPerSecond = speedMetresPerSecond;
        IncomingEnergyJoules = incomingEnergyJoules;
        EnergySource = energySource;
        ChanceScale = chanceScale;
        PhysicalSizeScale = physicalSizeScale;
        NormalIncidenceCosine = normalIncidenceCosine;
        IsForwardHit = isForwardHit;
        Distance = distance;
        IsTracer = isTracer;
        UsesClusterBudget = usesClusterBudget;
        HasStableShotFamily = hasStableShotFamily;
        ShotFamilyKey = shotFamilyKey;
        ShooterHash = shooterHash;
        ProjectileClassHash = projectileClassHash;
        ShotCurrentDirection = shotCurrentDirection;
        Normal = normal;
        Reflection = reflection;
        Tangent = tangent;
    }
}

internal static class BallisticSparkContextBuilder
{
    private const float MinimumVectorSqrMagnitude = 0.000001f;
    private const float SizeNormFactor = 2000f;
    private const float FallbackClusterCellMetres = 0.75f;

    public static bool TryBuild(
        ImpactKinetics kinetics,
        bool isTracer,
        out BallisticSparkRuntimeContext context,
        out BallisticSparkRejectionReason rejectionReason)
    {
        context = default;
        rejectionReason = BallisticSparkRejectionReason.InvalidGeometry;
        var shot = kinetics?.Bullet?.Info;
        if (shot == null)
        {
            rejectionReason = BallisticSparkRejectionReason.InvalidRawEnergy;
            return false;
        }

        var projectileMassGram = shot.BulletMassGram;
        var speedMetresPerSecond = shot.VelocityMagnitude;
        if (!BallisticSparkEnergy.TryCalculateIncomingEnergy(
                projectileMassGram,
                speedMetresPerSecond,
                out var incomingEnergyJoules))
        {
            rejectionReason = BallisticSparkRejectionReason.InvalidRawEnergy;
            return false;
        }

        if (!IsFinite(kinetics.DistanceToImpact) || !IsFinite(kinetics.Position) || !IsFinite(kinetics.Normal))
            return false;

        var incoming = shot.CurrentDirection;
        var normal = kinetics.Normal;
        if (!TryNormalize(incoming, out incoming) || !TryNormalize(normal, out normal))
            return false;

        var reflection = Vector3.Reflect(incoming, normal);
        if (!TryNormalize(reflection, out reflection))
            return false;

        var tangent = Vector3.ProjectOnPlane(incoming, normal);
        if (!TryNormalize(tangent, out tangent))
        {
            var fallbackReference = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(fallbackReference, normal);
            if (!TryNormalize(tangent, out tangent))
                return false;
        }

        var normalCosine = Mathf.Clamp01(Vector3.Dot(-incoming, normal));
        var physicalSizeScale = Mathf.Clamp(Mathf.Sqrt(incomingEnergyJoules / SizeNormFactor), 0.75f, 1.25f);
        var chanceScale = physicalSizeScale < 1f ? physicalSizeScale : physicalSizeScale * physicalSizeScale;
        var shooterHash = BallisticSparkSeed.Add(BallisticSparkSeed.OffsetBasis, shot.PlayerProfileID);
        var projectileClassHash = BallisticSparkSeed.Add(
            BallisticSparkSeed.OffsetBasis,
            shot.Ammo?.StringTemplateId);
        var hasStableShotFamily = TryBuildStableShotFamilyKey(
            shot,
            shooterHash,
            projectileClassHash,
            out var shotFamilyKey);
        var isMultiProjectile = shot.Ammo is Ammo { ProjectileCount: > 1 };
        var isFragment = shot.Parent != null || shot.FragmentIndex > 0 ||
                         shot.BulletState == Shot.EBulletState.FragmentationHit;

        context = new BallisticSparkRuntimeContext(
            kinetics.Material,
            ClassifySurface(kinetics.Material),
            ClassifyImpactState(shot, kinetics.Bullet.Penetrated),
            shot.BulletState,
            projectileMassGram,
            speedMetresPerSecond,
            incomingEnergyJoules,
            BallisticSparkEnergySource.RawIncomingImpactData,
            chanceScale,
            physicalSizeScale,
            normalCosine,
            shot.IsForwardHit,
            kinetics.DistanceToImpact,
            isTracer,
            isMultiProjectile || isFragment,
            hasStableShotFamily,
            shotFamilyKey,
            shooterHash,
            projectileClassHash,
            incoming,
            normal,
            reflection,
            tangent);
        rejectionReason = BallisticSparkRejectionReason.None;
        return true;
    }

    public static ulong ResolveClusterKey(
        in BallisticSparkRuntimeContext context,
        Vector3 impactPosition,
        out bool usedFallback)
    {
        if (context.HasStableShotFamily)
        {
            usedFallback = false;
            return context.ShotFamilyKey;
        }

        usedFallback = true;
        // Exact EFT 4.1.3 normally supplies FireIndex. If it is malformed, cluster only
        // nearby contacts sharing shooter/projectile/surface values; the fixed budget's
        // 180 ms window supplies the temporal half of this fallback.
        var seed = BallisticSparkSeed.OffsetBasis;
        seed = BallisticSparkSeed.Add(seed, context.ShooterHash);
        seed = BallisticSparkSeed.Add(seed, context.ProjectileClassHash);
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.x, 1f / FallbackClusterCellMetres));
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.y, 1f / FallbackClusterCellMetres));
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.z, 1f / FallbackClusterCellMetres));
        seed = BallisticSparkSeed.Add(seed, (int)context.Surface);
        return seed == 0UL ? 1UL : seed;
    }

    public static ulong BuildImpactSeed(
        in BallisticSparkRuntimeContext context,
        Vector3 impactPosition,
        ulong clusterKey,
        uint impactSequence)
    {
        var seed = BallisticSparkSeed.OffsetBasis;
        seed = BallisticSparkSeed.Add(seed, clusterKey);
        seed = BallisticSparkSeed.Add(seed, impactSequence);
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.x, 100f));
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.y, 100f));
        seed = BallisticSparkSeed.Add(seed, Quantize(impactPosition.z, 100f));
        seed = BallisticSparkSeed.Add(seed, (int)context.Material);
        seed = BallisticSparkSeed.Add(seed, (int)context.ImpactState);
        seed = BallisticSparkSeed.Add(seed, context.IsForwardHit ? 1 : 0);
        return seed == 0UL ? 1UL : seed;
    }

    public static Vector3 ResolveEmissionAxis(
        in BallisticSparkRuntimeContext context,
        in BallisticSparkPlan plan)
    {
        var axis = context.Normal * plan.NormalDirectionWeight +
                   context.Reflection * plan.ReflectionDirectionWeight +
                   context.Tangent * plan.TangentDirectionWeight;
        var normalComponent = Vector3.Dot(axis, context.Normal);
        if (normalComponent < 0f)
            axis -= context.Normal * normalComponent;

        return TryNormalize(axis, out axis) ? axis : context.Normal;
    }

    internal static BallisticSparkSurfaceClass ClassifySurface(MaterialType material)
    {
        return material switch
        {
            MaterialType.Chainfence or MaterialType.GarbageMetal or MaterialType.Grate or
                MaterialType.MetalThin or MaterialType.MetalThick or MaterialType.MetalNoDecal =>
                BallisticSparkSurfaceClass.PrimaryMetal,
            MaterialType.Concrete or MaterialType.Stone or MaterialType.Tile or MaterialType.GenericHard =>
                BallisticSparkSurfaceClass.SecondaryMineral,
            MaterialType.Asphalt or MaterialType.Gravel or MaterialType.Pebbles =>
                BallisticSparkSurfaceClass.LowMineral,
            MaterialType.BodyArmor => BallisticSparkSurfaceClass.BodyArmor,
            MaterialType.Helmet => BallisticSparkSurfaceClass.Helmet,
            MaterialType.HelmetRicochet => BallisticSparkSurfaceClass.HelmetRicochet,
            MaterialType.Body or MaterialType.Fabric or MaterialType.Cardboard or MaterialType.GarbagePaper or
                MaterialType.GenericSoft or MaterialType.GrassHigh or MaterialType.GrassLow or MaterialType.Mud or
                MaterialType.Soil or MaterialType.SoilForest or MaterialType.WoodThin or MaterialType.WoodThick or
                MaterialType.Tyre or MaterialType.Rubber or MaterialType.Plastic or MaterialType.Glass or
                MaterialType.GlassShattered or MaterialType.GlassVisor => BallisticSparkSurfaceClass.Prohibited,
            _ => BallisticSparkSurfaceClass.Unknown
        };
    }

    private static BallisticSparkImpactState ClassifyImpactState(Shot shot, bool penetrated)
    {
        if (shot.BulletState == Shot.EBulletState.RicochetHit)
            return BallisticSparkImpactState.Ricochet;
        if (!shot.IsForwardHit)
            return BallisticSparkImpactState.PenetrationExit;
        if (shot.BulletState == Shot.EBulletState.StopHit)
            return BallisticSparkImpactState.Stopped;
        if (penetrated && shot.BulletState is Shot.EBulletState.Flying or Shot.EBulletState.DeviationHit or
            Shot.EBulletState.FragmentationHit)
        {
            return BallisticSparkImpactState.PenetrationEntry;
        }

        return BallisticSparkImpactState.Unknown;
    }

    private static bool TryBuildStableShotFamilyKey(
        Shot shot,
        ulong shooterHash,
        ulong projectileClassHash,
        out ulong key)
    {
        key = 0UL;
        if (shot.FireIndex < 0 || !IsFinite(shot.MasterOrigin))
            return false;

        // BallisticsCalculator assigns one FireIndex to every shotgun pellet and copies it
        // into child fragments. It increments FireIndex for the next independent shot.
        var seed = BallisticSparkSeed.OffsetBasis;
        seed = BallisticSparkSeed.Add(seed, shooterHash);
        seed = BallisticSparkSeed.Add(seed, shot.FireIndex);
        seed = BallisticSparkSeed.Add(seed, shot.Weapon?.Id);
        seed = BallisticSparkSeed.Add(seed, projectileClassHash);
        seed = BallisticSparkSeed.Add(seed, Quantize(shot.MasterOrigin.x, 100f));
        seed = BallisticSparkSeed.Add(seed, Quantize(shot.MasterOrigin.y, 100f));
        seed = BallisticSparkSeed.Add(seed, Quantize(shot.MasterOrigin.z, 100f));
        key = seed == 0UL ? 1UL : seed;
        return true;
    }

    private static int Quantize(float value, float scale)
    {
        if (!IsFinite(value) || !IsFinite(scale))
            return 0;

        var scaled = Math.Round((double)value * scale, MidpointRounding.AwayFromZero);
        if (scaled >= int.MaxValue)
            return int.MaxValue;
        if (scaled <= int.MinValue)
            return int.MinValue;
        return (int)scaled;
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        if (!IsFinite(value) || value.sqrMagnitude < MinimumVectorSqrMagnitude)
        {
            normalized = default;
            return false;
        }

        normalized = value.normalized;
        return IsFinite(normalized);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
