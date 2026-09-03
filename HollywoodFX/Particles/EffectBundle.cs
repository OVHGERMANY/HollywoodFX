using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Systems.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Particles;

public class Emitter
{
    private const float MaximumBallisticLifetimeSeconds = 0.9f;
    public readonly ParticleSystem Main;
    private readonly List<SubEmitter> _emitters;
    private readonly ParticleSystem _ballisticSystem;
    private readonly Transform _ballisticTransform;
    private readonly ParticleSystem.MinMaxCurve _startSpeed;
    private readonly ParticleSystem.MinMaxCurve _startLifetime;
    private readonly ParticleSystemSimulationSpace _simulationSpace;
    private readonly Transform _customSimulationSpace;

    public Emitter(ParticleSystem main)
    {
        Main = main;
        _emitters = [];

        _ballisticSystem = ResolveBallisticSystem(Main);
        _ballisticTransform = _ballisticSystem.transform;
        var mainModule = _ballisticSystem.main;
        _startSpeed = mainModule.startSpeed;
        _startLifetime = mainModule.startLifetime;
        _simulationSpace = mainModule.simulationSpace;
        _customSimulationSpace = mainModule.customSimulationSpace;

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
        float spreadDegrees)
    {
        if (count <= 0)
            return 0;

        _ballisticTransform.position = position;
        _ballisticTransform.localScale = new Vector3(scale, scale, scale);
        _ballisticTransform.rotation = Quaternion.LookRotation(axis);

        var spreadScale = Mathf.Tan(Mathf.Clamp(spreadDegrees, 0f, 75f) * Mathf.Deg2Rad);
        for (var i = 0; i < count; i++)
        {
            var random = Random.insideUnitSphere;
            var perpendicular = random - axis * Vector3.Dot(random, axis);
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

            var worldVelocity = direction * Mathf.Max(0f, Sample(_startSpeed) * velocityMultiplier);
            var emitParams = new ParticleSystem.EmitParams
            {
                velocity = ToSimulationVelocity(worldVelocity),
                startLifetime = Mathf.Clamp(
                    Sample(_startLifetime) * lifetimeMultiplier,
                    0.03f,
                    MaximumBallisticLifetimeSeconds)
            };
            _ballisticSystem.Emit(emitParams, 1);
        }

        return count;
    }

    public void PrepareBallistic()
    {
        var lights = _ballisticSystem.lights;
        lights.enabled = false;
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

    private static ParticleSystem ResolveBallisticSystem(ParticleSystem main)
    {
        var mainSubEmitters = main.subEmitters;
        if (!mainSubEmitters.enabled || mainSubEmitters.subEmittersCount == 0)
            return main;

        var systems = main.GetComponentsInChildren<ParticleSystem>(true);
        for (var i = 0; i < systems.Length; i++)
        {
            var subEmitters = systems[i].subEmitters;
            if (!subEmitters.enabled || subEmitters.subEmittersCount == 0)
                return systems[i];
        }

        return main;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sample(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Random.Range(curve.constantMin, curve.constantMax);
            case ParticleSystemCurveMode.Curve:
                return curve.curve == null ? curve.constant : curve.curve.Evaluate(Random.value) * curve.curveMultiplier;
            case ParticleSystemCurveMode.TwoCurves:
                if (curve.curveMin == null || curve.curveMax == null)
                    return Random.Range(curve.constantMin, curve.constantMax);
                var time = Random.value;
                return Mathf.Lerp(
                    curve.curveMin.Evaluate(time),
                    curve.curveMax.Evaluate(time),
                    Random.value) * curve.curveMultiplier;
            default:
                return curve.constant;
        }
    }

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
        float spreadDegrees)
    {
        if (Emitters.Length == 0)
            return 0;

        var pick = Emitters.Length == 1 ? Emitters[0] : Emitters[Random.Range(0, Emitters.Length)];
        return pick.EmitBallistic(position, surfaceNormal, axis, scale, count, velocityMultiplier,
            lifetimeMultiplier, spreadDegrees);
    }

    public void PrepareBallistic()
    {
        for (var i = 0; i < Emitters.Length; i++)
            Emitters[i].PrepareBallistic();
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
