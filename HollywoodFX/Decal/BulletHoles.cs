using EFT.Ballistics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Decal;

internal enum BulletHoleKind : byte
{
    Stopped,
    PenetrationEntry,
    PenetrationExit
}

/*
 * Per-shot sizing for the game's bullet hole decals.
 *
 * The caliber cannot simply be read when the decal is drawn. Effects.Emit does not draw anything - it queues
 * an EffectEmitDescription, and Effects.Update flushes at most MAX_EFFECTS_PER_FRAME (10) of them per frame.
 * A burst or a shotgun therefore pushes decals into later frames, by which point ImpactStatic.Kinetics.Bullet
 * has already been overwritten by a newer shot. So the size is measured when the effect is queued and parked
 * here until the matching draw comes through.
 *
 * Entries live in a fixed ring: a description that is dropped rather than flushed just gets overwritten, so
 * there is nothing to leak and nothing to sweep.
 */
internal static class BulletHoles
{
    // 7.62 sits in the middle of the ammo table, so it is the caliber that renders at the configured size
    private const float ReferenceCaliberMm = 7.62f;

    // DeferredDecalRenderer clamps static decals to this size. The transpiler keeps that vanilla ceiling for
    // ordinary decals but raises it proportionally while a configured bullet-hole scale is armed.
    private const float VanillaStaticDecalMaxSize = 0.4f;

    // An exit that is allowed to fall back to entry size is visually ambiguous. Keep even a clean
    // pass-through large enough to read from the back face; energy loss can then widen it further.
    private const float MinimumReadableExitScale = 1.35f;

    private const int Capacity = 64;

    private static readonly Vector3[] Positions = new Vector3[Capacity];
    private static readonly float[] Scales = new float[Capacity];
    private static readonly BulletHoleKind[] Kinds = new BulletHoleKind[Capacity];

    private static int _next;

    // Speed of each round as it entered a surface, so the exit can be compared against it. A round only ever
    // has one entry in flight at a time, so this stays small.
    private const int EntryCapacity = 32;

    private static readonly Shot[] EntryShots = new Shot[EntryCapacity];
    private static readonly float[] EntrySpeeds = new float[EntryCapacity];

    private static int _nextEntry;

    /// Scale for the decal currently being drawn, valid only while Armed.
    public static float Current = 1f;

    public static BulletHoleKind CurrentKind = BulletHoleKind.Stopped;

    public static bool Armed;

    // The stock atlas contains several crater paintings with very different visible footprints. Lock forward
    // impacts to one tile so caliber and the configured variance control their apparent size. Exit holes keep
    // the atlas variety because their broken back-face edge is intentionally irregular.
    public static bool ShouldLockAtlasTile => CurrentKind != BulletHoleKind.PenetrationExit;

    public static float GetStaticDecalSizeLimit()
    {
        if (!Armed)
            return VanillaStaticDecalMaxSize;

        return VanillaStaticDecalMaxSize * Current;
    }

    /// <summary>
    /// EFT stores a random minimum/maximum radius in SingleDecal.DecalSize; its two components are not X/Y
    /// dimensions. Resolve that vanilla range to its midpoint and apply this shot's scalar multiplier once,
    /// otherwise EFT's own random range compounds with our variance and identical rounds can render nearly
    /// twice as wide as one another.
    /// </summary>
    public static Vector2 ResolveDecalSize(Vector2 vanillaRange)
    {
        var midpoint = (vanillaRange.x + vanillaRange.y) * 0.5f;
        var resolved = midpoint * Current;

        return new Vector2(resolved, resolved);
    }

    static BulletHoles()
    {
        Clear();
    }

    public static void Clear()
    {
        for (var i = 0; i < Capacity; i++)
            Positions[i] = Vector3.positiveInfinity;

        for (var i = 0; i < EntryCapacity; i++)
            EntryShots[i] = null;

        _next = 0;
        _nextEntry = 0;
        Armed = false;
        Current = 1f;
        CurrentKind = BulletHoleKind.Stopped;
    }

    public static void Record(Vector3 position, Shot shot)
    {
        var kind = Classify(shot);

        // Measure consumes the entry sample when it sees the matching exit, so the entry has to be banked first
        if (kind == BulletHoleKind.PenetrationEntry)
        {
            EntryShots[_nextEntry] = shot;
            EntrySpeeds[_nextEntry] = shot.VelocityMagnitude;

            _nextEntry = (_nextEntry + 1) % EntryCapacity;
        }

        Positions[_next] = position;
        Scales[_next] = Measure(shot, kind);
        Kinds[_next] = kind;

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"bullet-hole queued kind={kind} state={shot.BulletState} forward={shot.IsForwardHit} " +
                $"diameterMm={shot.BulletDiameterMilimeters:0.###} " +
                $"velocity={shot.VelocityMagnitude:0.###} scale={Scales[_next]:0.###} position={position.ToString("F4")}"
            );
        }

        _next = (_next + 1) % Capacity;
    }

    private static bool TryTakeEntrySpeed(Shot shot, out float speed)
    {
        for (var i = 0; i < EntryCapacity; i++)
        {
            if (!ReferenceEquals(EntryShots[i], shot))
                continue;

            speed = EntrySpeeds[i];

            // Shot instances come from a pool, so a consumed sample must not be matched by a later round
            EntryShots[i] = null;

            return true;
        }

        speed = 0f;

        return false;
    }

    public static bool TryTake(Vector3 position, out float scale, out BulletHoleKind kind)
    {
        for (var i = 0; i < Capacity; i++)
        {
            // Vector3 equality is Unity's epsilon compare, which is what we want for a value that has been
            // copied through a struct list. The infinity sentinel never matches, since inf - inf is NaN.
            if (Positions[i] != position)
                continue;

            scale = Scales[i];
            kind = Kinds[i];

            // Consume it so a recycled position can never pick up a stale size
            Positions[i] = Vector3.positiveInfinity;

            return true;
        }

        scale = 1f;
        kind = BulletHoleKind.Stopped;

        return false;
    }

    private static BulletHoleKind Classify(Shot shot)
    {
        if (!shot.IsForwardHit)
            return BulletHoleKind.PenetrationExit;

        return shot.BulletState is Shot.EBulletState.StopHit or Shot.EBulletState.RicochetHit
            ? BulletHoleKind.Stopped
            : BulletHoleKind.PenetrationEntry;
    }

    public static float GetForwardScale(Shot shot, bool includeVariance)
    {
        var diameter = shot.BulletDiameterMilimeters;

        if (diameter <= 0f)
            diameter = ReferenceCaliberMm;

        var caliber = Mathf.Pow(diameter / ReferenceCaliberMm, Plugin.BulletHoleCaliberScaling.Value);
        var scale = Plugin.BulletHoleSize.Value * caliber;

        if (!includeVariance)
            return scale;

        var variance = Plugin.BulletHoleVariance.Value;

        return scale * (1f + Random.Range(-variance, variance));
    }

    private static float Measure(Shot shot, BulletHoleKind kind)
    {
        // Taking the caliber ratio to a power keeps the spread readable: at 1.0 a 12.7 punches a hole 1.67x
        // a 7.62, which is too coarse on screen, so the default exponent compresses the extremes.
        var scale = GetForwardScale(shot, includeVariance: false);
        var variance = Plugin.BulletHoleVariance.Value;

        if (kind == BulletHoleKind.PenetrationExit)
        {
            scale *= ExitScale(shot, out var raggedness);
            variance *= raggedness;

            // Never let exit jitter shrink a readable blowout back toward entry size. Variation only grows
            // the exit, while the retained-energy calculation controls its guaranteed baseline.
            return scale * (1f + Random.Range(0f, variance));
        }

        // SingleDecal.DecalSize is a min/max range rather than two axes. Resolve that range elsewhere and use
        // one small scalar jitter here so caliber remains the dominant, readable size signal.
        return scale * (1f + Random.Range(-variance, variance));
    }

    /*
     * What tears an exit hole open is the energy the round dumped getting through the material, not the energy
     * it left with. A bullet that sails through plasterboard barely marks the far side; one that spent almost
     * everything punching out of a car door deformed and tumbled on the way and leaves a wide, ragged tear.
     *
     * Kinetic energy is 1/2 m v^2, and it is the same round on both sides of the material, so the mass term
     * cancels and the retained fraction is simply the square of the speed ratio.
     */
    private static float ExitScale(Shot shot, out float raggedness)
    {
        var maxScale = Mathf.Max(MinimumReadableExitScale, Plugin.ExitHoleSize.Value);

        if (!Plugin.ExitHoleEnergyScaling.Value || !TryTakeEntrySpeed(shot, out var entrySpeed) || entrySpeed <= 0.01f)
        {
            // Nothing to compare against - a fragment born inside the material, or an exit whose entry never
            // raised an effect. Fall back to the flat multiplier.
            raggedness = 2f;

            if (Plugin.DebugLoggingEnabled)
            {
                RuntimeDebugTrace.Write(
                    $"penetration exit unpairedOrFlat exitVelocity={shot.VelocityMagnitude:0.###} " +
                    $"multiplier={maxScale:0.###}"
                );
            }

            return maxScale;
        }

        var speedRatio = Mathf.Clamp01(shot.VelocityMagnitude / entrySpeed);
        var dumped = 1f - speedRatio * speedRatio;

        // A clean pass-through uses the guaranteed readable baseline; a round that gave up almost everything
        // opens toward the configured maximum and gains a more irregular edge.
        raggedness = 1f + 2f * dumped;

        var result = Mathf.Lerp(MinimumReadableExitScale, maxScale, dumped);

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"penetration exit entryVelocity={entrySpeed:0.###} exitVelocity={shot.VelocityMagnitude:0.###} " +
                $"retainedEnergy={1f - dumped:0.###} dumpedEnergy={dumped:0.###} multiplier={result:0.###}"
            );
        }

        return result;
    }
}
