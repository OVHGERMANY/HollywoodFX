using System;

namespace HollywoodFX.Impact.Sparks;

public struct BallisticSparkPrng
{
    private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public BallisticSparkPrng(ulong seed)
    {
        _state = seed == 0UL ? DefaultSeed : seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        // xorshift64*: four integer operations, one fixed state value, and no allocation.
        var state = _state;
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        _state = state;
        return (uint)((state * 0x2545F4914F6CDD1DUL) >> 32);
    }

    public float NextFloat01()
    {
        return (NextUInt() >> 8) * (1f / 16777216f);
    }

    public float NextSignedFloat()
    {
        return NextFloat01() * 2f - 1f;
    }

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
            return minimumInclusive;

        var range = (uint)(maximumExclusive - minimumInclusive);
        var scaled = ((ulong)NextUInt() * range) >> 32;
        return minimumInclusive + (int)scaled;
    }
}

public static class BallisticSparkSeed
{
    public const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Add(ulong seed, int value)
    {
        return Add(seed, unchecked((uint)value));
    }

    public static ulong Add(ulong seed, uint value)
    {
        unchecked
        {
            seed = (seed ^ (byte)value) * Prime;
            seed = (seed ^ (byte)(value >> 8)) * Prime;
            seed = (seed ^ (byte)(value >> 16)) * Prime;
            return (seed ^ (byte)(value >> 24)) * Prime;
        }
    }

    public static ulong Add(ulong seed, ulong value)
    {
        seed = Add(seed, (uint)value);
        return Add(seed, (uint)(value >> 32));
    }

    public static ulong Add(ulong seed, string value)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(value))
                return (seed ^ 0xffU) * Prime;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                seed = (seed ^ (byte)character) * Prime;
                seed = (seed ^ (byte)(character >> 8)) * Prime;
            }

            return seed;
        }
    }
}
