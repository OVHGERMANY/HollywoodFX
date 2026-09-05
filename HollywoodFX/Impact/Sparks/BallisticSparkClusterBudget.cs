using System;

namespace HollywoodFX.Impact.Sparks;

public readonly struct BallisticSparkClusterAllowance
{
    public readonly int AllowedParticles;
    public readonly int RejectedParticles;
    public readonly bool EventRejected;
    public readonly bool ParticleLimitReached;

    public BallisticSparkClusterAllowance(
        int allowedParticles,
        int rejectedParticles,
        bool eventRejected,
        bool particleLimitReached)
    {
        AllowedParticles = allowedParticles;
        RejectedParticles = rejectedParticles;
        EventRejected = eventRejected;
        ParticleLimitReached = particleLimitReached;
    }
}

public sealed class BallisticSparkClusterBudget
{
    // Pellet/fragment contacts arrive within a few physics steps. 180 ms groups the burst
    // without carrying state across a deliberate follow-up shot. Two visible events retain
    // some spatial spread, while 18 particles permit one full metal hit plus one small echo.
    // Sixty-four value-only slots cover simultaneous families without raid-growing storage.
    public const float ClusterWindowSeconds = 0.18f;
    public const int PerClusterParticleCap = 18;
    public const int PerClusterEventCap = 2;
    public const int SlotCount = 64;

    private readonly Slot[] _slots = new Slot[SlotCount];

    private struct Slot
    {
        public bool Occupied;
        public ulong Key;
        public float WindowStartedAt;
        public int Particles;
        public int Events;
    }

    public BallisticSparkClusterAllowance Preview(
        bool clusterLimited,
        ulong clusterKey,
        int requestedParticles,
        float unscaledTime)
    {
        if (requestedParticles <= 0)
            return default;

        if (!clusterLimited)
            return new BallisticSparkClusterAllowance(requestedParticles, 0, false, false);

        if (clusterKey == 0UL || float.IsNaN(unscaledTime) || float.IsInfinity(unscaledTime))
            return new BallisticSparkClusterAllowance(0, requestedParticles, true, true);

        var slotIndex = ResolveSlot(clusterKey, unscaledTime);
        // Keep every live family protected. Saturation may drop a new visual event,
        // but it must not reopen a previous family's particle allowance.
        if (slotIndex < 0)
            return new BallisticSparkClusterAllowance(0, requestedParticles, true, true);
        ref var slot = ref _slots[slotIndex];

        if (!slot.Occupied || slot.Key != clusterKey ||
            unscaledTime - slot.WindowStartedAt > ClusterWindowSeconds)
        {
            var initial = Math.Min(requestedParticles, PerClusterParticleCap);
            return new BallisticSparkClusterAllowance(initial, requestedParticles - initial,
                false, initial < requestedParticles);
        }

        if (slot.Events >= PerClusterEventCap)
            return new BallisticSparkClusterAllowance(0, requestedParticles, true,
                slot.Particles >= PerClusterParticleCap);

        var remainingParticles = Math.Max(0, PerClusterParticleCap - slot.Particles);
        if (remainingParticles == 0)
            return new BallisticSparkClusterAllowance(0, requestedParticles, false, true);

        var allowed = Math.Min(requestedParticles, remainingParticles);
        return new BallisticSparkClusterAllowance(
            allowed,
            requestedParticles - allowed,
            false,
            allowed < requestedParticles);
    }

    public BallisticSparkClusterAllowance Consume(
        bool clusterLimited,
        ulong clusterKey,
        int requestedParticles,
        float unscaledTime)
    {
        var allowance = Preview(clusterLimited, clusterKey, requestedParticles, unscaledTime);
        if (!clusterLimited || allowance.AllowedParticles <= 0)
            return allowance;

        ref var slot = ref _slots[ResolveSlot(clusterKey, unscaledTime)];
        if (!slot.Occupied || slot.Key != clusterKey ||
            unscaledTime - slot.WindowStartedAt > ClusterWindowSeconds)
        {
            slot = new Slot { Occupied = true, Key = clusterKey, WindowStartedAt = unscaledTime };
        }
        slot.Events++;
        slot.Particles += allowance.AllowedParticles;
        return allowance;
    }

    public void Reset()
    {
        Array.Clear(_slots, 0, _slots.Length);
    }

    private int ResolveSlot(ulong clusterKey, float unscaledTime)
    {
        var available = -1;

        for (var index = 0; index < _slots.Length; index++)
        {
            ref var slot = ref _slots[index];
            if (slot.Occupied && slot.Key == clusterKey &&
                unscaledTime - slot.WindowStartedAt <= ClusterWindowSeconds)
            {
                return index;
            }

            if (!slot.Occupied ||
                unscaledTime - slot.WindowStartedAt > ClusterWindowSeconds)
            {
                if (available < 0)
                    available = index;
            }
        }

        return available;
    }
}
