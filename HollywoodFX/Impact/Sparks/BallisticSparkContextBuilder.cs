using EFT.Ballistics;
using UnityEngine;

namespace HollywoodFX.Impact.Sparks;

internal readonly struct BallisticSparkRuntimeContext
{
    public readonly BallisticSparkSurfaceClass Surface;
    public readonly BallisticSparkImpactState ImpactState;
    public readonly float KineticEnergy;
    public readonly float ChanceScale;
    public readonly float NormalIncidenceCosine;
    public readonly bool IsForwardHit;
    public readonly float Distance;
    public readonly bool IsTracer;
    public readonly Vector3 Normal;
    public readonly Vector3 Reflection;
    public readonly Vector3 Tangent;

    public BallisticSparkRuntimeContext(
        BallisticSparkSurfaceClass surface,
        BallisticSparkImpactState impactState,
        float kineticEnergy,
        float chanceScale,
        float normalIncidenceCosine,
        bool isForwardHit,
        float distance,
        bool isTracer,
        Vector3 normal,
        Vector3 reflection,
        Vector3 tangent)
    {
        Surface = surface;
        ImpactState = impactState;
        KineticEnergy = kineticEnergy;
        ChanceScale = chanceScale;
        NormalIncidenceCosine = normalIncidenceCosine;
        IsForwardHit = isForwardHit;
        Distance = distance;
        IsTracer = isTracer;
        Normal = normal;
        Reflection = reflection;
        Tangent = tangent;
    }
}

internal static class BallisticSparkContextBuilder
{
    private const float MinimumVectorSqrMagnitude = 0.000001f;

    public static bool TryBuild(
        ImpactKinetics kinetics,
        bool isTracer,
        out BallisticSparkRuntimeContext context)
    {
        context = default;
        var shot = kinetics?.Bullet?.Info;
        if (shot == null || !IsFinite(kinetics.Bullet.Energy) || !IsFinite(kinetics.Bullet.ChanceScale) ||
            !IsFinite(kinetics.DistanceToImpact) || !IsFinite(kinetics.Normal))
        {
            return false;
        }

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
        var sparkEnergy = ResolveSparkEnergy(kinetics.Bullet.Energy, shot.BulletMassGram);
        context = new BallisticSparkRuntimeContext(
            ClassifySurface(kinetics.Material),
            ClassifyImpactState(shot, kinetics.Bullet.Penetrated),
            sparkEnergy,
            kinetics.Bullet.ChanceScale,
            normalCosine,
            shot.IsForwardHit,
            kinetics.DistanceToImpact,
            isTracer,
            normal,
            reflection,
            tangent);
        return true;
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

    private static float ResolveSparkEnergy(float kineticEnergy, float projectileMassGram)
    {
        if (!IsFinite(projectileMassGram) || projectileMassGram >= 3.5f)
            return Mathf.Max(0f, kineticEnergy);

        // BulletKinetics intentionally floors projectile mass for legacy HFX sizing. Undo only that visual floor here
        // so each buckshot pellet cannot request a rifle-sized spark shower.
        var pelletScale = Mathf.Clamp(projectileMassGram / 3.5f, 0.1f, 1f);
        return Mathf.Max(0f, kineticEnergy * pelletScale);
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
