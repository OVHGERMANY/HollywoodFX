using Comfort.Common;
using EFT.Ballistics;
using HollywoodFX.Gore;
using UnityEngine;

namespace HollywoodFX.Decal;

/*
 * Logical face armor and some helmets are resolved through a moving body-part collider rather than a
 * renderer-specific ballistic collider. EFT can damage that armor without ever queueing a visible decal on
 * the outer mesh. Draw one shallow dynamic projector at the same collision point and parent it to the exact
 * collider. This is deliberately separate from penetration apertures: it paints a mark but never cuts or
 * guesses a character renderer.
 */
internal static class CharacterImpactMarks
{
    private const int DiagnosticLimit = 8;
    private static int _diagnosticCount;

    internal static bool TryDraw(ImpactKinetics kinetics, BallisticCollider hitCollider)
    {
        var shot = kinetics?.Bullet?.Info;
        var hasOwner = hitCollider != null &&
                       BodyTargetClassifier.IsBodyTarget(hitCollider.transform, out _);
        var validGeometry = kinetics != null &&
                            kinetics.Normal.sqrMagnitude >= 0.000001f &&
                            shot != null && shot.CurrentDirection.sqrMagnitude >= 0.000001f;
        var materialLooksLikeCharacterSurface = kinetics != null &&
            kinetics.Material is MaterialType.Body or MaterialType.BodyArmor or MaterialType.Helmet or
                MaterialType.HelmetRicochet or MaterialType.GlassVisor;

        if (!CharacterImpactMarkPolicy.ShouldDraw(
                hitCollider is BodyPartCollider,
                materialLooksLikeCharacterSurface,
                hasOwner,
                hitCollider != null && !hitCollider.gameObject.isStatic,
                shot != null,
                validGeometry))
            return false;

        var painter = Singleton<DecalPainter>.Instance;
        var decal = Decals.TracerScorchMark;
        if (painter == null || decal == null)
        {
            Report(false, kinetics, hitCollider, "painter-or-decal-unavailable");
            return false;
        }

        var surfaceNormal = kinetics.Normal.normalized;
        var referenceAxis = Mathf.Abs(surfaceNormal.y) < 0.9f ? Vector3.up : Vector3.right;
        var surfaceTangent = Vector3.Cross(surfaceNormal, referenceAxis).normalized;
        var position = kinetics.Position + surfaceNormal * CharacterImpactMarkPolicy.SurfaceOffset;
        var scale = CharacterImpactMarkPolicy.ResolveScale(
            BulletHoles.GetForwardScale(shot, includeVariance: true));

        var drawn = painter.DrawOrientedDecal(
            decal,
            position,
            surfaceNormal,
            hitCollider,
            surfaceTangent,
            lengthMultiplier: 1f,
            widthMultiplier: 1f,
            sizeMultiplier: scale,
            lockFirstTile: true,
            projectorHeight: CharacterImpactMarkPolicy.ProjectorHeight);

        if (drawn)
            SurfaceImpactMarks.RegisterReplacement(kinetics.Position, "character-attached");

        Report(drawn, kinetics, hitCollider, drawn ? "attached" : "dynamic-projector-rejected");
        return drawn;
    }

    internal static void Clear()
    {
        _diagnosticCount = 0;
    }

    private static void Report(
        bool drawn,
        ImpactKinetics kinetics,
        BallisticCollider hitCollider,
        string reason)
    {
        if (_diagnosticCount >= DiagnosticLimit)
            return;

        _diagnosticCount++;
        Plugin.Log.LogInfo(
            $"Character impact mark drawn={drawn}, reason={reason}, " +
            $"material={kinetics?.Material}, collider={hitCollider?.name ?? "null"}");
    }
}
