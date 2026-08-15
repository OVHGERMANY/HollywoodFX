using System;

namespace HollywoodFX.Decal;

internal enum PenetrationApertureFace : byte
{
    Entry,
    Exit
}

internal readonly struct PenetrationAperturePlan
{
    internal PenetrationAperturePlan(
        bool createAperture,
        long identity,
        long pairIdentity,
        PenetrationApertureFace face)
    {
        CreateAperture = createAperture;
        Identity = identity;
        PairIdentity = pairIdentity;
        Face = face;
    }

    internal bool CreateAperture { get; }
    internal bool PreserveImpact => true;
    internal long Identity { get; }
    internal long PairIdentity { get; }
    internal PenetrationApertureFace Face { get; }
}

/// <summary>
/// Pairs EFT's resolved near-face and far-face impact notifications without retaining
/// an unbounded collection of pooled Shot instances.
/// </summary>
internal sealed class PenetrationApertureTracker
{
    private const int Capacity = 32;

    private readonly object[] _pendingShots = new object[Capacity];
    private readonly long[] _pendingPairs = new long[Capacity];
    private int _nextPending;
    private long _nextIdentity = 1;
    private long _nextPairIdentity = 1;

    internal PenetrationAperturePlan Record(
        object shotIdentity,
        bool isForwardHit,
        bool isConfirmedPenetration)
    {
        if (shotIdentity == null)
            return default;

        if (isForwardHit)
        {
            if (!isConfirmedPenetration)
                return default;

            var pairIdentity = _nextPairIdentity++;
            _pendingShots[_nextPending] = shotIdentity;
            _pendingPairs[_nextPending] = pairIdentity;
            _nextPending = (_nextPending + 1) % Capacity;

            return new PenetrationAperturePlan(
                true,
                _nextIdentity++,
                pairIdentity,
                PenetrationApertureFace.Entry);
        }

        for (var i = 0; i < Capacity; i++)
        {
            if (!ReferenceEquals(_pendingShots[i], shotIdentity))
                continue;

            var pairIdentity = _pendingPairs[i];
            _pendingShots[i] = null;
            _pendingPairs[i] = 0;

            return new PenetrationAperturePlan(
                true,
                _nextIdentity++,
                pairIdentity,
                PenetrationApertureFace.Exit);
        }

        return default;
    }

    internal void Clear()
    {
        Array.Clear(_pendingShots, 0, _pendingShots.Length);
        Array.Clear(_pendingPairs, 0, _pendingPairs.Length);
        _nextPending = 0;
        _nextIdentity = 1;
        _nextPairIdentity = 1;
    }
}

internal static class PenetrationApertureGeometry
{
    internal const float MinimumRadiusMeters = 0.004f;
    internal const float MaximumRadiusMeters = 0.025f;
    internal const float MaximumIncidenceStretch = 3f;

    internal static void ResolveRadii(
        float diameterMillimeters,
        float incidenceCosine,
        out float minorRadius,
        out float majorRadius)
    {
        var diameter = IsFinite(diameterMillimeters) && diameterMillimeters > 0f
            ? diameterMillimeters
            : 7.62f;
        minorRadius = Clamp(
            diameter * 0.0005f * 1.15f,
            MinimumRadiusMeters,
            MaximumRadiusMeters);
        var cosine = Clamp(Math.Abs(incidenceCosine), 0.25f, 1f);
        majorRadius = Math.Min(
            minorRadius / cosine,
            minorRadius * MaximumIncidenceStretch);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}
