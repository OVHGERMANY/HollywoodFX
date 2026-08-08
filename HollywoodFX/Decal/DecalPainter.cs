using System.Collections.Generic;
using DeferredDecals;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;

namespace HollywoodFX.Decal;

public class DecalPainter
{
    private readonly DeferredDecalRenderer _renderer;

    private readonly Dictionary<Material, DeferredDecalRenderer.ManagedMesh> _dictionary0;
    private readonly Dictionary<Camera, DeferredDecalRenderer.CameraData> _dictionary2;
    
    public DecalPainter(DeferredDecalRenderer renderer)
    {
        _renderer = renderer;
        var traverse = Traverse.Create(_renderer);
        _dictionary0 = traverse.Field("_meshesDict").GetValue<Dictionary<Material, DeferredDecalRenderer.ManagedMesh>>();
        _dictionary2 = traverse.Field("_cameras").GetValue<Dictionary<Camera, DeferredDecalRenderer.CameraData>>();
    }

    public void DrawDecal(
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 normal,
        BallisticCollider hitCollider,
        float projectorHeight=0.1f)
    {
        if (!_dictionary0.ContainsKey(decal.DecalMaterial))
        {
            foreach (var keyValuePair in _dictionary2)
                keyValuePair.Value.IsStaticBufferDirty = true;
            _renderer.CreateDecalMesh(decal);
        }
        _renderer.AddCubeToMesh(position, normal, _dictionary0[decal.DecalMaterial], decal, projectorHeight);
    }
}