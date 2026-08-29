namespace HollywoodFX.Decal;

internal static class CharacterImpactMarkPolicy
{
    internal const float BaseScale = 0.032f;
    internal const float MinimumScale = 0.024f;
    internal const float MaximumScale = 0.055f;
    internal const float SurfaceOffset = 0.012f;
    internal const float ProjectorHeight = 0.06f;

    internal static bool ShouldDraw(
        bool isBodyPartCollider,
        bool materialLooksLikeCharacterSurface,
        bool hasPlayerOrCorpseOwner,
        bool isDynamicCollider,
        bool hasShot,
        bool hasValidGeometry)
    {
        return hasPlayerOrCorpseOwner &&
               isDynamicCollider &&
               hasShot &&
               hasValidGeometry &&
               (isBodyPartCollider || materialLooksLikeCharacterSurface);
    }

    internal static float ResolveScale(float configuredCaliberScale)
    {
        var scale = BaseScale * configuredCaliberScale;
        if (scale < MinimumScale)
            return MinimumScale;
        if (scale > MaximumScale)
            return MaximumScale;
        return scale;
    }
}
