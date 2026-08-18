namespace HollywoodFX.Gore;

internal static class GoreEligibilityPolicy
{
    internal static bool ShouldEmitGore(bool materialLooksLikeBody, bool hasPlayerOrCorpseOwner)
    {
        return materialLooksLikeBody && hasPlayerOrCorpseOwner;
    }
}
