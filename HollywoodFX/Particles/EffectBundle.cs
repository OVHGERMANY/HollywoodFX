using System.Collections.Generic;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HollywoodFX.Impact.Sparks;
using Systems.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Particles;

public class Emitter
{
    private const float MaximumBallisticLifetimeSeconds = 0.9f;
    public readonly ParticleSystem Main;
    private readonly List<SubEmitter> _emitters;
    private ParticleSystem _ballisticSystem;
    private Transform _ballisticTransform;
    private ParticleSystem.MinMaxCurve _startSpeed;
    private ParticleSystem.MinMaxCurve _startLifetime;
    private ParticleSystemSimulationSpace _simulationSpace;
    private Transform _customSimulationSpace;
    private bool _ballisticPrepared;

    public Emitter(ParticleSystem main)
    {
        Main = main;
        _emitters = [];

        BuildEmitters();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Emit(Vector3 position, Vector3 normal, float scale)
    {
        var rotation = Quaternion.LookRotation(normal);

        Main.transform.position = position;
        Main.transform.localScale = new Vector3(scale, scale, scale);
        Main.transform.rotation = rotation;

        Main.Play(true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmitDirect(Vector3 position, Vector3 normal, float scale)
    {
        var rotation = Quaternion.LookRotation(normal);

        Main.transform.position = position;
        Main.transform.localScale = new Vector3(scale, scale, scale);
        Main.transform.rotation = rotation;

        for (var i = 0; i < _emitters.Count; i++)
        {
            var emitter = _emitters[i];

            if (emitter.Chance < 0.99f && Random.Range(0f, 1f) > emitter.Chance)
                continue;

            var system = emitter.ParticleSystem;
            
            var count = emitter.MinCount == emitter.MaxCount ? emitter.MaxCount : Random.Range(emitter.MinCount, emitter.MaxCount);

            system.Emit(count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmitDirect(Vector3 position, Vector3 normal, float scale, int count)
    {
        var rotation = Quaternion.LookRotation(normal);

        Main.transform.position = position;
        Main.transform.localScale = new Vector3(scale, scale, scale);
        Main.transform.rotation = rotation;

        Main.Emit(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EmitBallistic(
        Vector3 position,
        Vector3 surfaceNormal,
        Vector3 axis,
        float scale,
        int count,
        float velocityMultiplier,
        float lifetimeMultiplier,
        float spreadDegrees,
        ref BallisticSparkPrng random)
    {
        if (count <= 0 || _ballisticSystem == null || _ballisticTransform == null)
            return 0;

        _ballisticTransform.position = position;
        _ballisticTransform.localScale = new Vector3(scale, scale, scale);
        _ballisticTransform.rotation = Quaternion.LookRotation(axis);

        var spreadScale = Mathf.Tan(Mathf.Clamp(spreadDegrees, 0f, 75f) * Mathf.Deg2Rad);
        for (var i = 0; i < count; i++)
        {
            var randomVector = NextInsideUnitSphere(ref random);
            var perpendicular = randomVector - axis * Vector3.Dot(randomVector, axis);
            var direction = axis + perpendicular * spreadScale;
            if (direction.sqrMagnitude < 0.000001f)
                direction = axis;
            else
                direction.Normalize();

            var normalComponent = Vector3.Dot(direction, surfaceNormal);
            if (normalComponent < 0f)
            {
                direction -= surfaceNormal * normalComponent;
                if (direction.sqrMagnitude < 0.000001f)
                    direction = surfaceNormal;
                else
                    direction.Normalize();
            }

            var worldVelocity = direction * Mathf.Max(0f, Sample(_startSpeed, ref random) * velocityMultiplier);
            var emitParams = new ParticleSystem.EmitParams
            {
                velocity = ToSimulationVelocity(worldVelocity),
                startLifetime = Mathf.Clamp(
                    Sample(_startLifetime, ref random) * lifetimeMultiplier,
                    0.03f,
                    MaximumBallisticLifetimeSeconds)
            };
            _ballisticSystem.Emit(emitParams, 1);
        }

        return count;
    }

    public bool PrepareBallistic(string effectKey, int emitterIndex)
    {
        if (_ballisticPrepared)
            return _ballisticSystem != null;

        _ballisticPrepared = true;
        _ballisticSystem = ResolveBallisticSystem(Main, effectKey);
        if (_ballisticSystem == null)
        {
            Plugin.Log.LogWarning(
                $"ballistic-spark-leaf invalid effect={effectKey} emitter={Main?.name ?? "<null>"} index={emitterIndex}");
            return false;
        }

        _ballisticTransform = _ballisticSystem.transform;
        var mainModule = _ballisticSystem.main;
        _startSpeed = mainModule.startSpeed;
        _startLifetime = mainModule.startLifetime;
        _simulationSpace = mainModule.simulationSpace;
        _customSimulationSpace = mainModule.customSimulationSpace;
        var renderer = _ballisticSystem.GetComponent<ParticleSystemRenderer>();
        var trails = _ballisticSystem.trails;
        var lights = _ballisticSystem.lights;
        var subEmitters = _ballisticSystem.subEmitters;
        Plugin.Log.LogInfo(
            $"ballistic-spark-leaf effect={effectKey} emitter={Main.name} index={emitterIndex} " +
            $"particleSystem={_ballisticSystem.name} path={BuildHierarchyPath(_ballisticTransform)} " +
            $"rendererEnabled={renderer != null && renderer.enabled} simulationSpace={mainModule.simulationSpace} " +
            $"scalingMode={mainModule.scalingMode} startSize={DescribeCurve(mainModule.startSize)} " +
            $"startSpeed={DescribeCurve(_startSpeed)} startLifetime={DescribeCurve(_startLifetime)} " +
            $"trails={trails.enabled} particleLight={lights.enabled} " +
            $"subEmitters={subEmitters.enabled}:{subEmitters.subEmittersCount}");
        lights.enabled = false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 ToSimulationVelocity(Vector3 worldVelocity)
    {
        if (_simulationSpace == ParticleSystemSimulationSpace.World)
            return worldVelocity;
        if (_simulationSpace == ParticleSystemSimulationSpace.Custom && _customSimulationSpace != null)
            return _customSimulationSpace.InverseTransformDirection(worldVelocity);
        return _ballisticTransform.InverseTransformDirection(worldVelocity);
    }

    private static ParticleSystem ResolveBallisticSystem(ParticleSystem main, string effectKey)
    {
        if (main == null)
            return null;

        var systems = main.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystem selected = null;
        var selectedScore = int.MinValue;
        string selectedPath = null;
        for (var i = 0; i < systems.Length; i++)
        {
            var candidate = systems[i];
            var renderer = candidate.GetComponent<ParticleSystemRenderer>();
            var subEmitters = candidate.subEmitters;
            if (renderer == null || !renderer.enabled ||
                subEmitters.enabled && subEmitters.subEmittersCount > 0)
            {
                continue;
            }

            var score = ScoreBallisticCandidate(candidate, effectKey, main);
            var path = BuildHierarchyPath(candidate.transform);
            if (selected == null || score > selectedScore ||
                score == selectedScore && string.CompareOrdinal(path, selectedPath) < 0)
            {
                selected = candidate;
                selectedScore = score;
                selectedPath = path;
            }
        }

        return selected;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sample(ParticleSystem.MinMaxCurve curve, ref BallisticSparkPrng random)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Lerp(curve.constantMin, curve.constantMax, random.NextFloat01());
            case ParticleSystemCurveMode.Curve:
                return curve.curve == null
                    ? curve.constant
                    : curve.curve.Evaluate(random.NextFloat01()) * curve.curveMultiplier;
            case ParticleSystemCurveMode.TwoCurves:
                if (curve.curveMin == null || curve.curveMax == null)
                    return Mathf.Lerp(curve.constantMin, curve.constantMax, random.NextFloat01());
                var time = random.NextFloat01();
                return Mathf.Lerp(
                    curve.curveMin.Evaluate(time),
                    curve.curveMax.Evaluate(time),
                    random.NextFloat01()) * curve.curveMultiplier;
            default:
                return curve.constant;
        }
    }

    private static Vector3 NextInsideUnitSphere(ref BallisticSparkPrng random)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var candidate = new Vector3(
                random.NextSignedFloat(),
                random.NextSignedFloat(),
                random.NextSignedFloat());
            var squareMagnitude = candidate.sqrMagnitude;
            if (squareMagnitude is > 0.000001f and <= 1f)
                return candidate;
        }

        return new Vector3(random.NextSignedFloat(), random.NextSignedFloat(), 0f);
    }

    private static int ScoreBallisticCandidate(ParticleSystem candidate, string effectKey, ParticleSystem main)
    {
        var name = candidate.name ?? string.Empty;
        var score = candidate == main ? 4 : 8;
        if (name.IndexOf("spark", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 200;
        if (name.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("fleck", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 120;
        }
        if (name.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 40;
        if (!string.IsNullOrEmpty(effectKey) && effectKey.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 &&
            name.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 80;
        }

        var emission = candidate.emission;
        if (emission.enabled)
            score += 16;
        return score;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        var builder = new StringBuilder(transform.name);
        for (var parent = transform.parent; parent != null; parent = parent.parent)
            builder.Insert(0, parent.name + "/");
        return builder.ToString();
    }

    private static string DescribeCurve(ParticleSystem.MinMaxCurve curve)
    {
        return $"{curve.mode}:{curve.constantMin:F3}..{curve.constantMax:F3}:constant={curve.constant:F3}:multiplier={curve.curveMultiplier:F3}";
    }

    public string BallisticEmitterName => Main?.name ?? "<null>";

    public string BallisticParticleSystemName => _ballisticSystem?.name ?? "<invalid>";

    public bool IsBallisticCompatible => _ballisticSystem != null;

    public void ScaleDensity(float density)
    {
        foreach (var subSystem in Main.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleHelpers.ScaleEmissionRate(subSystem, density);
        }

        BuildEmitters();
    }

    public void ScaleLimit(float scaling)
    {
        foreach (var system in Main.GetComponentsInChildren<ParticleSystem>())
        {
            var main = system.main;
            main.maxParticles = (int)(main.maxParticles * scaling);
        }
    }

    public void ScaleLifetime(float scaling)
    {
        foreach (var system in Main.GetComponentsInChildren<ParticleSystem>())
        {
            var main = system.main;
            var lifetime = main.startLifetime;
            lifetime.constant *= scaling;
            lifetime.constantMin *= scaling;
            lifetime.constantMax *= scaling;
            lifetime.curveMultiplier = scaling;
        }
    }

    private void BuildEmitters()
    {
        _emitters.Clear();

        foreach (var subSystem in Main.GetComponentsInChildren<ParticleSystem>())
        {
            var emission = subSystem.emission;

            if (!emission.enabled)
                continue;

            for (var i = 0; i < emission.burstCount; i++)
            {
                var burst = emission.GetBurst(i);

                var minCount = (int) burst.count.constant;
                var maxCount = (int) burst.count.constant;

                if (burst.count.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    minCount = burst.minCount;
                    maxCount =  burst.maxCount;
                }
                
                _emitters.Add(
                    new SubEmitter { ParticleSystem = subSystem, MinCount = minCount, MaxCount = maxCount, Chance = burst.probability }
                );
            }
        }
    }
}

public struct SubEmitter
{
    public ParticleSystem ParticleSystem;
    public int MinCount;
    public int MaxCount;
    public float Chance;
}

public class EffectBundle(Emitter[] emitters)
{
    public readonly Emitter[] Emitters = emitters;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Emit(Vector3 position, Vector3 normal, float scale)
    {
        var pick = Emitters.Length == 1 ? Emitters[0] : Emitters[Random.Range(0, Emitters.Length)];
        pick.Emit(position, normal, scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmitDirect(Vector3 position, Vector3 normal, float scale)
    {
        var pick = Emitters.Length == 1 ? Emitters[0] : Emitters[Random.Range(0, Emitters.Length)];
        pick.EmitDirect(position, normal, scale);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmitDirect(Vector3 position, Vector3 normal, float scale, int count)
    {
        var pick = Emitters.Length == 1 ? Emitters[0] : Emitters[Random.Range(0, Emitters.Length)];
        pick.EmitDirect(position, normal, scale, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EmitBallistic(
        Vector3 position,
        Vector3 surfaceNormal,
        Vector3 axis,
        float scale,
        int count,
        float velocityMultiplier,
        float lifetimeMultiplier,
        float spreadDegrees,
        ref BallisticSparkPrng random,
        out string emitterName,
        out string particleSystemName)
    {
        emitterName = "<none>";
        particleSystemName = "<none>";
        if (Emitters.Length == 0)
            return 0;

        var start = Emitters.Length == 1 ? 0 : random.NextInt(0, Emitters.Length);
        Emitter pick = null;
        for (var offset = 0; offset < Emitters.Length; offset++)
        {
            var candidate = Emitters[(start + offset) % Emitters.Length];
            if (!candidate.IsBallisticCompatible)
                continue;
            pick = candidate;
            break;
        }

        if (pick == null)
            return 0;

        emitterName = pick.BallisticEmitterName;
        particleSystemName = pick.BallisticParticleSystemName;
        return pick.EmitBallistic(position, surfaceNormal, axis, scale, count, velocityMultiplier,
            lifetimeMultiplier, spreadDegrees, ref random);
    }

    public int PrepareBallistic(string effectKey)
    {
        var invalid = 0;
        for (var i = 0; i < Emitters.Length; i++)
        {
            if (!Emitters[i].PrepareBallistic(effectKey, i))
                invalid++;
        }

        return invalid;
    }

    public void Shuffle(int count = 0)
    {
        if (count >= Emitters.Length || count <= 0)
        {
            count = Emitters.Length;
        }

        // Partial Fisher-Yates: only shuffle the first 'count' positions
        for (var i = 0; i < count; i++)
        {
            var randomIndex = Random.Range(i, Emitters.Length);
            (Emitters[i], Emitters[randomIndex]) = (Emitters[randomIndex], Emitters[i]);
        }
    }


    public static EffectBundle Merge(params EffectBundle[] bundles)
    {
        return new EffectBundle(bundles.SelectMany(b => b.Emitters).ToArray());
    }

    public static Dictionary<string, EffectBundle> LoadPrefab(Effects eftEffects, GameObject prefab, bool dynamicAlpha)
    {
        var effectMap = new Dictionary<string, EffectBundle>();

        foreach (var (name, particleSystems) in ParticleHelpers.LoadEmitterBundles(eftEffects, prefab, dynamicAlpha))
        {
            effectMap[name] = new EffectBundle(particleSystems);
        }

        return effectMap;
    }

    public void ScaleDensity(float density)
    {
        if (Mathf.Approximately(density, 1f)) return;

        foreach (var emitter in Emitters)
        {
            emitter.ScaleDensity(density);
        }
    }
    
    public void ScaleLifetime(float scaling)
    {
        if (Mathf.Approximately(scaling, 1f)) return;

        foreach (var emitter in Emitters)
        {
            emitter.ScaleLifetime(scaling);
        }
    }

    public void ScaleLimit(float scaling)
    {
        if (Mathf.Approximately(scaling, 1f)) return;

        foreach (var emitter in Emitters)
        {
            emitter.ScaleLimit(scaling);
        }
    }
}
