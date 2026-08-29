using System.Collections.Generic;
using Comfort.Common;
using HollywoodFX.Lighting;
using HollywoodFX.Particles;
using Systems.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HollywoodFX.Gore;

internal class RigidbodyEffects : MonoBehaviour
{
    private float _lifetime = 2f;

    private List<ParticleSystem> _pool;
    private Queue<Emission> _active;

    // public bool Debug;

    public void Setup(Effects eftEffects, GameObject prefab, int copyCount, float lifetime, float density)
    {
        _lifetime = lifetime;
        _pool = [];
        _active = new Queue<Emission>();

        Plugin.Log.LogInfo($"Creating RigidbodyEffects for {prefab.name} lifetime {_lifetime}");

        for (var i = 0; i < copyCount; i++)
        {
            Plugin.Log.LogInfo($"Instantiating Effects Prefab {prefab.name} installment {i + 1}");

            var rootInstance = Instantiate(prefab);

            foreach (var child in rootInstance.transform.GetChildren())
            {
                if (!child.gameObject.TryGetComponent<ParticleSystem>(out var particleSystem)) continue;

                child.parent = eftEffects.transform;
                Singleton<MaterialRegistry>.Instance.Register(particleSystem, false);
                _pool.Add(particleSystem);

                foreach (var subSystem in particleSystem.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleHelpers.ScaleEmissionRate(subSystem, density);
                }

                Plugin.Log.LogInfo($"Adding Effect {child.name} density {density}");
            }
        }

        foreach (var effect in _pool)
        {
            if (!Plugin.BloodRenderOwnership.AllowParticleCollisionEnvironmentDeposits)
                continue;

            var particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);

            if (particleSystems == null)
                continue;

            foreach (var particleSystem in particleSystems)
            {
                if (!particleSystem.collision.enabled)
                    continue;

                particleSystem.gameObject.AddComponent<BloodSquirtCollisionHandler>();
            }
        }
    }

    // public void OnGUI()
    // {
    //     if (!Debug) return;
    //     
    //     var rect = new Rect(0f, 50f, 0f, 0f);
    //
    //     foreach (var effect in _active)
    //     {
    //         var streamEffect = effect.Effect.transform.Find("Stream")?.gameObject.GetComponent<ParticleSystem>();
    //
    //         if (streamEffect == null)
    //             continue;
    //
    //         rect = DebugUI.Label(new Vector2(50, rect.y + rect.height),
    //             $"{effect.Effect.name} stream count: {streamEffect.particleCount}", centered: false);
    //     }
    // }

    public void Update()
    {
        foreach (var emission in _active)
        {
            emission.FollowTarget();
        }

        while (_active.Count > 0)
        {
            var emission = _active.Peek();

            if (Time.time - emission.Timestamp > _lifetime)
            {
                _active.Dequeue();
                emission.Effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _pool.Add(emission.Effect);
            }
            else
            {
                // The next item is still active, we bail out.
                break;
            }
        }
    }

    public void Emit(Rigidbody rigidbody, Vector3 position, Vector3 normal, float scale = 1f)
    {
        ParticleSystem effect;

        if (_pool.Count > 0)
        {
            // Pick a random effect from the pool
            var pick = _pool.Count == 1 ? 0 : Random.Range(0, _pool.Count);
            effect = _pool[pick];
            var last = _pool.Count - 1;
            // Swap the last item to the one we just removed
            _pool[pick] = _pool[last];
            // Pop the last item in the list
            _pool.RemoveAt(last);
        }
        else
        {
            // Suppress effect
            return;
            
            // Steal an active emission
            // var emission = _active.Dequeue();
            // effect = emission.Effect;
        }

        var target = rigidbody.transform;
        var localPosition = target.InverseTransformPoint(position);
        var localNormal = target.InverseTransformDirection(normal);
        if (localNormal.sqrMagnitude <= Mathf.Epsilon)
        {
            localNormal = Vector3.forward;
        }
        localNormal.Normalize();
        var worldNormal = target.TransformDirection(localNormal);

        effect.transform.position = position;
        effect.transform.localScale = new Vector3(scale, scale, scale);
        effect.transform.rotation = Quaternion.LookRotation(worldNormal);
        effect.Play(true);

        _active.Enqueue(new Emission(
            effect,
            target,
            localPosition,
            localNormal,
            Time.time));
    }

    private readonly struct Emission(
        ParticleSystem effect,
        Transform target,
        Vector3 localPosition,
        Vector3 localNormal,
        float timestamp)
    {
        public readonly ParticleSystem Effect = effect;
        private readonly Transform _target = target;
        private readonly Vector3 _localPosition = localPosition;
        private readonly Vector3 _localNormal = localNormal;
        public readonly float Timestamp = timestamp;

        public void FollowTarget()
        {
            if (_target == null || Effect == null)
            {
                return;
            }

            var worldNormal = _target.TransformDirection(_localNormal);
            Effect.transform.SetPositionAndRotation(
                _target.TransformPoint(_localPosition),
                Quaternion.LookRotation(worldNormal));
        }
    }
}

public class BloodSquirtCollisionHandler : MonoBehaviour
{
    private Effects _effects;
    private ParticleSystem _particleSystem;
    private List<ParticleCollisionEvent> _collisionEvents;

    public void Start()
    {
        _effects = Singleton<Effects>.Instance;
        _particleSystem = GetComponent<ParticleSystem>();
        _collisionEvents = new List<ParticleCollisionEvent>(10);
        Plugin.Log.LogInfo($"Starting gore effects collision handler for {_particleSystem.name}");
    }

    public void OnParticleCollision(GameObject other)
    {
        if (!Plugin.BloodRenderOwnership.AllowParticleCollisionEnvironmentDeposits)
            return;

        if (other == null)
            return;

        var numEvents = _particleSystem.GetCollisionEvents(other, _collisionEvents);

        for (var i = 0; i < numEvents; i++)
        {
            var hitPos = _collisionEvents[i].intersection;
            var hitNormal = _collisionEvents[i].normal;

            _effects.EmitBleeding(hitPos, hitNormal);
        }
    }
}
