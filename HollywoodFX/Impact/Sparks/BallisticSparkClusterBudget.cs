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

    public BallisticSparkClusterAllowance Consume(
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
        ref var slot = ref _slots[slotIndex];

        if (!slot.Occupied || slot.Key != clusterKey || unscaledTime < slot.WindowStartedAt ||
            unscaledTime - slot.WindowStartedAt > ClusterWindowSeconds)
        {
            slot.Occupied = true;
            slot.Key = clusterKey;
            slot.WindowStartedAt = unscaledTime;
            slot.Particles = 0;
            slot.Events = 0;
        }

        if (slot.Events >= PerClusterEventCap)
            return new BallisticSparkClusterAllowance(0, requestedParticles, true,
                slot.Particles >= PerClusterParticleCap);

        var remainingParticles = Math.Max(0, PerClusterParticleCap - slot.Particles);
        if (remainingParticles == 0)
            return new BallisticSparkClusterAllowance(0, requestedParticles, false, true);

        var allowed = Math.Min(requestedParticles, remainingParticles);
        slot.Events++;
        slot.Particles += allowed;
        return new BallisticSparkClusterAllowance(
            allowed,
            requestedParticles - allowed,
            false,
            allowed < requestedParticles);
    }

    public void Reset()
    {
        Array.Clear(_slots, 0, _slots.Length);
    }

    private int ResolveSlot(ulong clusterKey, float unscaledTime)
    {
        var available = -1;
        var replacement = 0;
        var oldestStart = float.MaxValue;

        for (var index = 0; index < _slots.Length; index++)
        {
            ref var slot = ref _slots[index];
            if (slot.Occupied && slot.Key == clusterKey && unscaledTime >= slot.WindowStartedAt &&
                unscaledTime - slot.WindowStartedAt <= ClusterWindowSeconds)
            {
                return index;
            }

            if (!slot.Occupied || unscaledTime < slot.WindowStartedAt ||
                unscaledTime - slot.WindowStartedAt > ClusterWindowSeconds)
            {
                if (available < 0)
                    available = index;
                continue;
            }

            if (slot.WindowStartedAt < oldestStart)
            {
                oldestStart = slot.WindowStartedAt;
                replacement = index;
            }
        }

        return available >= 0 ? available : replacement;
    }
}
