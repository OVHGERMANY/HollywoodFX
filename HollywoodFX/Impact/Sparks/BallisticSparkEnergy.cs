using System;

namespace HollywoodFX.Impact.Sparks;

public enum BallisticSparkEnergySource
{
    None,
    RawIncomingImpactData
}

public static class BallisticSparkEnergy
{
    public static bool TryCalculateIncomingEnergy(
        float projectileMassGram,
        float speedMetresPerSecond,
        out float incomingEnergyJoules)
    {
        incomingEnergyJoules = 0f;
        if (!IsFinite(projectileMassGram) || projectileMassGram <= 0f ||
            !IsFinite(speedMetresPerSecond) || speedMetresPerSecond < 0f)
        {
            return false;
        }

        // This is raw incoming impact kinetic energy: 1/2 mv^2 using EFT's reported
        // projectile mass and current speed. It is not dissipated energy because the
        // outgoing velocity is unavailable at Effects.Emit time.
        var massKilograms = projectileMassGram / 1000d;
        var energy = 0.5d * massKilograms * speedMetresPerSecond * speedMetresPerSecond;
        if (double.IsNaN(energy) || double.IsInfinity(energy) || energy > float.MaxValue)
            return false;

        incomingEnergyJoules = (float)energy;
        return IsFinite(incomingEnergyJoules) && incomingEnergyJoules >= 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
