using System;
using Comfort.Common;
using EFT.Ballistics;
using HollywoodFX.Gore;
using UnityEngine;

namespace HollywoodFX.Decal;

/*
 * Replaces the game's always-square bullet projector when one physical impact needs directional geometry:
 * a true ricochet or a stopped round arriving obliquely. The replacement is recorded by position and consumed
 * by EffectQueuedBulletHolePatch before EFT queues its circular decal. Ordinary impacts always remain independent.
 */
internal static class SurfaceImpactMarks
{
    private const int ReplacementCapacity = 64;
    private const float MinimumReadableRicochetWidth = 0.35f;

    private static readonly Vector3[] ReplacedImpactPositions = new Vector3[ReplacementCapacity];

    private static int _nextReplacement;

    static SurfaceImpactMarks()
    {
        Clear();
    }

    public static void TryDraw(ImpactKinetics kinetics, BallisticCollider hitCollider)
    {
        if (hitCollider != null)
        {
            var hasActorOwner = BodyTargetClassifier.IsBodyTarget(
                hitCollider.transform, out _);
            if (!CustomImpactGeometryPolicy.ShouldUseCustomGeometry(
                    hitCollider is BodyPartCollider, hasActorOwner))
            {
                if (Plugin.DebugLoggingEnabled)
                    RuntimeDebugTrace.Write(
                        $"custom surface mark skipped: character-owned collider={hitCollider.name}");
                return;
            }
        }

        var shot = kinetics.Bullet.Info;

        if (shot == null)
        {
            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write("impact skipped: no current Shot was available");

            return;
        }

        var direction = shot.CurrentDirection;
        var validGeometry = direction.sqrMagnitude >= 0.000001f && kinetics.Normal.sqrMagnitude >= 0.000001f;
        var incidenceDegrees = validGeometry
            ? Vector3.Angle(-direction.normalized, kinetics.Normal.normalized)
            : float.NaN;
        var colliderName = hitCollider == null ? "null" : hitCollider.name;
        var colliderStatic = hitCollider == null || hitCollider.gameObject.isStatic;

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"impact state={shot.BulletState} forward={shot.IsForwardHit} " +
                $"incidenceFromNormal={incidenceDegrees:0.###} material={kinetics.Material} " +
                $"collider={colliderName} colliderStatic={colliderStatic} " +
                $"diameterMm={shot.BulletDiameterMilimeters:0.###} massGram={shot.BulletMassGram:0.###} " +
                $"penetrationPower={shot.PenetrationPower:0.###} velocity={shot.VelocityMagnitude:0.###} " +
                $"position={kinetics.Position.ToString("F4")} direction={direction.ToString("F4")}"
            );
        }

        if (!shot.IsForwardHit || !validGeometry)
            return;

        var painter = Singleton<DecalPainter>.Instance;

        if (shot.BulletState == Shot.EBulletState.RicochetHit)
        {
            TryDrawRicochet(painter, kinetics, hitCollider, shot, direction, incidenceDegrees);
            return;
        }

        // Penetration entries remain in BulletHoles' normal queue so their speed can be paired with the later
        // back-face exit. Directional replacement is limited to confirmed stopped impacts.
        if (shot.BulletState != Shot.EBulletState.StopHit || painter == null)
            return;

        var decal = painter.GetBulletDecal(hitCollider);

        if (decal == null)
            return;

        // Only this impact may replace its matching stock decal. Nearby marks are deliberately left alone.
        var shouldDrawOblique = Plugin.DirectionalBulletMarksEnabled.Value &&
                                !float.IsNaN(incidenceDegrees) &&
                                incidenceDegrees >= Plugin.ObliqueImpactAngle.Value;

        if (!shouldDrawOblique)
            return;

        var obliqueness = Mathf.InverseLerp(Plugin.ObliqueImpactAngle.Value, 85f, incidenceDegrees);
        var cosine = Mathf.Max(0.12f, Mathf.Cos(incidenceDegrees * Mathf.Deg2Rad));
        var lengthMultiplier = Mathf.Clamp(1f / Mathf.Sqrt(cosine), 1.1f, 2.75f);
        var widthMultiplier = Mathf.Lerp(0.95f, 0.55f, obliqueness);
        var sizeMultiplier = BulletHoles.GetForwardScale(shot, includeVariance: true);

        var obliqueDrawn = painter.DrawOrientedDecal(
            decal,
            kinetics.Position,
            kinetics.Normal,
            hitCollider,
            direction,
            lengthMultiplier,
            widthMultiplier,
            sizeMultiplier,
            lockFirstTile: true
        );

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"oblique stop drawn={obliqueDrawn} incidence={incidenceDegrees:0.###} " +
                $"length={lengthMultiplier:0.###} width={widthMultiplier:0.###} scale={sizeMultiplier:0.###}"
            );
        }

        if (obliqueDrawn)
            RegisterReplacement(kinetics.Position, "oblique-stop");
    }

    private static void TryDrawRicochet(
        DecalPainter painter,
        ImpactKinetics kinetics,
        BallisticCollider hitCollider,
        Shot shot,
        Vector3 direction,
        float incidenceDegrees)
    {
        if (!Plugin.RicochetMarksEnabled.Value)
            return;

        if (painter == null)
        {
            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write("ricochet scrape skipped: painter=null");

            return;
        }

        // Tracer_Decal_Scorch is a large environmental burn projector (roughly 0.35-0.55 m before
        // scaling), not bullet-hole artwork. Stretching it for a ricochet produced projectors more than
        // two metres long which often read as no contact mark at all. Use the struck surface's bullet
        // decal instead: it is already material-correct and bullet-sized, and squashing its first atlas
        // tile produces a visible gouge at the actual point where the round changed direction.
        var decal = painter.GetBulletDecal(hitCollider);

        if (decal == null)
        {
            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write("ricochet scrape skipped: surface bullet decal=null");

            return;
        }

        var angleScale = Mathf.Lerp(1.35f, 2.75f, Mathf.InverseLerp(45f, 85f, incidenceDegrees));
        var caliberScale = Mathf.Clamp(BulletHoles.GetForwardScale(shot, includeVariance: false), 0.65f, 1.6f);
        var lengthMultiplier = Plugin.RicochetMarkLength.Value * angleScale;
        var widthMultiplier = Mathf.Max(MinimumReadableRicochetWidth, Plugin.RicochetMarkWidth.Value);

        var drawn = painter.DrawOrientedDecal(
            decal,
            kinetics.Position,
            kinetics.Normal,
            hitCollider,
            direction,
            lengthMultiplier,
            widthMultiplier,
            caliberScale,
            lockFirstTile: true
        );

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"ricochet scrape drawn={drawn} incidence={incidenceDegrees:0.###} " +
                $"material={decal.DecalMaterial.name} length={lengthMultiplier:0.###} " +
                $"width={widthMultiplier:0.###} configuredWidth={Plugin.RicochetMarkWidth.Value:0.###} " +
                $"scale={caliberScale:0.###}"
            );
        }

        if (drawn)
            RegisterReplacement(kinetics.Position, "ricochet");
    }

    internal static void RegisterReplacement(Vector3 position, string kind)
    {
        ReplacedImpactPositions[_nextReplacement] = position;
        _nextReplacement = (_nextReplacement + 1) % ReplacementCapacity;

        if (Plugin.DebugLoggingEnabled)
            RuntimeDebugTrace.Write($"registered {kind} replacement at {position.ToString("F4")}");
    }

    public static bool TryConsumeStandardDecalReplacement(Vector3 position)
    {
        for (var i = 0; i < ReplacementCapacity; i++)
        {
            if (ReplacedImpactPositions[i] != position)
                continue;

            ReplacedImpactPositions[i] = Vector3.positiveInfinity;

            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write($"suppressed round decal at {position.ToString("F4")}");

            return true;
        }

        return false;
    }

    public static void Clear()
    {
        Array.Fill(ReplacedImpactPositions, Vector3.positiveInfinity);
        _nextReplacement = 0;
    }
}
