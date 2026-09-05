using System;
using System.Numerics;

namespace HollywoodFX.Impact.Sparks;

// Pure value math shared by the runtime and the portable regression suite.
internal readonly struct BallisticSparkEmissionFrame
{
    public const float SurfaceOffsetMetres = 0.001f;
    private readonly Vector3 _normal;
    private readonly Vector3 _axis;
    private readonly Vector3 _right;
    private readonly Vector3 _up;
    private readonly float _minimumCosine;

    private BallisticSparkEmissionFrame(Vector3 normal, Vector3 axis, Vector3 right,
        Vector3 up, float minimumCosine)
    {
        _normal = normal;
        _axis = axis;
        _right = right;
        _up = up;
        _minimumCosine = minimumCosine;
    }

    public static bool TryCreate(Vector3 normal, Vector3 axis, float spreadDegrees,
        out BallisticSparkEmissionFrame frame)
    {
        frame = default;
        if (!TryNormalize(normal, out normal) || !TryNormalize(axis, out axis) || !IsFinite(spreadDegrees))
            return false;

        var inward = Vector3.Dot(axis, normal);
        if (inward < 0f)
        {
            axis -= normal * inward;
            if (!TryNormalize(axis, out axis))
                axis = normal;
        }

        var reference = Math.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        if (!TryNormalize(Vector3.Cross(reference, axis), out var right))
            return false;
        var up = Vector3.Cross(axis, right);
        var cosine = (float)Math.Cos(Math.Clamp(spreadDegrees, 0f, 75f) * (Math.PI / 180d));
        frame = new BallisticSparkEmissionFrame(normal, axis, right, up, cosine);
        return true;
    }

    public Vector3 SampleDirection(ref BallisticSparkPrng random)
    {
        // Uniform solid-angle sampling uses exactly two draws, without rejection loops.
        var cosine = 1f - random.NextFloat01() * (1f - _minimumCosine);
        var sine = (float)Math.Sqrt(Math.Max(0f, 1f - cosine * cosine));
        var azimuth = random.NextFloat01() * (2d * Math.PI);
        var direction = _axis * cosine +
                        (_right * (float)Math.Cos(azimuth) + _up * (float)Math.Sin(azimuth)) * sine;
        var inward = Vector3.Dot(direction, _normal);
        // Mirror the inward half of a grazing cone. Projection would collapse those
        // particles onto a flat line; reflection keeps their spread and unit length.
        if (inward < 0f)
            direction -= 2f * inward * _normal;
        return direction;
    }

    public Vector3 ResolvePosition(Vector3 impactPosition)
    {
        return impactPosition + _normal * SurfaceOffsetMetres;
    }

    public static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        normalized = default;
        if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            return false;
        var largest = Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        if (largest == 0f)
            return false;
        var scaled = value / largest;
        var length = (float)Math.Sqrt(Vector3.Dot(scaled, scaled));
        if (largest < 0.001f / length)
            return false;
        normalized = scaled / length;
        return true;
    }

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
