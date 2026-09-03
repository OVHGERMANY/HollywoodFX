namespace HollywoodFX.Impact.Sparks;

internal sealed class BallisticSparkDiagnostics
{
    private readonly bool _enabled;

    private long _attempts;
    private long _eligiblePlans;
    private long _emittedEvents;
    private long _particles;
    private long _materialRejected;
    private long _probabilityRejected;
    private long _exitReduced;
    private long _distanceRejected;
    private long _budgetRejected;
    private long _invalidGeometry;
    private long _tracerDuplicatePrevented;
    private int _maxImpact;
    private int _maxFrame;

    public BallisticSparkDiagnostics(bool enabled)
    {
        _enabled = enabled;
    }

    public void RecordAttempt()
    {
        if (_enabled) _attempts++;
    }

    public void RecordRejected(BallisticSparkRejectionReason reason)
    {
        if (!_enabled) return;

        switch (reason)
        {
            case BallisticSparkRejectionReason.Material:
                _materialRejected++;
                break;
            case BallisticSparkRejectionReason.Distance:
                _distanceRejected++;
                break;
            case BallisticSparkRejectionReason.InvalidGeometry:
                _invalidGeometry++;
                break;
        }
    }

    public void RecordEligible(in BallisticSparkPlan plan, bool isTracer)
    {
        if (!_enabled) return;

        _eligiblePlans++;
        if (plan.ImpactState == BallisticSparkImpactState.PenetrationExit)
            _exitReduced++;
        if (isTracer)
            _tracerDuplicatePrevented++;
        if (plan.MaximumParticles > _maxImpact)
            _maxImpact = plan.MaximumParticles;
    }

    public void RecordProbabilityRejected()
    {
        if (_enabled) _probabilityRejected++;
    }

    public void RecordBudgetResult(int requested, int emitted, int frameParticles)
    {
        if (!_enabled) return;

        if (emitted < requested)
            _budgetRejected++;
        if (frameParticles > _maxFrame)
            _maxFrame = frameParticles;
    }

    public void RecordEmission(int particles)
    {
        if (!_enabled || particles <= 0) return;
        _emittedEvents++;
        _particles += particles;
    }

    public void WriteSummaryAndReset()
    {
        if (_enabled)
        {
            Plugin.Log.LogInfo(
                $"spark-summary attempts={_attempts} eligible={_eligiblePlans} emittedEvents={_emittedEvents} " +
                $"particles={_particles} materialRejected={_materialRejected} probabilityRejected={_probabilityRejected} " +
                $"exitReduced={_exitReduced} distanceRejected={_distanceRejected} budgetRejected={_budgetRejected} " +
                $"invalidGeometry={_invalidGeometry} tracerDuplicatePrevented={_tracerDuplicatePrevented} " +
                $"maxImpact={_maxImpact} maxFrame={_maxFrame}");
        }

        _attempts = 0;
        _eligiblePlans = 0;
        _emittedEvents = 0;
        _particles = 0;
        _materialRejected = 0;
        _probabilityRejected = 0;
        _exitReduced = 0;
        _distanceRejected = 0;
        _budgetRejected = 0;
        _invalidGeometry = 0;
        _tracerDuplicatePrevented = 0;
        _maxImpact = 0;
        _maxFrame = 0;
    }
}
