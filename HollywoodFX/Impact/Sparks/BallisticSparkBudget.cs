using System;

namespace HollywoodFX.Impact.Sparks;

public sealed class BallisticSparkBudget
{
    public const int PerFrameParticleCap = 96;
    public const int RollingCapacity = 192;
    public const float RollingRefillPerSecond = 192f;

    private float _available = RollingCapacity;
    private float _lastTime;
    private int _frame = int.MinValue;
    private int _frameParticles;
    private bool _initialized;

    public int CurrentFrameParticles => _frameParticles;

    public int Consume(int requestedParticles, float unscaledTime, int frame)
    {
        if (requestedParticles <= 0 || float.IsNaN(unscaledTime) || float.IsInfinity(unscaledTime))
            return 0;

        if (!_initialized)
        {
            _initialized = true;
            _lastTime = unscaledTime;
        }
        else
        {
            var elapsed = Math.Max(0f, unscaledTime - _lastTime);
            _available = Math.Min(RollingCapacity, _available + elapsed * RollingRefillPerSecond);
            _lastTime = unscaledTime;
        }

        if (_frame != frame)
        {
            _frame = frame;
            _frameParticles = 0;
        }

        var perImpact = Math.Min(requestedParticles, BallisticSparkPolicy.PerImpactParticleCap);
        var frameRemaining = Math.Max(0, PerFrameParticleCap - _frameParticles);
        var rollingRemaining = Math.Max(0, (int)Math.Floor(_available));
        var allowed = Math.Min(perImpact, Math.Min(frameRemaining, rollingRemaining));

        _frameParticles += allowed;
        _available -= allowed;
        return allowed;
    }

    public void Reset()
    {
        _available = RollingCapacity;
        _lastTime = 0f;
        _frame = int.MinValue;
        _frameParticles = 0;
        _initialized = false;
    }
}
