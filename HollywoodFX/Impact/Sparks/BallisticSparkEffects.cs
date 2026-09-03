using System.Collections.Generic;
using HollywoodFX.Particles;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Impact.Sparks;

internal sealed class BallisticSparkEffects
{
    private const int MissingMetalCompact = 1 << 0;
    private const int MissingMineral = 1 << 1;
    private static int _missingKeysLogged;

    private readonly EffectBundle _metalCompact;
    private readonly EffectBundle _mineralFleck;
    private readonly BallisticSparkBudget _budget = new();
    private readonly BallisticSparkDiagnostics _diagnostics;

    public BallisticSparkEffects(
        Dictionary<string, EffectBundle> mainEffects)
    {
        _metalCompact = GetOptional(mainEffects, "Spray_Sparks_Metal", MissingMetalCompact);
        _mineralFleck = GetOptional(mainEffects, "Spray_Sparks_Light", MissingMineral);
        _metalCompact?.PrepareBallistic();
        _mineralFleck?.PrepareBallistic();
        _diagnostics = new BallisticSparkDiagnostics(Plugin.DebugLoggingEnabled);
    }

    public void Emit(ImpactKinetics kinetics, bool isTracer)
    {
        _diagnostics.RecordAttempt();

        if (!Plugin.BallisticImpactSparksEnabled.Value)
            return;

        if (!BallisticSparkContextBuilder.TryBuild(kinetics, isTracer, out var context))
        {
            _diagnostics.RecordRejected(BallisticSparkRejectionReason.InvalidGeometry);
            return;
        }

        var plan = BallisticSparkPolicy.CreatePlan(
            context.Surface,
            context.ImpactState,
            context.KineticEnergy,
            context.ChanceScale,
            context.NormalIncidenceCosine,
            context.IsForwardHit,
            context.Distance,
            Plugin.BallisticImpactSparkIntensity.Value,
            Plugin.BallisticImpactSparkMaximumDistance.Value,
            geometryIsValid: true);

        if (!plan.ShouldAttemptEmission)
        {
            _diagnostics.RecordRejected(plan.RejectionReason);
            return;
        }

        _diagnostics.RecordEligible(plan, context.IsTracer);
        if (Random.value >= plan.Probability)
        {
            _diagnostics.RecordProbabilityRejected();
            return;
        }

        var bundle = SelectBundle(plan.VisualProfile);
        if (bundle == null)
            return;

        var requested = plan.MinimumParticles == plan.MaximumParticles
            ? plan.MaximumParticles
            : Random.Range(plan.MinimumParticles, plan.MaximumParticles + 1);
        if (requested <= 0)
            return;

        var allowed = _budget.Consume(requested, Time.unscaledTime, Time.frameCount);
        _diagnostics.RecordBudgetResult(requested, allowed, _budget.CurrentFrameParticles);
        if (allowed <= 0)
            return;

        var axis = BallisticSparkContextBuilder.ResolveEmissionAxis(context, plan);
        var physicalScale = Plugin.EffectSize.Value * kinetics.Bullet.SizeScale * plan.SizeMultiplier;
        var emitted = bundle.EmitBallistic(
            kinetics.Position,
            context.Normal,
            axis,
            physicalScale,
            allowed,
            plan.VelocityMultiplier,
            plan.LifetimeMultiplier,
            plan.SpreadDegrees);
        _diagnostics.RecordEmission(emitted);
    }

    public void Dispose()
    {
        _diagnostics.WriteSummaryAndReset();
        _budget.Reset();
    }

    private EffectBundle SelectBundle(BallisticSparkVisualProfile visualProfile)
    {
        return visualProfile switch
        {
            BallisticSparkVisualProfile.MetalCompact => _metalCompact,
            BallisticSparkVisualProfile.MetalRicochet => _metalCompact,
            BallisticSparkVisualProfile.MineralFleck => _mineralFleck,
            BallisticSparkVisualProfile.ArmorFleck => _mineralFleck,
            _ => null
        };
    }

    private static EffectBundle GetOptional(
        Dictionary<string, EffectBundle> effects,
        string key,
        int warningBit)
    {
        if (effects != null && effects.TryGetValue(key, out var bundle))
            return bundle;

        if ((_missingKeysLogged & warningBit) == 0)
        {
            _missingKeysLogged |= warningBit;
            Plugin.Log.LogWarning($"Ballistic impact sparks disabled one visual profile because effect key '{key}' is missing.");
        }

        return null;
    }
}
