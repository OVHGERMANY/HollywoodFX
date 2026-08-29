namespace HollywoodFX.Decal;

internal static class BloodDecalPresentation
{
    internal const float DefaultSizeMultiplier = 0.65f;
    internal const float BleedingDecalWidthMetres = 0.125f;
    internal const float BleedingDecalHeightMetres = 0.175f;

    internal const float MatteGlossiness = 0.06f;
    internal const float MatteNormalPower = 1f;
    internal const float DisabledSurfaceResponse = 0f;

    internal const float AbsorbedTintRedMultiplier = 0.68f;
    internal const float AbsorbedTintGreenMultiplier = 0.52f;
    internal const float AbsorbedTintBlueMultiplier = 0.48f;

    internal static void ResolveBleedingDecalSize(float sizeMultiplier, out float width, out float height)
    {
        width = BleedingDecalWidthMetres * sizeMultiplier;
        height = BleedingDecalHeightMetres * sizeMultiplier;
    }

    internal static float ResolveEnvironmentDecalScale(float sizeMultiplier)
    {
        return sizeMultiplier;
    }

    internal static void ResolveAbsorbedTint(
        float red,
        float green,
        float blue,
        float alpha,
        out float tintedRed,
        out float tintedGreen,
        out float tintedBlue,
        out float preservedAlpha)
    {
        tintedRed = red * AbsorbedTintRedMultiplier;
        tintedGreen = green * AbsorbedTintGreenMultiplier;
        tintedBlue = blue * AbsorbedTintBlueMultiplier;
        preservedAlpha = alpha;
    }
}
