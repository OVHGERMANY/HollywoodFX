using EFT.Ballistics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Decal;

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

    private const int Capacity = 64;

    private static readonly Vector3[] Positions = new Vector3[Capacity];
    private static readonly Vector2[] Scales = new Vector2[Capacity];

    private static int _next;

    // Speed of each round as it entered a surface, so the exit can be compared against it. A round only ever
    // has one entry in flight at a time, so this stays small.
    private const int EntryCapacity = 32;

    private static readonly Shot[] EntryShots = new Shot[EntryCapacity];
    private static readonly float[] EntrySpeeds = new float[EntryCapacity];

    private static int _nextEntry;

    /// Scale for the decal currently being drawn, valid only while Armed.
    public static Vector2 Current = Vector2.one;

    public static bool Armed;

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
        Current = Vector2.one;
    }

    public static void Record(Vector3 position, Shot shot)
    {
        // Measure consumes the entry sample when it sees the matching exit, so the entry has to be banked first
        if (shot.IsForwardHit)
        {
            EntryShots[_nextEntry] = shot;
            EntrySpeeds[_nextEntry] = shot.VelocityMagnitude;

            _nextEntry = (_nextEntry + 1) % EntryCapacity;
        }

        Positions[_next] = position;
        Scales[_next] = Measure(shot);

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

    public static bool TryTake(Vector3 position, out Vector2 scale)
    {
        for (var i = 0; i < Capacity; i++)
        {
            // Vector3 equality is Unity's epsilon compare, which is what we want for a value that has been
            // copied through a struct list. The infinity sentinel never matches, since inf - inf is NaN.
            if (Positions[i] != position)
                continue;

            scale = Scales[i];

            // Consume it so a recycled position can never pick up a stale size
            Positions[i] = Vector3.positiveInfinity;

            return true;
        }

        scale = Vector2.one;

        return false;
    }

    private static Vector2 Measure(Shot shot)
    {
        var diameter = shot.BulletDiameterMilimeters;

        if (diameter <= 0f)
            diameter = ReferenceCaliberMm;

        // Taking the ratio to a power keeps the spread readable: at 1.0 a 12.7 punches a hole 1.67x a 7.62,
        // which is too coarse on screen, so the default pulls the exponent below 1 to compress the extremes.
        var caliber = Mathf.Pow(diameter / ReferenceCaliberMm, Plugin.BulletHoleCaliberScaling.Value);
        var scale = Plugin.BulletHoleSize.Value * caliber;
        var variance = Plugin.BulletHoleVariance.Value;

        // A back face hit is the round leaving the surface rather than entering it.
        if (!shot.IsForwardHit)
        {
            scale *= ExitScale(shot, out var raggedness);
            variance *= raggedness;
        }

        // Jittering the axes independently stops repeated hits on one wall from reading as a stamped pattern
        return new Vector2(
            scale * (1f + Random.Range(-variance, variance)),
            scale * (1f + Random.Range(-variance, variance))
        );
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
        var maxScale = Plugin.ExitHoleSize.Value;

        if (!Plugin.ExitHoleEnergyScaling.Value || !TryTakeEntrySpeed(shot, out var entrySpeed) || entrySpeed <= 0.01f)
        {
            // Nothing to compare against - a fragment born inside the material, or an exit whose entry never
            // raised an effect. Fall back to the flat multiplier.
            raggedness = 2f;

            return maxScale;
        }

        var speedRatio = Mathf.Clamp01(shot.VelocityMagnitude / entrySpeed);
        var dumped = 1f - speedRatio * speedRatio;

        // Clean pass-through stays near the entry size and stays tidy; a round that gave up everything opens
        // all the way to the configured maximum and jitters three times as hard.
        raggedness = 1f + 2f * dumped;

        return Mathf.Lerp(1f, maxScale, dumped);
    }
}
