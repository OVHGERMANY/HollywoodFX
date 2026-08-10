using System.Collections.Generic;
using DeferredDecals;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;

namespace HollywoodFX.Decal;

public class DecalPainter
{
    private const int VerticesPerDecal = 24;

    public struct OrientedDecalHandle
    {
        internal bool IsValid;
        internal bool IsDynamic;
        internal int RendererInstanceId;
        internal int Index;
        internal int Generation;
        internal Material Material;
        internal DeferredDecalRenderer.SingleDecal Decal;
        internal DeferredDecalRenderer.ManagedMesh StaticMesh;
        internal DynamicDeferredDecalRenderer DynamicProjector;
        internal Material RuntimeMaterial;
        internal int SphereIndex;
        internal int ColliderId;
        internal Vector3 LocalPosition;
    }

    private readonly DeferredDecalRenderer _renderer;
    private readonly Traverse _rendererTraverse;

    private readonly Dictionary<Material, DeferredDecalRenderer.ManagedMesh> _dictionary0;
    private readonly Dictionary<Camera, DeferredDecalRenderer.CameraData> _dictionary2;
    private readonly Dictionary<DeferredDecalRenderer.ManagedMesh, int[]> _staticSlotGenerations = new();
    private readonly Dictionary<DynamicDeferredDecalRenderer, int> _dynamicSlotGenerations = new();

    private readonly Vector3[] _newDecalVerts;
    private readonly Vector3[] _newDecalNormals;
    private readonly Vector4[] _newDecalTangents;
    private readonly Vector4[] _decalUv0Right;
    private readonly Vector4[] _decalUv1Up;
    private readonly Vector4[] _decalUv2Fwd;

    private int _nextGeneration;
    
    public DecalPainter(DeferredDecalRenderer renderer)
    {
        _renderer = renderer;
        _rendererTraverse = Traverse.Create(_renderer);
        _dictionary0 = _rendererTraverse.Field("_meshesDict").GetValue<Dictionary<Material, DeferredDecalRenderer.ManagedMesh>>();
        _dictionary2 = _rendererTraverse.Field("_cameras").GetValue<Dictionary<Camera, DeferredDecalRenderer.CameraData>>();
        _newDecalVerts = _rendererTraverse.Field("_newDecalVerts").GetValue<Vector3[]>();
        _newDecalNormals = _rendererTraverse.Field("_newDecalNormals").GetValue<Vector3[]>();
        _newDecalTangents = _rendererTraverse.Field("_newDecalTangents").GetValue<Vector4[]>();
        _decalUv0Right = _rendererTraverse.Field("_decalUv0Right").GetValue<Vector4[]>();
        _decalUv1Up = _rendererTraverse.Field("_decalUv1Up").GetValue<Vector4[]>();
        _decalUv2Fwd = _rendererTraverse.Field("_decalUv2Fwd").GetValue<Vector4[]>();
    }

    public void DrawDecal(
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 normal,
        BallisticCollider hitCollider,
        float projectorHeight=0.1f)
    {
        var mesh = GetOrCreateStaticMesh(decal);
        _renderer.AddCubeToMesh(position, normal, mesh, decal, projectorHeight);
    }

    public DeferredDecalRenderer.SingleDecal GetBulletDecal(BallisticCollider hitCollider)
    {
        return _renderer.GetSingleDecal(hitCollider, isGrenade: false);
    }

    public void ObserveVanillaStaticWrite(DeferredDecalRenderer.ManagedMesh mesh)
    {
        var maxDecals = _rendererTraverse.Field("_maxDecals").GetValue<int>();

        if (mesh == null || maxDecals <= 0)
            return;

        var index = _rendererTraverse.Field("_currentDecalIndex").GetValue<int>() % maxDecals;
        ClaimStaticSlot(mesh, index);
    }

    public void ObserveVanillaDynamicWrite()
    {
        var dynamicDecals = _rendererTraverse.Field("_dynamicDecals").GetValue<List<DynamicDeferredDecalRenderer>>();

        if (dynamicDecals == null || dynamicDecals.Count == 0)
            return;

        var index = _rendererTraverse.Field("_currentDynamicDecalIndex").GetValue<int>() % dynamicDecals.Count;
        ClaimDynamicSlot(dynamicDecals[index]);
    }

    public bool DrawOrientedDecal(
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 normal,
        BallisticCollider hitCollider,
        Vector3 surfaceDirection,
        float lengthMultiplier,
        float widthMultiplier,
        float sizeMultiplier = 1f,
        bool lockFirstTile = false,
        float projectorHeight = 0.1f)
    {
        var handle = default(OrientedDecalHandle);

        return DrawOrUpdateOrientedDecal(
            ref handle,
            decal,
            position,
            normal,
            hitCollider,
            surfaceDirection,
            lengthMultiplier,
            widthMultiplier,
            sizeMultiplier,
            lockFirstTile,
            projectorHeight
        );
    }

    public bool DrawOrUpdateOrientedDecal(
        ref OrientedDecalHandle handle,
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 normal,
        BallisticCollider hitCollider,
        Vector3 surfaceDirection,
        float lengthMultiplier,
        float widthMultiplier,
        float sizeMultiplier = 1f,
        bool lockFirstTile = false,
        float projectorHeight = 0.1f)
    {
        if (decal == null || decal.DecalMaterial == null || normal.sqrMagnitude < 0.000001f)
            return false;

        var surfaceNormal = normal.normalized;
        var alongSurface = Vector3.ProjectOnPlane(surfaceDirection, surfaceNormal);

        if (alongSurface.sqrMagnitude < 0.000001f)
            return false;

        alongSurface.Normalize();
        var acrossSurface = Vector3.Cross(surfaceNormal, alongSurface).normalized;

        var minSize = Mathf.Min(decal.DecalSize.x, decal.DecalSize.y);
        var maxSize = Mathf.Max(decal.DecalSize.x, decal.DecalSize.y);
        var baseSize = (lockFirstTile ? (minSize + maxSize) * 0.5f : Random.Range(minSize, maxSize)) *
                       Mathf.Max(0.01f, sizeMultiplier);
        var boxSize = new Vector3(
            Mathf.Max(0.001f, baseSize * lengthMultiplier),
            Mathf.Max(0.001f, projectorHeight),
            Mathf.Max(0.001f, baseSize * widthMultiplier)
        );

        RuntimeDebugTrace.Write(
            $"oriented projector material={decal.DecalMaterial.name} " +
            $"baseSize={baseSize:0.###} box={boxSize.ToString("F4")} " +
            $"along={alongSurface.ToString("F4")} across={acrossSurface.ToString("F4")} " +
            $"normal={surfaceNormal.ToString("F4")}"
        );

        if (hitCollider != null && !hitCollider.gameObject.isStatic)
        {
            return DrawOrientedDynamicDecal(
                decal,
                position,
                surfaceNormal,
                hitCollider,
                alongSurface,
                acrossSurface,
                boxSize,
                lockFirstTile,
                ref handle
            );
        }

        var maxDecals = _rendererTraverse.Field("_maxDecals").GetValue<int>();

        if (maxDecals <= 0)
            return false;

        var mesh = GetOrCreateStaticMesh(decal);
        DeferredDecalMeshHelper.GenerateVerts(
            _newDecalVerts, position, boxSize, alongSurface, surfaceNormal, acrossSurface
        );
        GenerateTangents(decal, lockFirstTile);

        var normalSize = boxSize;
        var uv0 = new Vector4(alongSurface.x, alongSurface.y, alongSurface.z, position.x);
        var uv1 = new Vector4(surfaceNormal.x, surfaceNormal.y, surfaceNormal.z, position.y);
        var uv2 = new Vector4(acrossSurface.x, acrossSurface.y, acrossSurface.z, position.z);

        for (var i = 0; i < VerticesPerDecal; i++)
        {
            _newDecalNormals[i] = normalSize;
            _decalUv0Right[i] = uv0;
            _decalUv1Up[i] = uv1;
            _decalUv2Fwd[i] = uv2;
        }

        var reused = CanReuseStaticProjector(handle, decal, mesh);
        var currentDecalIndex = reused
            ? handle.Index
            : _rendererTraverse.Field("_currentDecalIndex").GetValue<int>();

        mesh.PasteProjectorIntoMiddle(
            currentDecalIndex * VerticesPerDecal,
            _newDecalVerts,
            _newDecalTangents,
            _newDecalNormals,
            _decalUv0Right,
            _decalUv1Up,
            _decalUv2Fwd
        );

        var generation = handle.Generation;

        if (!reused)
        {
            generation = ClaimStaticSlot(mesh, currentDecalIndex);
            _rendererTraverse.Field("_currentDecalIndex").SetValue((currentDecalIndex + 1) % maxDecals);
        }

        handle = new OrientedDecalHandle
        {
            IsValid = true,
            IsDynamic = false,
            RendererInstanceId = _renderer.GetInstanceID(),
            Index = currentDecalIndex,
            Generation = generation,
            Material = decal.DecalMaterial,
            Decal = decal,
            StaticMesh = mesh,
            ColliderId = 0,
        };

        RuntimeDebugTrace.Write(
            $"oriented static projector action={(reused ? "updated" : "allocated")} " +
            $"index={currentDecalIndex} generation={generation}"
        );

        return true;
    }

    private bool DrawOrientedDynamicDecal(
        DeferredDecalRenderer.SingleDecal decal,
        Vector3 position,
        Vector3 surfaceNormal,
        BallisticCollider hitCollider,
        Vector3 alongSurface,
        Vector3 acrossSurface,
        Vector3 boxSize,
        bool lockFirstTile,
        ref OrientedDecalHandle handle)
    {
        var dynamicDecals = _rendererTraverse.Field("_dynamicDecals").GetValue<List<DynamicDeferredDecalRenderer>>();
        var boundingSpheres = _rendererTraverse.Field("_dynamicDecalsBoundingSpheres").GetValue<BoundingSphere[]>();
        var cube = _rendererTraverse.Field("_cube").GetValue<Mesh>();

        if (dynamicDecals == null || dynamicDecals.Count == 0 || boundingSpheres == null)
            return false;

        var reused = CanReuseDynamicProjector(
            handle, decal, hitCollider, dynamicDecals, boundingSpheres.Length
        );
        var currentIndex = reused
            ? handle.Index
            : _rendererTraverse.Field("_currentDynamicDecalIndex").GetValue<int>() % dynamicDecals.Count;

        var dynamicDecal = dynamicDecals[currentIndex];
        var gameObject = dynamicDecal.gameObject;
        var transformHelper = dynamicDecal.TransformHelper;
        var sphereIndex = reused ? handle.SphereIndex : currentIndex;

        if (gameObject == null || sphereIndex < 0 || sphereIndex >= boundingSpheres.Length)
            return false;

        dynamicDecal.CullingGroupSphereIndex = sphereIndex;

        var halfHeight = boxSize.y * 0.5f;
        var radius = Mathf.Sqrt(
            boxSize.x * boxSize.x + boxSize.z * boxSize.z + halfHeight * halfHeight
        );
        boundingSpheres[sphereIndex] = new BoundingSphere(position, radius);

        var rotation = Quaternion.LookRotation(-acrossSurface, surfaceNormal);
        var decalTransform = gameObject.transform;
        decalTransform.localScale = new Vector3(boxSize.x * 2f, boxSize.y, boxSize.z * 2f);
        decalTransform.rotation = rotation;
        decalTransform.position = position;

        if (transformHelper != null)
        {
            transformHelper.position = position;
            transformHelper.rotation = rotation;
            transformHelper.parent = hitCollider.transform;
        }

        dynamicDecal.enabled = true;

        var generation = handle.Generation;

        if (!reused)
        {
            var material = decal.DynamicDecalMaterial != null ? decal.DynamicDecalMaterial : decal.DecalMaterial;
            var uvStartEnd = GetTileUv(decal, lockFirstTile);
            dynamicDecal.Init(material, cube, surfaceNormal, uvStartEnd, decal.IsTiled, sphereIndex);
            generation = ClaimDynamicSlot(dynamicDecal);
            _rendererTraverse.Field("_currentDynamicDecalIndex").SetValue((currentIndex + 1) % dynamicDecals.Count);
        }

        _renderer.MakeDynamicBufferDirty();

        handle = new OrientedDecalHandle
        {
            IsValid = true,
            IsDynamic = true,
            RendererInstanceId = _renderer.GetInstanceID(),
            Index = currentIndex,
            Generation = generation,
            Material = decal.DecalMaterial,
            Decal = decal,
            DynamicProjector = dynamicDecal,
            RuntimeMaterial = dynamicDecal.DecalMaterial,
            SphereIndex = sphereIndex,
            ColliderId = hitCollider.GetInstanceID(),
            LocalPosition = transformHelper == null
                ? hitCollider.transform.InverseTransformPoint(position)
                : transformHelper.localPosition
        };

        RuntimeDebugTrace.Write(
            $"oriented dynamic projector action={(reused ? "updated" : "allocated")} " +
            $"collider={hitCollider.name} index={currentIndex} " +
            $"generation={generation} sphere={sphereIndex} " +
            $"scale={decalTransform.localScale.ToString("F4")} radius={radius:0.###}"
        );

        return true;
    }

    private bool CanReuseStaticProjector(
        OrientedDecalHandle handle,
        DeferredDecalRenderer.SingleDecal decal,
        DeferredDecalRenderer.ManagedMesh mesh)
    {
        if (!handle.IsValid || handle.IsDynamic ||
            handle.RendererInstanceId != _renderer.GetInstanceID() ||
            handle.Index < 0 || handle.Material != decal.DecalMaterial ||
            handle.Decal != decal || handle.StaticMesh != mesh)
            return false;

        var vertexIndex = handle.Index * VerticesPerDecal;

        if (vertexIndex < 0 || vertexIndex >= mesh.VertexCount)
            return false;

        return _staticSlotGenerations.TryGetValue(mesh, out var generations) &&
               handle.Index < generations.Length &&
               generations[handle.Index] == handle.Generation;
    }

    private bool CanReuseDynamicProjector(
        OrientedDecalHandle handle,
        DeferredDecalRenderer.SingleDecal decal,
        BallisticCollider hitCollider,
        List<DynamicDeferredDecalRenderer> dynamicDecals,
        int boundingSphereCount)
    {
        if (!handle.IsValid || !handle.IsDynamic || hitCollider == null ||
            handle.RendererInstanceId != _renderer.GetInstanceID() ||
            handle.Index < 0 || handle.Index >= dynamicDecals.Count ||
            handle.SphereIndex < 0 || handle.SphereIndex >= boundingSphereCount ||
            handle.Material != decal.DecalMaterial || handle.Decal != decal ||
            handle.ColliderId != hitCollider.GetInstanceID())
            return false;

        var dynamicDecal = dynamicDecals[handle.Index];
        var transformHelper = dynamicDecal == null ? null : dynamicDecal.TransformHelper;

        if (dynamicDecal == null || dynamicDecal != handle.DynamicProjector ||
            !dynamicDecal.enabled || dynamicDecal.DecalMaterial != handle.RuntimeMaterial ||
            transformHelper == null || transformHelper.parent != hitCollider.transform)
            return false;

        return _dynamicSlotGenerations.TryGetValue(dynamicDecal, out var generation) &&
               generation == handle.Generation;
    }

    private int ClaimStaticSlot(DeferredDecalRenderer.ManagedMesh mesh, int index)
    {
        var slotCount = mesh.VertexCount / VerticesPerDecal;

        if (!_staticSlotGenerations.TryGetValue(mesh, out var generations) || generations.Length != slotCount)
        {
            generations = new int[slotCount];
            _staticSlotGenerations[mesh] = generations;
        }

        var generation = NextGeneration();

        if (index >= 0 && index < generations.Length)
            generations[index] = generation;

        return generation;
    }

    private int ClaimDynamicSlot(DynamicDeferredDecalRenderer dynamicDecal)
    {
        var generation = NextGeneration();
        _dynamicSlotGenerations[dynamicDecal] = generation;

        return generation;
    }

    private int NextGeneration()
    {
        _nextGeneration++;

        if (_nextGeneration <= 0)
            _nextGeneration = 1;

        return _nextGeneration;
    }

    private void GenerateTangents(DeferredDecalRenderer.SingleDecal decal, bool lockFirstTile)
    {
        if (!lockFirstTile || !decal.IsTiled)
        {
            DeferredDecalMeshHelper.GenerateTangents(_newDecalTangents, decal);
            return;
        }

        var rows = decal.TileSheetRows;
        var columns = decal.TileSheetColumns;

        try
        {
            // TileUSize and TileVSize remain the initialized dimensions of one atlas cell.
            decal.TileSheetRows = 1;
            decal.TileSheetColumns = 1;
            DeferredDecalMeshHelper.GenerateTangents(_newDecalTangents, decal);
        }
        finally
        {
            decal.TileSheetRows = rows;
            decal.TileSheetColumns = columns;
        }
    }

    private static Vector4 GetTileUv(DeferredDecalRenderer.SingleDecal decal, bool lockFirstTile)
    {
        var column = lockFirstTile ? 0 : Random.Range(0, decal.TileSheetColumns);
        var row = lockFirstTile ? 0 : Random.Range(0, decal.TileSheetRows);

        return new Vector4(
            row * decal.TileUSize,
            column * decal.TileVSize,
            row * decal.TileUSize + decal.TileUSize,
            column * decal.TileVSize + decal.TileVSize
        );
    }

    private DeferredDecalRenderer.ManagedMesh GetOrCreateStaticMesh(DeferredDecalRenderer.SingleDecal decal)
    {
        if (_dictionary0.TryGetValue(decal.DecalMaterial, out var mesh))
            return mesh;

        _renderer.CreateDecalMesh(decal);

        foreach (var keyValuePair in _dictionary2)
            keyValuePair.Value.IsStaticBufferDirty = true;

        return _dictionary0[decal.DecalMaterial];
    }
}
