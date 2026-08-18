using EFT;
using EFT.Interactive;
using UnityEngine;

namespace HollywoodFX.Gore;

internal static class BodyTargetClassifier
{
    internal static bool IsBodyTarget(Transform root, out string owner)
    {
        owner = "none";
        if (root == null)
            return false;

        if (root.GetComponentInParent<Player>() != null)
        {
            owner = "player";
            return true;
        }

        if (root.GetComponentInParent<Corpse>() != null)
        {
            owner = "corpse";
            return true;
        }

        return false;
    }

    internal static bool ShouldEmitGore(bool materialLooksLikeBody, Transform root, out string owner)
    {
        bool hasOwner = IsBodyTarget(root, out owner);
        return GoreEligibilityPolicy.ShouldEmitGore(materialLooksLikeBody, hasOwner);
    }
}
