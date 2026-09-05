using System.Collections.Generic;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HollywoodFX.Impact.Sparks;
using Systems.Effects;
using UnityEngine;
using Random = UnityEngine.Random;
using SparkVector = System.Numerics.Vector3;

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
    private ParticleSystem.MinMaxCurve _startSize;
    private bool _ballisticPrepared;
    private string _ballisticEmitterName = "<null>";
    private string _ballisticParticleSystemName = "<invalid>";

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
        if (count <= 0 || _ballisticSystem == null || _ballisticTransform == null ||
            !BallisticSparkEmissionFrame.IsFinite(position.x) ||
            !BallisticSparkEmissionFrame.IsFinite(position.y) ||
            !BallisticSparkEmissionFrame.IsFinite(position.z) ||
            !BallisticSparkEmissionFrame.IsFinite(scale) || scale <= 0f ||
            !BallisticSparkEmissionFrame.IsFinite(velocityMultiplier) || velocityMultiplier < 0f ||
            !BallisticSparkEmissionFrame.IsFinite(lifetimeMultiplier) || lifetimeMultiplier <= 0f ||
            !BallisticSparkEmissionFrame.TryCreate(
                new SparkVector(surfaceNormal.x, surfaceNormal.y, surfaceNormal.z),
                new SparkVector(axis.x, axis.y, axis.z), spreadDegrees, out var frame))
            return 0;

        var origin = ToUnityVector(frame.ResolvePosition(new SparkVector(position.x, position.y, position.z)));
        var capacity = Math.Max(0, _ballisticSystem.main.maxParticles - _ballisticSystem.particleCount);
        count = Math.Min(count, Math.Min(capacity, BallisticSparkPolicy.PerImpactParticleCap));

        var submitted = 0;
        for (var i = 0; i < count; i++)
        {
            var direction = frame.SampleDirection(ref random);
            var speed = Sample(_startSpeed, ref random) * velocityMultiplier;
            var lifetime = Sample(_startLifetime, ref random) * lifetimeMultiplier;
            var size = Sample(_startSize, ref random) * scale;
            if (!BallisticSparkEmissionFrame.IsFinite(speed) || speed < 0f ||
                !BallisticSparkEmissionFrame.IsFinite(lifetime) || lifetime <= 0f ||
                !BallisticSparkEmissionFrame.IsFinite(size) || size <= 0f)
                continue;

            var particleSeed = random.NextUInt();
            var emitParams = new ParticleSystem.EmitParams
            {
                // Explicit world-space values keep a reused emitter from moving or
                // resizing earlier sparks. Ignore the prefab's 5-15 cm spawn volumes.
                position = origin,
                applyShapeToPosition = false,
                velocity = ToUnityVector(direction * speed),
                startSize = size,
                randomSeed = particleSeed == 0U ? 1U : particleSeed,
                startLifetime = Mathf.Clamp(lifetime, 0.03f, MaximumBallisticLifetimeSeconds)
            };
            _ballisticSystem.Emit(emitParams, 1);
            submitted++;
        }

        return submitted;
    }

    public bool PrepareBallistic(string effectKey, int emitterIndex)
    {
        if (_ballisticPrepared)
            return _ballisticSystem != null;

        _ballisticPrepared = true;
        _ballisticEmitterName = Main != null ? Main.name : "<null>";
        _ballisticSystem = ResolveBallisticSystem(Main, effectKey);
        if (_ballisticSystem == null)
        {
            Plugin.Log.LogWarning(
                $"ballistic-spark-leaf invalid effect={effectKey} emitter={Main?.name ?? "<null>"} index={emitterIndex}");
            return false;
        }

        _ballisticTransform = _ballisticSystem.transform;
        _ballisticParticleSystemName = _ballisticSystem.name;
        var mainModule = _ballisticSystem.main;
        _startSpeed = mainModule.startSpeed;
        _startLifetime = mainModule.startLifetime;
        _startSize = mainModule.startSize;
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
            $"subEmitters={subEmitters.enabled}:{subEmitters.subEmittersCount} " +
            "runtimeSimulationSpace=World runtimeScalingMode=Shape runtimeParticleLight=false manualEmissionOnly=true");
        lights.enabled = false;
        // These two effect keys belong exclusively to the ballistic spark owner.
        // Configure once, before emission; do not transform live particles per impact.
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.scalingMode = ParticleSystemScalingMode.Shape;
        var emission = _ballisticSystem.emission;
        emission.enabled = false;
        _ballisticTransform.rotation = Quaternion.identity;
        _ballisticTransform.localScale = Vector3.one;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ToUnityVector(SparkVector value)
    {
        return new Vector3(value.X, value.Y, value.Z);
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

    public string BallisticEmitterName => _ballisticEmitterName;

    public string BallisticParticleSystemName => _ballisticParticleSystemName;

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
