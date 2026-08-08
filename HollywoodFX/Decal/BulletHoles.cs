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

        _next = 0;
        Armed = false;
        Current = Vector2.one;
    }

    public static void Record(Vector3 position, Shot shot)
    {
        Positions[_next] = position;
        Scales[_next] = Measure(shot);

        _next = (_next + 1) % Capacity;
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

        // A back face hit is the round leaving the surface rather than entering it. Exit holes are wider and
        // far more ragged than entries, so they take an extra multiplier and double the shape jitter.
        if (!shot.IsForwardHit)
        {
            scale *= Plugin.ExitHoleSize.Value;
            variance *= 2f;
        }

        // Jittering the axes independently stops repeated hits on one wall from reading as a stamped pattern
        return new Vector2(
            scale * (1f + Random.Range(-variance, variance)),
            scale * (1f + Random.Range(-variance, variance))
        );
    }
}
