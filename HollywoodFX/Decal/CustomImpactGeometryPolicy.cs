namespace HollywoodFX.Decal;

internal static class CustomImpactGeometryPolicy
{
    internal static bool ShouldUseCustomGeometry(
        bool isBodyPartCollider,
        bool hasPlayerOrCorpseOwner)
    {
        return !isBodyPartCollider && !hasPlayerOrCorpseOwner;
    }
}
