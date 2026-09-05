using System.Collections.Generic;
using HollywoodFX.Particles;
using UnityEngine;

namespace HollywoodFX.Impact.Sparks;

internal sealed class BallisticSparkEffects
{
    private const int MissingMetalCompact = 1 << 0;
    private const int MissingMineral = 1 << 1;
    private const string MetalEffectKey = "Spray_Sparks_Metal";
    private const string MineralEffectKey = "Spray_Sparks_Light";
    private static int _missingKeysLogged;

    private readonly EffectBundle _metalCompact;
    private readonly EffectBundle _mineralFleck;
    private readonly BallisticSparkBudget _budget = new();
    private readonly BallisticSparkClusterBudget _clusterBudget = new();
    private readonly BallisticSparkDiagnostics _diagnostics;
    private uint _impactSequence;

    public BallisticSparkEffects(Dictionary<string, EffectBundle> mainEffects)
    {
        _diagnostics = new BallisticSparkDiagnostics(Plugin.DebugLoggingEnabled);
        _metalCompact = GetOptional(mainEffects, MetalEffectKey, MissingMetalCompact);
        _mineralFleck = GetOptional(mainEffects, MineralEffectKey, MissingMineral);
        if (_metalCompact != null)
            _diagnostics.RecordMissingOrInvalidParticleLeaves(_metalCompact.PrepareBallistic(MetalEffectKey));
        if (_mineralFleck != null)
            _diagnostics.RecordMissingOrInvalidParticleLeaves(_mineralFleck.PrepareBallistic(MineralEffectKey));
    }

    public void Emit(ImpactKinetics kinetics, bool isTracer)
    {
        _diagnostics.RecordAttempt();

        if (!Plugin.BallisticImpactSparksEnabled.Value)
            return;

        if (!BallisticSparkContextBuilder.TryBuild(kinetics, isTracer, out var context, out var rejectionReason))
        {
            _diagnostics.RecordRejected(rejectionReason);
            return;
        }

        var plan = BallisticSparkPolicy.CreatePlan(
            context.Surface,
            context.ImpactState,
            context.IncomingEnergyJoules,
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

        _diagnostics.RecordEligible(plan);
        var now = Time.unscaledTime;
        var clusterKey = BallisticSparkContextBuilder.ResolveClusterKey(
            context,
            kinetics.Position,
            out var usedFallbackClusterKey);
        var sequence = NextImpactSequence();
        var seed = BallisticSparkContextBuilder.BuildImpactSeed(
            context,
            kinetics.Position,
            clusterKey,
            sequence);
        var random = new BallisticSparkPrng(seed);

        if (random.NextFloat01() >= plan.Probability)
        {
            _diagnostics.RecordProbabilityRejected();
            return;
        }

        var bundle = SelectBundle(plan.VisualProfile, out var effectKey);
        if (bundle == null)
            return;

        var requested = plan.MinimumParticles == plan.MaximumParticles
            ? plan.MaximumParticles
            : random.NextInt(plan.MinimumParticles, plan.MaximumParticles + 1);
        if (requested <= 0)
            return;

        var clusterAllowance = _clusterBudget.Preview(
            context.UsesClusterBudget,
            clusterKey,
            requested,
            now);
        _diagnostics.RecordClusterResult(clusterAllowance,
            context.UsesClusterBudget && usedFallbackClusterKey);
        var clusterAllowed = clusterAllowance.AllowedParticles;
        if (clusterAllowed <= 0)
        {
            var rejectedAxis = BallisticSparkContextBuilder.ResolveEmissionAxis(context, plan);
            _diagnostics.RecordDetail(context, plan, rejectedAxis, requested, 0, 0, 0,
                effectKey, "<cluster-rejected>", "<cluster-rejected>", now);
            return;
        }

        var globallyAllowed = _budget.Consume(clusterAllowed, now, Time.frameCount);
        _diagnostics.RecordBudgetResult(clusterAllowed, globallyAllowed, _budget.CurrentFrameParticles);
        if (globallyAllowed <= 0)
        {
            var rejectedAxis = BallisticSparkContextBuilder.ResolveEmissionAxis(context, plan);
            _diagnostics.RecordDetail(context, plan, rejectedAxis, requested, clusterAllowed, 0, 0,
                effectKey, "<global-budget-rejected>", "<global-budget-rejected>", now);
            return;
        }

        var axis = BallisticSparkContextBuilder.ResolveEmissionAxis(context, plan);
        var physicalScale = Plugin.EffectSize.Value * context.PhysicalSizeScale * plan.SizeMultiplier;
        var emitted = bundle.EmitBallistic(
            kinetics.Position,
            context.Normal,
            axis,
            physicalScale,
            globallyAllowed,
            plan.VelocityMultiplier,
            plan.LifetimeMultiplier,
            plan.SpreadDegrees,
            ref random,
            out var emitterName,
            out var particleSystemName);
        // Charge the family only after submission. Global throttling, a full leaf,
        // or an invalid particle must not spend its two visible events for nothing.
        _clusterBudget.Consume(context.UsesClusterBudget, clusterKey, emitted, now);
        if (emitted <= 0 && particleSystemName == "<none>")
            _diagnostics.RecordMissingOrInvalidParticleLeaves(1);
        _diagnostics.RecordEmission(context.Surface, emitted);
        _diagnostics.RecordDetail(context, plan, axis, requested, clusterAllowed, globallyAllowed, emitted,
            effectKey, emitterName, particleSystemName, now);
    }

    public void Dispose()
    {
        _diagnostics.WriteSummaryAndReset();
        _budget.Reset();
        _clusterBudget.Reset();
        _impactSequence = 0;
    }

    private EffectBundle SelectBundle(BallisticSparkVisualProfile visualProfile, out string effectKey)
    {
        switch (visualProfile)
        {
            case BallisticSparkVisualProfile.MetalCompact:
            case BallisticSparkVisualProfile.MetalRicochet:
                effectKey = MetalEffectKey;
                return _metalCompact;
            case BallisticSparkVisualProfile.MineralFleck:
            case BallisticSparkVisualProfile.ArmorFleck:
                effectKey = MineralEffectKey;
                return _mineralFleck;
            default:
                effectKey = "<none>";
                return null;
        }
    }

    private uint NextImpactSequence()
    {
        unchecked
        {
            _impactSequence++;
            if (_impactSequence == 0)
                _impactSequence++;
            return _impactSequence;
        }
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
