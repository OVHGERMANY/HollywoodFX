using System;
using Comfort.Common;
using DeferredDecals;
using EFT.Ballistics;
using UnityEngine;

namespace HollywoodFX.Decal;

/*
 * Replaces the game's always-square bullet projector when the physical result needs directional geometry:
 * a true ricochet, a stopped round arriving obliquely, or a group of overlapping impacts. The replacement is
 * recorded by position and consumed by EffectQueuedBulletHolePatch before EFT queues its circular decal.
 */
internal static class SurfaceImpactMarks
{
    private const int ReplacementCapacity = 64;
    private const int ClusterCapacity = 32;
    private const float ClusterLifetimeSeconds = 30f;
    private const float MaximumClusterExtentInRadii = 3f;
    private const float MinimumRadius = 0.001f;
    private const float AxisLockDistanceSquared = 0.000001f;
    private const float MinimumReadableRicochetWidth = 0.35f;

    private static readonly Vector3[] ReplacedImpactPositions = new Vector3[ReplacementCapacity];
    private static readonly ImpactCluster[] Clusters = new ImpactCluster[ClusterCapacity];

    private static int _nextReplacement;
    private static int _nextCluster;

    private struct ImpactCluster
    {
        public bool Active;
        public int ColliderId;
        public bool AxisLocked;
        public bool UsesColliderSpace;
        public Vector3 Anchor;
        public Vector3 Normal;
        public Vector3 Axis;
        public float MinAlong;
        public float MaxAlong;
        public float MinAcross;
        public float MaxAcross;
        public float MaximumRadius;
        public float LastTime;
        public int Hits;
        public DecalPainter.OrientedDecalHandle Projector;
    }

    static SurfaceImpactMarks()
    {
        Clear();
    }

    public static void TryDraw(ImpactKinetics kinetics, BallisticCollider hitCollider)
    {
        var shot = kinetics.Bullet.Info;

        if (shot == null)
        {
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

        RuntimeDebugTrace.Write(
            $"impact state={shot.BulletState} forward={shot.IsForwardHit} " +
            $"incidenceFromNormal={incidenceDegrees:0.###} material={kinetics.Material} " +
            $"collider={colliderName} colliderStatic={colliderStatic} " +
            $"diameterMm={shot.BulletDiameterMilimeters:0.###} massGram={shot.BulletMassGram:0.###} " +
            $"penetrationPower={shot.PenetrationPower:0.###} velocity={shot.VelocityMagnitude:0.###} " +
            $"position={kinetics.Position.ToString("F4")} direction={direction.ToString("F4")}"
        );

        if (!shot.IsForwardHit || !validGeometry)
            return;

        var painter = Singleton<DecalPainter>.Instance;

        if (shot.BulletState == Shot.EBulletState.RicochetHit)
        {
            TryDrawRicochet(painter, kinetics, hitCollider, shot, direction, incidenceDegrees);
            return;
        }

        // Penetration entries must remain in BulletHoles' normal queue so their speed can be paired with the
        // later back-face exit. Directional and compound replacement is currently limited to confirmed stops.
        if (shot.BulletState != Shot.EBulletState.StopHit || painter == null)
            return;

        var decal = painter.GetBulletDecal(hitCollider);

        if (decal == null)
            return;

        // An oblique stop needs its own directional footprint on the very first hit.  The compound
        // builder deliberately draws first hits as a circular, reliable seed, which would otherwise
        // return early and turn a 60-75 degree impact back into a round hole.  Keep the seed path for
        // ordinary stops, but let a genuine oblique impact reach the oriented-decal branch below.
        var shouldDrawOblique = Plugin.DirectionalBulletMarksEnabled.Value &&
                                !float.IsNaN(incidenceDegrees) &&
                                incidenceDegrees >= Plugin.ObliqueImpactAngle.Value;

        if (Plugin.MergeOverlappingBulletHoles.Value && !shouldDrawOblique && TryBuildCompoundMark(
                decal,
                kinetics.Position,
                kinetics.Normal,
                hitCollider,
                shot,
                direction,
                out var compoundCenter,
                out var compoundNormal,
                out var compoundAxis,
                out var compoundLength,
                out var compoundWidth,
                out var compoundScale,
                out var compoundHits,
                out var compoundClusterIndex))
        {
            var cluster = Clusters[compoundClusterIndex];
            var projector = cluster.Projector;
            var drawn = painter.DrawOrUpdateOrientedDecal(
                ref projector,
                decal,
                compoundCenter,
                compoundNormal,
                hitCollider,
                compoundAxis,
                compoundLength,
                compoundWidth,
                compoundScale,
                lockFirstTile: true
            );

            if (drawn)
            {
                cluster.Projector = projector;
                Clusters[compoundClusterIndex] = cluster;
            }

            RuntimeDebugTrace.Write(
                $"compound impact drawn={drawn} hits={compoundHits} center={compoundCenter.ToString("F4")} " +
                $"normal={compoundNormal.ToString("F4")} axis={compoundAxis.ToString("F4")} " +
                $"axisLocked={cluster.AxisLocked} length={compoundLength:0.###} " +
                $"width={compoundWidth:0.###} scale={compoundScale:0.###} cluster={compoundClusterIndex}"
            );

            if (drawn)
                RegisterReplacement(kinetics.Position, "compound");

            return;
        }

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

        RuntimeDebugTrace.Write(
            $"oblique stop drawn={obliqueDrawn} incidence={incidenceDegrees:0.###} " +
            $"length={lengthMultiplier:0.###} width={widthMultiplier:0.###} scale={sizeMultiplier:0.###}"
        );

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

        RuntimeDebugTrace.Write(
            $"ricochet scrape drawn={drawn} incidence={incidenceDegrees:0.###} " +
            $"material={decal.DecalMaterial.name} length={lengthMultiplier:0.###} " +
            $"width={widthMultiplier:0.###} configuredWidth={Plugin.RicochetMarkWidth.Value:0.###} " +
            $"scale={caliberScale:0.###}"
        );

        if (drawn)
            RegisterReplacement(kinetics.Position, "ricochet");
    }

    private static bool TryBuildCompoundMark(
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 normal,
        BallisticCollider hitCollider,
        Shot shot,
        Vector3 shotDirection,
        out Vector3 center,
        out Vector3 compoundNormal,
        out Vector3 axis,
        out float lengthMultiplier,
        out float widthMultiplier,
        out float sizeMultiplier,
        out int hits,
        out int clusterIndex)
    {
        center = position;
        compoundNormal = normal.normalized;
        axis = GetSurfaceAxis(shotDirection, compoundNormal);
        lengthMultiplier = 1f;
        widthMultiplier = 1f;
        sizeMultiplier = BulletHoles.GetForwardScale(shot, includeVariance: false);
        hits = 1;
        clusterIndex = -1;

        var rawRadius = (decal.DecalSize.x + decal.DecalSize.y) * 0.5f;

        if (rawRadius <= MinimumRadius)
            return false;

        var shotRadius = Mathf.Max(MinimumRadius, rawRadius * sizeMultiplier);
        var surfaceNormal = compoundNormal;

        var colliderId = hitCollider == null ? 0 : hitCollider.GetInstanceID();
        var now = Time.unscaledTime;
        var mergeDistance = shotRadius * Plugin.BulletHoleMergeDistance.Value;
        var nearestIndex = -1;
        var nearestDistance = float.MaxValue;

        for (var i = 0; i < ClusterCapacity; i++)
        {
            var cluster = Clusters[i];

            if (!cluster.Active)
                continue;

            var age = now - cluster.LastTime;

            if (age < 0f || age > ClusterLifetimeSeconds)
            {
                cluster.Active = false;
                Clusters[i] = cluster;
                continue;
            }

            if (cluster.ColliderId != colliderId ||
                !TryRestoreClusterBasis(cluster, hitCollider, out var anchorWorld, out var normalWorld, out var axisWorld) ||
                Vector3.Dot(normalWorld, surfaceNormal) < 0.94f)
                continue;

            var acrossWorld = Vector3.Cross(normalWorld, axisWorld).normalized;
            var middleAlong = 0.5f * (cluster.MinAlong + cluster.MaxAlong);
            var middleAcross = 0.5f * (cluster.MinAcross + cluster.MaxAcross);
            var clusterCenter = anchorWorld + axisWorld * middleAlong + acrossWorld * middleAcross;
            var halfLength = 0.5f * (cluster.MaxAlong - cluster.MinAlong);
            var halfWidth = 0.5f * (cluster.MaxAcross - cluster.MinAcross);
            var footprintRadius = Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth);
            var deltaFromCenter = position - clusterCenter;
            var deltaFromAnchor = Vector3.ProjectOnPlane(position - anchorWorld, normalWorld);
            var planeDistance = Vector3.ProjectOnPlane(deltaFromCenter, normalWorld).magnitude;
            var normalDistance = Mathf.Abs(Vector3.Dot(position - anchorWorld, normalWorld));
            var largestRadius = Mathf.Max(cluster.MaximumRadius, shotRadius);
            var allowedDistance = mergeDistance + Mathf.Min(footprintRadius, largestRadius * 1.5f);
            var insideMaximumExtent = deltaFromAnchor.magnitude <= largestRadius * MaximumClusterExtentInRadii;

            if (!insideMaximumExtent || normalDistance > largestRadius ||
                planeDistance > allowedDistance || planeDistance >= nearestDistance)
                continue;

            nearestIndex = i;
            nearestDistance = planeDistance;
        }

        if (nearestIndex < 0)
        {
            var usesColliderSpace = hitCollider != null && !hitCollider.gameObject.isStatic;
            var storedAnchor = usesColliderSpace
                ? hitCollider.transform.InverseTransformPoint(position)
                : position;
            var storedNormal = usesColliderSpace
                ? hitCollider.transform.InverseTransformDirection(surfaceNormal).normalized
                : surfaceNormal;
            var storedAxis = usesColliderSpace
                ? hitCollider.transform.InverseTransformDirection(axis).normalized
                : axis;

            clusterIndex = _nextCluster;
            Clusters[clusterIndex] = new ImpactCluster
            {
                Active = true,
                ColliderId = colliderId,
                AxisLocked = false,
                UsesColliderSpace = usesColliderSpace,
                Anchor = storedAnchor,
                Normal = storedNormal,
                Axis = storedAxis,
                MinAlong = -shotRadius,
                MaxAlong = shotRadius,
                MinAcross = -shotRadius,
                MaxAcross = shotRadius,
                MaximumRadius = shotRadius,
                LastTime = now,
                Hits = 1
            };
            _nextCluster = (_nextCluster + 1) % ClusterCapacity;

            // Draw the first impact through the same owned-projector path used by later compound updates.  Keeping
            // the initial handle lets the next nearby shot resize this exact mark instead of layering another
            // projector over a separately allocated stock decal.
            center = position;
            compoundNormal = surfaceNormal;
            axis = GetSurfaceAxis(shotDirection, surfaceNormal);
            lengthMultiplier = 1f;
            widthMultiplier = 1f;
            sizeMultiplier = shotRadius / rawRadius;
            hits = 1;

            return true;
        }

        var match = Clusters[nearestIndex];

        if (!TryRestoreClusterBasis(match, hitCollider, out var matchedAnchor, out var matchedNormal, out var matchedAxis))
            return false;

        var surfaceDelta = Vector3.ProjectOnPlane(position - matchedAnchor, matchedNormal);

        if (!match.AxisLocked && surfaceDelta.sqrMagnitude >= AxisLockDistanceSquared)
        {
            var previousEnvelope = GetClusterEnvelope(match);
            matchedAxis = surfaceDelta.normalized;
            match.Axis = StoreClusterDirection(match, hitCollider, matchedAxis);
            match.AxisLocked = true;
            match.MinAlong = -previousEnvelope;
            match.MaxAlong = previousEnvelope;
            match.MinAcross = -previousEnvelope;
            match.MaxAcross = previousEnvelope;
        }

        var matchedAcross = Vector3.Cross(matchedNormal, matchedAxis).normalized;

        if (!match.AxisLocked)
        {
            var envelope = Mathf.Max(GetClusterEnvelope(match), surfaceDelta.magnitude + shotRadius);
            match.MinAlong = -envelope;
            match.MaxAlong = envelope;
            match.MinAcross = -envelope;
            match.MaxAcross = envelope;
        }
        else
        {
            var alongDistance = Vector3.Dot(surfaceDelta, matchedAxis);
            var acrossDistance = Vector3.Dot(surfaceDelta, matchedAcross);
            match.MinAlong = Mathf.Min(match.MinAlong, alongDistance - shotRadius);
            match.MaxAlong = Mathf.Max(match.MaxAlong, alongDistance + shotRadius);
            match.MinAcross = Mathf.Min(match.MinAcross, acrossDistance - shotRadius);
            match.MaxAcross = Mathf.Max(match.MaxAcross, acrossDistance + shotRadius);
        }

        match.MaximumRadius = Mathf.Max(match.MaximumRadius, shotRadius);
        match.LastTime = now;
        match.Hits = Mathf.Max(1, match.Hits) + 1;

        var middleU = 0.5f * (match.MinAlong + match.MaxAlong);
        var middleV = 0.5f * (match.MinAcross + match.MaxAcross);
        var halfU = 0.5f * (match.MaxAlong - match.MinAlong);
        var halfV = 0.5f * (match.MaxAcross - match.MinAcross);
        var renderRadius = Mathf.Max(MinimumRadius, match.MaximumRadius);

        center = matchedAnchor + matchedAxis * middleU + matchedAcross * middleV;
        compoundNormal = matchedNormal;
        axis = matchedAxis;
        lengthMultiplier = Mathf.Max(MinimumRadius, halfU) / renderRadius;
        widthMultiplier = Mathf.Max(MinimumRadius, halfV) / renderRadius;
        sizeMultiplier = renderRadius / rawRadius;
        hits = match.Hits;
        clusterIndex = nearestIndex;

        Clusters[nearestIndex] = match;

        return true;
    }

    private static bool TryRestoreClusterBasis(
        ImpactCluster cluster,
        BallisticCollider hitCollider,
        out Vector3 anchor,
        out Vector3 normal,
        out Vector3 axis)
    {
        if (cluster.UsesColliderSpace)
        {
            if (hitCollider == null)
            {
                anchor = default;
                normal = default;
                axis = default;
                return false;
            }

            anchor = hitCollider.transform.TransformPoint(cluster.Anchor);
            normal = hitCollider.transform.TransformDirection(cluster.Normal).normalized;
            axis = hitCollider.transform.TransformDirection(cluster.Axis);
        }
        else
        {
            anchor = cluster.Anchor;
            normal = cluster.Normal.normalized;
            axis = cluster.Axis;
        }

        axis = Vector3.ProjectOnPlane(axis, normal);

        if (normal.sqrMagnitude < 0.000001f || axis.sqrMagnitude < 0.000001f)
            return false;

        axis.Normalize();
        return true;
    }

    private static Vector3 StoreClusterDirection(
        ImpactCluster cluster,
        BallisticCollider hitCollider,
        Vector3 worldDirection)
    {
        return cluster.UsesColliderSpace && hitCollider != null
            ? hitCollider.transform.InverseTransformDirection(worldDirection).normalized
            : worldDirection.normalized;
    }

    private static float GetClusterEnvelope(ImpactCluster cluster)
    {
        return Mathf.Max(
            Mathf.Max(Mathf.Abs(cluster.MinAlong), Mathf.Abs(cluster.MaxAlong)),
            Mathf.Max(Mathf.Abs(cluster.MinAcross), Mathf.Abs(cluster.MaxAcross))
        );
    }

    private static Vector3 GetSurfaceAxis(Vector3 direction, Vector3 normal)
    {
        var surfaceNormal = normal.normalized;
        var axis = Vector3.ProjectOnPlane(direction, surfaceNormal);

        if (axis.sqrMagnitude >= 0.000001f)
            return axis.normalized;

        var reference = Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) > 0.95f
            ? Vector3.right
            : Vector3.up;

        return Vector3.Cross(surfaceNormal, reference).normalized;
    }

    private static void RegisterReplacement(Vector3 position, string kind)
    {
        ReplacedImpactPositions[_nextReplacement] = position;
        _nextReplacement = (_nextReplacement + 1) % ReplacementCapacity;
        RuntimeDebugTrace.Write($"registered {kind} replacement at {position.ToString("F4")}");
    }

    public static bool TryConsumeStandardDecalReplacement(Vector3 position)
    {
        for (var i = 0; i < ReplacementCapacity; i++)
        {
            if (ReplacedImpactPositions[i] != position)
                continue;

            ReplacedImpactPositions[i] = Vector3.positiveInfinity;
            RuntimeDebugTrace.Write($"suppressed round decal at {position.ToString("F4")}");

            return true;
        }

        return false;
    }

    public static void Clear()
    {
        Array.Fill(ReplacedImpactPositions, Vector3.positiveInfinity);
        Array.Clear(Clusters, 0, Clusters.Length);
        _nextReplacement = 0;
        _nextCluster = 0;
    }
}
