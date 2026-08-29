using System;
using System.Collections.Generic;
using EFT.Ballistics;
using HollywoodFX.Gore;
using Systems.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollywoodFX.Decal;

internal static class PenetrationApertures
{
    private static readonly PenetrationApertureTracker Tracker = new();
    private static PenetrationApertureRenderer _renderer;

    internal static void Initialize(Effects effects)
    {
        Clear();
        if (effects == null)
            return;

        var host = new GameObject("HFX Penetration Apertures")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        host.transform.SetParent(effects.transform, false);
        _renderer = host.AddComponent<PenetrationApertureRenderer>();
        _renderer.Initialize();
    }

    internal static void TryDraw(ImpactKinetics kinetics, BallisticCollider hitCollider)
    {
        var shot = kinetics?.Bullet?.Info;
        if (_renderer == null || shot == null || hitCollider == null ||
            kinetics.Normal.sqrMagnitude < 0.000001f ||
            shot.CurrentDirection.sqrMagnitude < 0.000001f)
            return;

        var hasActorOwner = BodyTargetClassifier.IsBodyTarget(
            hitCollider.transform, out _);
        if (!CustomImpactGeometryPolicy.ShouldUseCustomGeometry(
                hitCollider is BodyPartCollider, hasActorOwner))
        {
            if (Plugin.DebugLoggingEnabled)
                RuntimeDebugTrace.Write(
                    $"penetration aperture skipped: character-owned collider={hitCollider.name}");
            return;
        }

        var surfaceRenderer = PenetrationApertureRenderer.ResolveSurfaceRenderer(hitCollider);
        if (surfaceRenderer == null)
            return;

        var rootShot = ResolveRootShot(shot);
        var confirmedPenetration = shot.IsForwardHit &&
            !shot.BlockedBy.HasValue && !shot.DeflectedBy.HasValue &&
            shot.BulletState is Shot.EBulletState.Flying or
                Shot.EBulletState.DeviationHit or Shot.EBulletState.FragmentationHit;
        var plan = Tracker.Record(rootShot, shot.IsForwardHit, confirmedPenetration);

        if (!plan.CreateAperture)
            return;

        _renderer.Add(
            plan,
            kinetics.Position,
            kinetics.Normal,
            shot.CurrentDirection,
            shot.BulletDiameterMilimeters,
            hitCollider,
            surfaceRenderer);

        if (Plugin.DebugLoggingEnabled)
        {
            RuntimeDebugTrace.Write(
                $"penetration aperture identity={plan.Identity} pair={plan.PairIdentity} " +
                $"face={plan.Face} position={kinetics.Position.ToString("F4")} " +
                $"collider={hitCollider.name} renderer={surfaceRenderer.name}");
        }
    }

    internal static void Clear()
    {
        Tracker.Clear();
        if (_renderer != null)
        {
            _renderer.Release();
            _renderer = null;
        }
    }

    private static Shot ResolveRootShot(Shot shot)
    {
        var root = shot;
        for (var guard = 0; root.Parent != null && guard < 16; guard++)
            root = root.Parent;
        return root;
    }
}

internal sealed class PenetrationApertureRenderer : MonoBehaviour
{
    private const int Capacity = 64;
    private const int CircleSegments = 32;
    private const float SurfaceOffset = 0.0015f;

    private sealed class ApertureSlot
    {
        internal bool Active;
        internal long Identity;
        internal long PairIdentity;
        internal PenetrationApertureFace Face;
        internal GameObject Root;
        internal GameObject DiskObject;
        internal GameObject RimObject;
        internal Mesh DiskMesh;
        internal Mesh RimMesh;
        internal MeshRenderer DiskRenderer;
        internal MeshRenderer RimRenderer;
        internal Vector3[] DiskVertices;
        internal Vector2[] DiskUvs;
        internal Transform Anchor;
        internal Vector3 LocalPosition;
        internal Vector3 LocalNormal;
        internal Vector3 LocalDirection;
        internal Vector3 WorldPosition;
        internal Vector3 WorldNormal;
        internal Vector3 WorldDirection;
        internal float MinorRadius;
        internal float MajorRadius;
        internal float SurfaceDepth;
        internal Renderer SurfaceRenderer;
    }

    private readonly ApertureSlot[] _slots = new ApertureSlot[Capacity];
    private readonly Dictionary<long, int> _entrySlotsByPair = new();
    private readonly List<Renderer> _disabledRenderers = new(Capacity * 3);
    private int _nextSlot;
    private Camera _captureCamera;
    private RenderTexture _background;
    private Material _portalMaterial;
    private Material _rimMaterial;
    private bool _capturing;

    internal void Initialize()
    {
        var portalShader = Shader.Find("Unlit/Texture");
        var rimShader = Shader.Find("Unlit/Color");
        if (portalShader == null || rimShader == null)
        {
            Plugin.Log.LogError("Penetration apertures disabled: required built-in shaders were not found.");
            enabled = false;
            return;
        }

        _portalMaterial = new Material(portalShader)
        {
            name = "HFX Penetration Aperture Portal",
            hideFlags = HideFlags.HideAndDontSave
        };
        _rimMaterial = new Material(rimShader)
        {
            name = "HFX Penetration Aperture Edge",
            hideFlags = HideFlags.HideAndDontSave,
            color = new Color(0.075f, 0.06f, 0.045f, 1f)
        };

        var cameraObject = new GameObject("HFX Aperture Background Camera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        cameraObject.transform.SetParent(transform, false);
        _captureCamera = cameraObject.AddComponent<Camera>();
        _captureCamera.enabled = false;
    }

    internal static Renderer ResolveSurfaceRenderer(BallisticCollider hitCollider)
    {
        if (hitCollider == null)
            return null;

        var renderer = hitCollider.GetComponent<Renderer>();
        if (renderer != null)
            return renderer;

        renderer = hitCollider.GetComponentInParent<Renderer>();
        if (renderer != null)
            return renderer;

        return hitCollider.GetComponentInChildren<Renderer>();
    }

    internal void Add(
        PenetrationAperturePlan plan,
        Vector3 position,
        Vector3 normal,
        Vector3 direction,
        float diameterMillimeters,
        BallisticCollider hitCollider,
        Renderer surfaceRenderer)
    {
        if (!enabled || surfaceRenderer == null)
            return;

        var surfaceNormal = normal.normalized;
        var shotDirection = direction.normalized;
        if (plan.Face == PenetrationApertureFace.Entry)
        {
            if (Vector3.Dot(surfaceNormal, shotDirection) > 0f)
                surfaceNormal = -surfaceNormal;
        }
        else if (Vector3.Dot(surfaceNormal, shotDirection) < 0f)
        {
            surfaceNormal = -surfaceNormal;
        }

        var incidenceCosine = Mathf.Abs(Vector3.Dot(surfaceNormal, shotDirection));
        PenetrationApertureGeometry.ResolveRadii(
            diameterMillimeters,
            incidenceCosine,
            out var minorRadius,
            out var majorRadius);

        var slotIndex = AcquireSlot();
        var slot = _slots[slotIndex];
        EnsureSlotObjects(slot, slotIndex);

        slot.Active = true;
        slot.Identity = plan.Identity;
        slot.PairIdentity = plan.PairIdentity;
        slot.Face = plan.Face;
        slot.SurfaceRenderer = surfaceRenderer;
        slot.MinorRadius = minorRadius;
        slot.MajorRadius = majorRadius;
        slot.SurfaceDepth = SurfaceOffset;

        var physicsCollider = hitCollider.GetComponent<Collider>();
        var attachedRigidbody = physicsCollider != null
            ? physicsCollider.attachedRigidbody
            : hitCollider.GetComponentInParent<Rigidbody>();
        var anchor = !hitCollider.gameObject.isStatic
            ? attachedRigidbody != null
                ? attachedRigidbody.transform
                : hitCollider.transform
            : null;
        slot.Anchor = anchor;
        if (anchor != null)
        {
            slot.LocalPosition = anchor.InverseTransformPoint(position);
            slot.LocalNormal = anchor.InverseTransformDirection(surfaceNormal).normalized;
            slot.LocalDirection = anchor.InverseTransformDirection(shotDirection).normalized;
        }
        else
        {
            slot.WorldPosition = position;
            slot.WorldNormal = surfaceNormal;
            slot.WorldDirection = shotDirection;
        }

        slot.Root.name = $"HFX Aperture {plan.PairIdentity}:{plan.Face}:{plan.Identity}";
        slot.Root.layer = surfaceRenderer.gameObject.layer;
        slot.DiskObject.layer = slot.Root.layer;
        slot.RimObject.layer = slot.Root.layer;
        slot.Root.SetActive(true);
        UpdateWorldGeometry(slot);

        if (plan.Face == PenetrationApertureFace.Entry)
        {
            _entrySlotsByPair[plan.PairIdentity] = slotIndex;
        }
        else if (_entrySlotsByPair.TryGetValue(plan.PairIdentity, out var entryIndex))
        {
            var entry = _slots[entryIndex];
            if (entry != null && entry.Active && entry.PairIdentity == plan.PairIdentity)
            {
                UpdateWorldGeometry(entry);
                var depth = Vector3.Distance(entry.WorldPosition, slot.WorldPosition);
                entry.SurfaceDepth = depth;
                slot.SurfaceDepth = depth;
                ApplySlotTransform(entry);
                ApplySlotTransform(slot);
            }
            _entrySlotsByPair.Remove(plan.PairIdentity);
        }
    }

    internal void Release()
    {
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private int AcquireSlot()
    {
        var index = _nextSlot;
        _nextSlot = (_nextSlot + 1) % Capacity;
        var existing = _slots[index];
        if (existing != null && existing.Active)
        {
            if (existing.Face == PenetrationApertureFace.Entry &&
                _entrySlotsByPair.TryGetValue(existing.PairIdentity, out var mapped) &&
                mapped == index)
                _entrySlotsByPair.Remove(existing.PairIdentity);
            existing.Active = false;
            existing.Root.SetActive(false);
        }

        if (_slots[index] == null)
            _slots[index] = new ApertureSlot();
        return index;
    }

    private void EnsureSlotObjects(ApertureSlot slot, int index)
    {
        if (slot.Root != null)
            return;

        slot.Root = new GameObject($"HFX Aperture Pool {index}")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        slot.Root.transform.SetParent(transform, false);

        slot.DiskObject = new GameObject("Portal")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        slot.DiskObject.transform.SetParent(slot.Root.transform, false);
        var diskFilter = slot.DiskObject.AddComponent<MeshFilter>();
        slot.DiskRenderer = slot.DiskObject.AddComponent<MeshRenderer>();
        slot.DiskMesh = BuildDiskMesh(out slot.DiskVertices, out slot.DiskUvs);
        diskFilter.sharedMesh = slot.DiskMesh;
        ConfigureRenderer(slot.DiskRenderer, _portalMaterial);

        slot.RimObject = new GameObject("Edge")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        slot.RimObject.transform.SetParent(slot.Root.transform, false);
        var rimFilter = slot.RimObject.AddComponent<MeshFilter>();
        slot.RimRenderer = slot.RimObject.AddComponent<MeshRenderer>();
        slot.RimMesh = BuildRimMesh();
        rimFilter.sharedMesh = slot.RimMesh;
        ConfigureRenderer(slot.RimRenderer, _rimMaterial);
    }

    private static void ConfigureRenderer(MeshRenderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void LateUpdate()
    {
        if (_capturing || _captureCamera == null || _portalMaterial == null)
            return;

        var cameraManager = EFT.CameraControl.CameraManager.Instance;
        var mainCamera = cameraManager != null ? cameraManager.Camera : null;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            return;

        var anyVisible = false;
        for (var i = 0; i < Capacity; i++)
        {
            var slot = _slots[i];
            if (slot == null || !slot.Active)
                continue;

            if (slot.SurfaceRenderer == null ||
                slot.Anchor != null && !slot.Anchor.gameObject.activeInHierarchy)
            {
                RecycleSlot(i);
                continue;
            }

            UpdateWorldGeometry(slot);
            UpdatePortalUvs(slot, mainCamera);
            var viewport = mainCamera.WorldToViewportPoint(slot.WorldPosition);
            if (viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f &&
                viewport.y > -0.1f && viewport.y < 1.1f)
                anyVisible = true;
        }

        if (anyVisible)
            CaptureBackground(mainCamera);
    }

    private void UpdateWorldGeometry(ApertureSlot slot)
    {
        if (slot.Anchor != null)
        {
            slot.WorldPosition = slot.Anchor.TransformPoint(slot.LocalPosition);
            slot.WorldNormal = slot.Anchor.TransformDirection(slot.LocalNormal).normalized;
            slot.WorldDirection = slot.Anchor.TransformDirection(slot.LocalDirection).normalized;
        }
        ApplySlotTransform(slot);
    }

    private static void ApplySlotTransform(ApertureSlot slot)
    {
        var along = Vector3.ProjectOnPlane(slot.WorldDirection, slot.WorldNormal);
        if (along.sqrMagnitude < 0.000001f)
            along = Vector3.Cross(slot.WorldNormal, Vector3.up);
        if (along.sqrMagnitude < 0.000001f)
            along = Vector3.Cross(slot.WorldNormal, Vector3.right);
        along.Normalize();
        var across = Vector3.Cross(slot.WorldNormal, along).normalized;
        var rotation = Quaternion.LookRotation(slot.WorldNormal, across);

        slot.Root.transform.SetPositionAndRotation(
            slot.WorldPosition + slot.WorldNormal * SurfaceOffset,
            rotation);
        slot.DiskObject.transform.localPosition = Vector3.zero;
        slot.DiskObject.transform.localRotation = Quaternion.identity;
        slot.DiskObject.transform.localScale = new Vector3(
            slot.MajorRadius,
            slot.MinorRadius,
            1f);

        var bevelDepth = Mathf.Clamp(slot.SurfaceDepth * 0.08f, 0.0015f, 0.008f);
        slot.RimObject.transform.localPosition = Vector3.zero;
        slot.RimObject.transform.localRotation = Quaternion.identity;
        slot.RimObject.transform.localScale = new Vector3(
            slot.MajorRadius,
            slot.MinorRadius,
            bevelDepth);
    }

    private static void UpdatePortalUvs(ApertureSlot slot, Camera camera)
    {
        for (var i = 0; i < slot.DiskVertices.Length; i++)
        {
            var world = slot.DiskObject.transform.TransformPoint(slot.DiskVertices[i]);
            var viewport = camera.WorldToViewportPoint(world);
            var y = SystemInfo.graphicsUVStartsAtTop ? 1f - viewport.y : viewport.y;
            slot.DiskUvs[i] = new Vector2(viewport.x, y);
        }
        slot.DiskMesh.uv = slot.DiskUvs;
    }

    private void CaptureBackground(Camera mainCamera)
    {
        EnsureRenderTexture(mainCamera);
        if (_background == null)
            return;

        _disabledRenderers.Clear();
        _capturing = true;
        try
        {
            for (var i = 0; i < Capacity; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.Active)
                    continue;
                DisableForCapture(slot.SurfaceRenderer);
                DisableForCapture(slot.DiskRenderer);
                DisableForCapture(slot.RimRenderer);
            }

            _captureCamera.CopyFrom(mainCamera);
            _captureCamera.transform.SetPositionAndRotation(
                mainCamera.transform.position,
                mainCamera.transform.rotation);
            _captureCamera.targetTexture = _background;
            _captureCamera.enabled = false;
            _captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _captureCamera.Render();
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning("Penetration aperture background capture failed: " + exception.Message);
        }
        finally
        {
            for (var i = 0; i < _disabledRenderers.Count; i++)
                if (_disabledRenderers[i] != null)
                    _disabledRenderers[i].enabled = true;
            _disabledRenderers.Clear();
            _capturing = false;
        }
    }

    private void DisableForCapture(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || _disabledRenderers.Contains(renderer))
            return;
        renderer.enabled = false;
        _disabledRenderers.Add(renderer);
    }

    private void EnsureRenderTexture(Camera mainCamera)
    {
        var sourceWidth = Mathf.Max(1, mainCamera.pixelWidth);
        var sourceHeight = Mathf.Max(1, mainCamera.pixelHeight);
        var width = Mathf.Clamp(sourceWidth / 2, 256, 768);
        var height = Mathf.Max(144, Mathf.RoundToInt(width * (sourceHeight / (float)sourceWidth)));
        if (_background != null && _background.width == width && _background.height == height)
            return;

        if (_background != null)
        {
            _background.Release();
            Destroy(_background);
        }

        _background = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "HFX Penetration Aperture Background",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave
        };
        _background.Create();
        _portalMaterial.mainTexture = _background;
    }

    private void RecycleSlot(int index)
    {
        var slot = _slots[index];
        if (slot == null || !slot.Active)
            return;
        if (slot.Face == PenetrationApertureFace.Entry &&
            _entrySlotsByPair.TryGetValue(slot.PairIdentity, out var mapped) && mapped == index)
            _entrySlotsByPair.Remove(slot.PairIdentity);
        slot.Active = false;
        slot.Root.SetActive(false);
    }

    private static Mesh BuildDiskMesh(out Vector3[] vertices, out Vector2[] uvs)
    {
        vertices = new Vector3[CircleSegments + 2];
        uvs = new Vector2[vertices.Length];
        var triangles = new int[CircleSegments * 3];
        vertices[0] = Vector3.zero;
        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i * Mathf.PI * 2f / CircleSegments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            if (i >= CircleSegments)
                continue;
            var triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i + 2;
        }

        var mesh = new Mesh
        {
            name = "HFX Penetration Aperture Portal Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildRimMesh()
    {
        var vertices = new Vector3[(CircleSegments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[CircleSegments * 6];
        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i * Mathf.PI * 2f / CircleSegments;
            var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices[i * 2] = radial * 1.42f;
            vertices[i * 2 + 1] = radial + Vector3.forward;
            normals[i * 2] = Vector3.forward;
            normals[i * 2 + 1] = (Vector3.forward + radial * -0.45f).normalized;
            if (i >= CircleSegments)
                continue;
            var vertex = i * 2;
            var triangle = i * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }

        var mesh = new Mesh
        {
            name = "HFX Penetration Aperture Edge Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            normals = normals,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        for (var i = 0; i < Capacity; i++)
        {
            var slot = _slots[i];
            if (slot == null)
                continue;
            if (slot.DiskMesh != null)
                Destroy(slot.DiskMesh);
            if (slot.RimMesh != null)
                Destroy(slot.RimMesh);
        }

        if (_background != null)
        {
            _background.Release();
            Destroy(_background);
        }
        if (_portalMaterial != null)
            Destroy(_portalMaterial);
        if (_rimMaterial != null)
            Destroy(_rimMaterial);

        _entrySlotsByPair.Clear();
        _disabledRenderers.Clear();
    }
}
