using System;
using System.Text;
using UnityEngine;

namespace HollywoodFX.Impact.Sparks;

internal sealed class BallisticSparkDiagnostics
{
    private const float DetailIntervalSeconds = 1f;
    private const int SurfaceCount = 8;
    private const int ProfileCount = 5;

    private readonly bool _enabled;
    private readonly long[] _selectedProfiles = new long[ProfileCount];
    private readonly long[] _surfaceEvents = new long[SurfaceCount];
    private readonly long[] _surfaceParticles = new long[SurfaceCount];

    private long _attempts;
    private long _eligiblePlans;
    private long _emittedEvents;
    private long _particles;
    private long _materialRejected;
    private long _probabilityRejected;
    private long _negligibleEnergyRejected;
    private long _invalidRawEnergy;
    private long _exitReduced;
    private long _distanceRejected;
    private long _budgetReduced;
    private long _clusterReduced;
    private long _clusterParticlesRejected;
    private long _clusterEventsRejected;
    private long _clusterFallbacks;
    private long _missingOrInvalidParticleLeaves;
    private long _invalidGeometry;
    private int _maxImpact;
    private int _maxFrame;
    private uint _detailStateFaceMask;
    private float _nextDetailTime;

    public BallisticSparkDiagnostics(bool enabled)
    {
        _enabled = enabled;
    }

    public void RecordAttempt()
    {
        if (_enabled) Increment(ref _attempts);
    }

    public void RecordRejected(BallisticSparkRejectionReason reason)
    {
        if (!_enabled) return;

        switch (reason)
        {
            case BallisticSparkRejectionReason.Material:
                Increment(ref _materialRejected);
                break;
            case BallisticSparkRejectionReason.Distance:
                Increment(ref _distanceRejected);
                break;
            case BallisticSparkRejectionReason.InvalidGeometry:
                Increment(ref _invalidGeometry);
                break;
            case BallisticSparkRejectionReason.InvalidRawEnergy:
                Increment(ref _invalidRawEnergy);
                break;
            case BallisticSparkRejectionReason.NegligibleEnergy:
                Increment(ref _negligibleEnergyRejected);
                break;
        }
    }

    public void RecordEligible(in BallisticSparkPlan plan)
    {
        if (!_enabled) return;

        Increment(ref _eligiblePlans);
        if (plan.ImpactState == BallisticSparkImpactState.PenetrationExit)
            Increment(ref _exitReduced);
        if (plan.MaximumParticles > _maxImpact)
            _maxImpact = plan.MaximumParticles;

        var profile = (int)plan.VisualProfile;
        if (profile >= 0 && profile < _selectedProfiles.Length)
            Increment(ref _selectedProfiles[profile]);
    }

    public void RecordProbabilityRejected()
    {
        if (_enabled) Increment(ref _probabilityRejected);
    }

    public void RecordClusterResult(in BallisticSparkClusterAllowance allowance, bool usedFallback)
    {
        if (!_enabled) return;

        if (allowance.RejectedParticles > 0)
        {
            Increment(ref _clusterReduced);
            Add(ref _clusterParticlesRejected, allowance.RejectedParticles);
        }
        if (allowance.EventRejected)
            Increment(ref _clusterEventsRejected);
        if (usedFallback)
            Increment(ref _clusterFallbacks);
    }

    public void RecordBudgetResult(int requested, int allowed, int frameParticles)
    {
        if (!_enabled) return;

        if (allowed < requested)
            Increment(ref _budgetReduced);
        if (frameParticles > _maxFrame)
            _maxFrame = frameParticles;
    }

    public void RecordMissingOrInvalidParticleLeaves(int count)
    {
        if (_enabled && count > 0)
            Add(ref _missingOrInvalidParticleLeaves, count);
    }

    public void RecordEmission(BallisticSparkSurfaceClass surface, int particles)
    {
        if (!_enabled || particles <= 0) return;
        Increment(ref _emittedEvents);
        Add(ref _particles, particles);

        var index = (int)surface;
        if (index >= 0 && index < _surfaceEvents.Length)
        {
            Increment(ref _surfaceEvents[index]);
            Add(ref _surfaceParticles[index], particles);
        }
    }

    public void RecordDetail(
        in BallisticSparkRuntimeContext context,
        in BallisticSparkPlan plan,
        Vector3 axis,
        int requestedParticles,
        int clusterAllowedParticles,
        int globalAllowedParticles,
        int actualParticles,
        string effectKey,
        string emitterName,
        string particleSystemName,
        float unscaledTime)
    {
        if (!_enabled || float.IsNaN(unscaledTime) || float.IsInfinity(unscaledTime))
            return;

        var incidenceBand = context.NormalIncidenceCosine >= 0.75f ? 0 :
            context.NormalIncidenceCosine <= 0.35f ? 2 : 1;
        var bitIndex = Math.Min(31,
            (int)context.ImpactState * 6 + (context.IsForwardHit ? 3 : 0) + incidenceBand);
        var bit = 1U << bitIndex;
        var firstStateFaceSample = (_detailStateFaceMask & bit) == 0;
        if (!firstStateFaceSample && unscaledTime < _nextDetailTime)
            return;

        _detailStateFaceMask |= bit;
        _nextDetailTime = unscaledTime + DetailIntervalSeconds;
        Plugin.Log.LogInfo(
            $"spark-detail material={context.Material} surface={context.Surface} bulletState={context.EftBulletState} " +
            $"face={(context.IsForwardHit ? "forward" : "back")} massGram={context.ProjectileMassGram:F4} " +
            $"velocityMps={context.SpeedMetresPerSecond:F3} incomingEnergyJ={context.IncomingEnergyJoules:F3} " +
            $"energySource={context.EnergySource} incidenceCos={context.NormalIncidenceCosine:F4} " +
            $"directionSource=Shot.CurrentDirection incomingDirectionCandidate={FormatVector(context.ShotCurrentDirection)} " +
            $"normal={FormatVector(context.Normal)} " +
            $"reflection={FormatVector(context.Reflection)} tangent={FormatVector(context.Tangent)} " +
            $"axis={FormatVector(axis)} probability={plan.Probability:F4} requested={requestedParticles} " +
            $"clusterAllowed={clusterAllowedParticles} globalAllowed={globalAllowedParticles} actual={actualParticles} " +
            $"effect={effectKey ?? "<none>"} emitter={emitterName ?? "<none>"} " +
            $"particleSystem={particleSystemName ?? "<none>"}");
    }

    public void WriteSummaryAndReset()
    {
        if (_enabled)
        {
            Plugin.Log.LogInfo(
                $"spark-summary attempts={_attempts} eligible={_eligiblePlans} emittedEvents={_emittedEvents} " +
                $"particles={_particles} materialRejected={_materialRejected} probabilityRejected={_probabilityRejected} " +
                $"negligibleEnergyRejected={_negligibleEnergyRejected} invalidRawEnergy={_invalidRawEnergy} " +
                $"rawEnergyFallback=0 exitReduced={_exitReduced} distanceRejected={_distanceRejected} " +
                $"invalidGeometry={_invalidGeometry} " +
                $"budgetReduced={_budgetReduced} clusterReduced={_clusterReduced} " +
                $"clusterParticlesRejected={_clusterParticlesRejected} clusterEventsRejected={_clusterEventsRejected} " +
                $"clusterFallbacks={_clusterFallbacks} missingOrInvalidParticleLeaves={_missingOrInvalidParticleLeaves} " +
                $"selectedProfiles={FormatCounts(_selectedProfiles)} emittedBySurface={FormatSurfaceCounts()} " +
                $"maxImpact={_maxImpact} maxFrame={_maxFrame}");
        }

        _attempts = 0;
        _eligiblePlans = 0;
        _emittedEvents = 0;
        _particles = 0;
        _materialRejected = 0;
        _probabilityRejected = 0;
        _negligibleEnergyRejected = 0;
        _invalidRawEnergy = 0;
        _exitReduced = 0;
        _distanceRejected = 0;
        _budgetReduced = 0;
        _clusterReduced = 0;
        _clusterParticlesRejected = 0;
        _clusterEventsRejected = 0;
        _clusterFallbacks = 0;
        _missingOrInvalidParticleLeaves = 0;
        _invalidGeometry = 0;
        _maxImpact = 0;
        _maxFrame = 0;
        _detailStateFaceMask = 0;
        _nextDetailTime = 0f;
        Array.Clear(_selectedProfiles, 0, _selectedProfiles.Length);
        Array.Clear(_surfaceEvents, 0, _surfaceEvents.Length);
        Array.Clear(_surfaceParticles, 0, _surfaceParticles.Length);
    }

    private string FormatSurfaceCounts()
    {
        var builder = new StringBuilder();
        for (var index = 0; index < _surfaceEvents.Length; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append((BallisticSparkSurfaceClass)index);
            builder.Append(':');
            builder.Append(_surfaceEvents[index]);
            builder.Append('/');
            builder.Append(_surfaceParticles[index]);
        }
        return builder.ToString();
    }

    private static string FormatCounts(long[] counts)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < counts.Length; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append((BallisticSparkVisualProfile)index);
            builder.Append(':');
            builder.Append(counts[index]);
        }
        return builder.ToString();
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }

    private static void Increment(ref long value)
    {
        if (value < long.MaxValue)
            value++;
    }

    private static void Add(ref long value, int addition)
    {
        if (addition <= 0 || value == long.MaxValue)
            return;

        value = addition > long.MaxValue - value ? long.MaxValue : value + addition;
    }
}
