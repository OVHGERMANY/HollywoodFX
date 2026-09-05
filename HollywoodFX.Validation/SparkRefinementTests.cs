using System.Numerics;
using HollywoodFX.Impact.Sparks;

internal static class SparkRefinementTests
{
    public static readonly (string Name, Action Run)[] All =
    {
        ("budget previews neither consume nor reserve cluster slots", PreviewDoesNotSpend),
        ("globally rejected contacts retain their family allowance", GlobalRejectionDoesNotSpend),
        ("partial global allowance charges only submitted particles", PartialGlobalAllowance),
        ("backwards time cannot reopen a live family", BackwardsClusterTime),
        ("reset clears global and family state", ResetClearsState),
        ("cone sampling stays finite outward and bounded across 100000 samples", ConeStress),
        ("grazing samples do not collapse onto the surface", GrazingSamplesRetainSpread),
        ("cone sampling consumes exactly two local random draws", FixedDrawCount),
        ("emission origin stays at the contact across impact reuse", ContactPosition),
        ("geometry rejects malformed inputs and normalizes large finite vectors", GeometryInputBounds),
        ("spark math and budgets allocate zero bytes in the hot path", HotPathAllocation),
        ("runtime charges family budget after particle submission", RuntimeCommitsAfterSubmission),
        ("runtime emits explicit world-space contact particles", RuntimeUsesContactParticles),
        ("runtime caches particle names outside the hot path", RuntimeCachesNames),
        ("realism defaults retain every existing adjustment", RealismDefaultsRetainControls),
        ("realism spark defaults reduce output while preserving material hierarchy", RealismSparkOutput),
        ("realism defaults do not amplify casings or cinematic effects", RealismPresentationBounds),
        ("cluster budget matches an independent model for 100000 contacts", ClusterReferenceModel),
        ("invalid budget inputs cannot poison subsequent valid requests", InvalidBudgetInputs)
    };

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void PreviewDoesNotSpend()
    {
        var budget = new BallisticSparkClusterBudget();
        for (ulong key = 1; key <= 1000; key++)
            Require(budget.Preview(true, key, 14, 1f).AllowedParticles == 14, "preview reserved a slot");
        Require(budget.Consume(true, 1, 14, 1f).AllowedParticles == 14, "preview spent particles");
        Require(budget.Consume(true, 1, 14, 1f).AllowedParticles == 4, "remaining allowance changed");
    }

    private static void GlobalRejectionDoesNotSpend()
    {
        var global = new BallisticSparkBudget();
        var family = new BallisticSparkClusterBudget();
        for (var i = 0; i < 4; i++) global.Consume(24, 1f, 1);
        for (var i = 0; i < 12; i++)
        {
            var preview = family.Preview(true, 1, 14, 1f);
            var allowed = global.Consume(preview.AllowedParticles, 1f, 1);
            family.Consume(true, 1, allowed, 1f);
        }
        Require(family.Consume(true, 1, 14, 1.02f).AllowedParticles == 14,
            "global rejection exhausted the family before its first visible event");
    }

    private static void PartialGlobalAllowance()
    {
        var global = new BallisticSparkBudget();
        var family = new BallisticSparkClusterBudget();
        for (var i = 0; i < 3; i++) global.Consume(24, 1f, 1);
        global.Consume(22, 1f, 1);
        var preview = family.Preview(true, 1, 14, 1f);
        var allowed = global.Consume(preview.AllowedParticles, 1f, 1);
        Require(allowed == 2, "partial frame setup failed");
        family.Consume(true, 1, allowed, 1f);
        Require(family.Consume(true, 1, 14, 1.02f).AllowedParticles == 14,
            "family paid for the twelve globally rejected particles");
        Require(family.Consume(true, 1, 1, 1.03f).AllowedParticles == 0,
            "two visible events failed to close the family");
    }

    private static void BackwardsClusterTime()
    {
        var family = new BallisticSparkClusterBudget();
        family.Consume(true, 1, 18, 10f);
        Require(family.Consume(true, 1, 18, 9f).AllowedParticles == 0, "backwards time reopened family");
        Require(family.Consume(true, 1, 18, 10f).AllowedParticles == 0, "revisited time reopened family");
    }

    private static void ResetClearsState()
    {
        var global = new BallisticSparkBudget();
        var family = new BallisticSparkClusterBudget();
        for (var i = 0; i < 8; i++) global.Consume(24, 10f, i);
        for (ulong key = 1; key <= 64; key++) family.Consume(true, key, 18, 10f);
        global.Reset();
        family.Reset();
        Require(global.Consume(24, 0f, 1) == 24, "global reset failed");
        Require(family.Consume(true, 1, 18, 0f).AllowedParticles == 18, "family reset failed");
    }

    private static void ConeStress()
    {
        var random = new BallisticSparkPrng(0xDEADBEEF);
        for (var i = 0; i < 1000; i++)
        {
            var normal = Vector3.Normalize(new Vector3(random.NextSignedFloat(), random.NextSignedFloat(), random.NextSignedFloat()));
            var tangent = Vector3.Normalize(Vector3.Cross(normal, Math.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            var axis = Vector3.Normalize(normal * random.NextFloat01() + tangent * random.NextFloat01());
            var spread = random.NextFloat01() * 75f;
            Require(BallisticSparkEmissionFrame.TryCreate(normal, axis, spread, out var frame), "valid frame rejected");
            var minDot = (float)Math.Cos(spread * Math.PI / 180d);
            for (var j = 0; j < 100; j++)
            {
                var direction = frame.SampleDirection(ref random);
                Require(float.IsFinite(direction.LengthSquared()), "non-finite direction");
                Require(Math.Abs(direction.LengthSquared() - 1f) < 0.00001f, "non-unit direction");
                Require(Vector3.Dot(direction, normal) >= -0.000001f, "inward direction");
                Require(Vector3.Dot(direction, axis) >= minDot - 0.00001f, "direction escaped cone");
            }
        }
    }

    private static void GrazingSamplesRetainSpread()
    {
        Require(BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, Vector3.UnitX, 40f, out var frame), "frame failed");
        var random = new BallisticSparkPrng(42);
        var flattened = 0;
        var meanZ = 0d;
        for (var i = 0; i < 10000; i++)
        {
            var direction = frame.SampleDirection(ref random);
            if (direction.Y <= 0.000001f) flattened++;
            meanZ += direction.Z;
        }
        Require(flattened < 5, $"{flattened}/10000 grazing samples collapsed onto the plane");
        Require(Math.Abs(meanZ / 10000) < 0.015, "grazing spread has a sideways bias");
    }

    private static void FixedDrawCount()
    {
        BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, Vector3.UnitY, 20f, out var frame);
        var first = new BallisticSparkPrng(99);
        var second = new BallisticSparkPrng(99);
        frame.SampleDirection(ref first);
        second.NextUInt();
        second.NextUInt();
        Require(first.NextUInt() == second.NextUInt(), "sample did not use exactly two draws");
    }

    private static void ContactPosition()
    {
        BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, Vector3.UnitY, 20f, out var frame);
        var first = frame.ResolvePosition(new Vector3(0, 0, 0));
        var second = frame.ResolvePosition(new Vector3(50, 10, -30));
        Require(first == new Vector3(0, 0.001f, 0), "first origin not one millimetre above contact");
        Require(second == new Vector3(50, 10.001f, -30), "second origin not anchored to its own contact");
        Require(first == frame.ResolvePosition(Vector3.Zero), "later impact changed earlier origin");
    }

    private static void GeometryInputBounds()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Require(!BallisticSparkEmissionFrame.TryCreate(new Vector3(value, 0, 1), Vector3.UnitY, 20, out _), "invalid normal accepted");
            Require(!BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, new Vector3(0, value, 1), 20, out _), "invalid axis accepted");
            Require(!BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, Vector3.UnitY, value, out _), "invalid spread accepted");
        }
        Require(!BallisticSparkEmissionFrame.TryNormalize(Vector3.Zero, out _), "zero vector accepted");
        Require(!BallisticSparkEmissionFrame.TryNormalize(new Vector3(0.0000001f), out _), "near-zero vector accepted");
        Require(BallisticSparkEmissionFrame.TryNormalize(new Vector3(float.MaxValue), out var huge), "finite large vector rejected");
        Require(Math.Abs(huge.LengthSquared() - 1f) < 0.00001f, "overflow normalized to zero");
        Require(BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, -Vector3.UnitY, 20f, out var frame), "inward fallback failed");
        var random = new BallisticSparkPrng(1);
        Require(frame.SampleDirection(ref random).Y > 0, "fallback remained inward");
    }

    private static void HotPathAllocation()
    {
        var global = new BallisticSparkBudget();
        var family = new BallisticSparkClusterBudget();
        BallisticSparkEmissionFrame.TryCreate(Vector3.UnitY, Vector3.UnitX, 40f, out var frame);
        var random = new BallisticSparkPrng(1);
        for (var i = 0; i < 1000; i++) Step(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1000; i < 101000; i++) Step(i);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(bytes == 0, $"portable hot path allocated {bytes} bytes in 100000 iterations");

        void Step(int index)
        {
            var key = (ulong)(index % 80 + 1);
            var now = index * 0.001f;
            var preview = family.Preview(true, key, 14, now);
            var allowed = global.Consume(preview.AllowedParticles, now, index / 16);
            family.Consume(true, key, allowed, now);
            frame.SampleDirection(ref random);
        }
    }

    private static string Source(string suffix)
    {
        var assembly = typeof(SparkRefinementTests).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static void RuntimeCommitsAfterSubmission()
    {
        var source = Source("BallisticSparkEffects.cs");
        var preview = source.IndexOf("_clusterBudget.Preview(", StringComparison.Ordinal);
        var global = source.IndexOf("_budget.Consume(", StringComparison.Ordinal);
        var emission = source.IndexOf("bundle.EmitBallistic(", StringComparison.Ordinal);
        var commit = source.IndexOf("_clusterBudget.Consume(context.UsesClusterBudget, clusterKey, emitted, now)", StringComparison.Ordinal);
        Require(preview >= 0 && preview < global && global < emission && emission < commit,
            "runtime spends family budget before it knows how many particles were submitted");
    }

    private static void RuntimeUsesContactParticles()
    {
        var source = Source("EffectBundle.cs");
        var start = source.IndexOf("public int EmitBallistic(", StringComparison.Ordinal);
        var end = source.IndexOf("public bool PrepareBallistic(", StringComparison.Ordinal);
        var emit = source[start..end];
        foreach (var token in new[] { "position = origin", "applyShapeToPosition = false", "startSize = size",
                     "randomSeed = particleSeed", "frame.SampleDirection(ref random)", "main.maxParticles - _ballisticSystem.particleCount" })
            Require(emit.Contains(token, StringComparison.Ordinal), "contact emission omitted " + token);
        Require(!emit.Contains("_ballisticTransform.position =", StringComparison.Ordinal) &&
                !emit.Contains("_ballisticTransform.rotation =", StringComparison.Ordinal) &&
                !emit.Contains("_ballisticTransform.localScale =", StringComparison.Ordinal),
            "per-impact transform changes can affect existing particles");
        Require(source.Contains("mainModule.simulationSpace = ParticleSystemSimulationSpace.World", StringComparison.Ordinal) &&
                source.Contains("mainModule.scalingMode = ParticleSystemScalingMode.Shape", StringComparison.Ordinal),
            "world-space independent sizing is not configured");
    }

    private static void RuntimeCachesNames()
    {
        var source = Source("EffectBundle.cs");
        Require(source.Contains("BallisticEmitterName => _ballisticEmitterName", StringComparison.Ordinal) &&
                source.Contains("BallisticParticleSystemName => _ballisticParticleSystemName", StringComparison.Ordinal),
            "native name lookups remain on each impact");
    }

    private static void RealismDefaultsRetainControls()
    {
        var source = Source("Plugin.cs");
        var bindings = new (string Key, string Constant)[]
        {
            ("Ballistic Impact Spark Intensity", "SparkIntensity"),
            ("Impact Effect Size", "ImpactSize"),
            ("Fireball Density", "FireballDensity"),
            ("Sparks Density (CPU HEAVY)", "ExplosionSparkDensity"),
            ("Muzzle Jet Size", "MuzzleJetSize"),
            ("Muzzle Sparks Size", "MuzzleSparkSize"),
            ("Muzzle Sparks Emission Rate (RESTART)", "MuzzleSparkEmission"),
            ("Muzzle Smoke Size", "MuzzleSmokeSize"),
            ("Muzzle Smoke Emission Rate (RESTART)", "MuzzleSmokeEmission"),
            ("Concussion Duration", "ConcussionDuration"),
            ("Enable Suppression FX", "SuppressionEnabled"),
            ("Battle Blur Intensity", "BattleBlurIntensity"),
            ("Ambient Effect Emission Rate", "AmbientEmission"),
            ("Enable Cinematic Ragdolls (RESTART)", "CinematicRagdolls"),
            ("Spent Shells Size", "ShellSize"),
            ("Shell Ejection Velocity", "ShellVelocity")
        };
        foreach (var binding in bindings)
            Require(source.Contains($"\"{binding.Key}\", RealismDefaults.{binding.Constant}, new ConfigDescription(", StringComparison.Ordinal),
                "missing adjustable realism default: " + binding.Key);
        Require(source.Contains("MajorMinorVersion = \"2.0\"", StringComparison.Ordinal), "existing configuration would be reset");
    }

    private static void RealismSparkOutput()
    {
        foreach (var energy in new[] { 8f, 80f, 280f, 1600f, 4000f })
        {
            foreach (var state in Enum.GetValues<BallisticSparkImpactState>())
            {
                var old = Plan(BallisticSparkSurfaceClass.PrimaryMetal, state, energy, 1f);
                var tuned = Plan(BallisticSparkSurfaceClass.PrimaryMetal, state, energy, HollywoodFX.RealismDefaults.SparkIntensity);
                Require(tuned.Probability <= old.Probability && tuned.MaximumParticles <= old.MaximumParticles,
                    "lower default amplified output");
                var mineral = Plan(BallisticSparkSurfaceClass.SecondaryMineral, state, energy, HollywoodFX.RealismDefaults.SparkIntensity);
                Require(mineral.Probability <= tuned.Probability && mineral.MaximumParticles <= tuned.MaximumParticles,
                    "mineral default surpassed metal");
            }
        }
        var near = Plan(BallisticSparkSurfaceClass.PrimaryMetal, BallisticSparkImpactState.Stopped, 1600f, HollywoodFX.RealismDefaults.SparkIntensity);
        Require(near.ShouldAttemptEmission && near.MaximumParticles >= 2, "default erased ordinary metal sparks");
        Require(HollywoodFX.RealismDefaults.SparkIntensity > 0.5f, "Potato preset is no longer below the default");

        static BallisticSparkPlan Plan(BallisticSparkSurfaceClass surface, BallisticSparkImpactState state, float energy, float intensity)
            => BallisticSparkPolicy.CreatePlan(surface, state, energy, 1f, 0.8f, true, 10f, intensity, 140f, true);
    }

    private static void RealismPresentationBounds()
    {
        Require(HollywoodFX.RealismDefaults.ShellSize == 1f && HollywoodFX.RealismDefaults.ShellVelocity == 1f,
            "casings are larger or faster than their unamplified defaults");
        Require(!HollywoodFX.RealismDefaults.CinematicRagdolls && !HollywoodFX.RealismDefaults.SuppressionEnabled,
            "extra cinematic ragdolls or near-impact blur are on by default");
        foreach (var value in new[] { HollywoodFX.RealismDefaults.FireballDensity, HollywoodFX.RealismDefaults.ExplosionSparkDensity,
                     HollywoodFX.RealismDefaults.MuzzleJetSize, HollywoodFX.RealismDefaults.MuzzleSparkSize,
                     HollywoodFX.RealismDefaults.MuzzleSparkEmission, HollywoodFX.RealismDefaults.MuzzleSmokeSize,
                     HollywoodFX.RealismDefaults.MuzzleSmokeEmission, HollywoodFX.RealismDefaults.BattleBlurIntensity,
                     HollywoodFX.RealismDefaults.AmbientEmission })
            Require(value > 0f && value < 1f, "restrained default is disabled or amplified");
    }

    private static void ClusterReferenceModel()
    {
        var budget = new BallisticSparkClusterBudget();
        var reference = new Dictionary<ulong, (float Start, int Particles, int Events)>();
        var random = new BallisticSparkPrng(0xB00D6E7);
        var now = 1f;
        for (var i = 0; i < 100000; i++)
        {
            now += random.NextFloat01() * 0.003f;
            var key = (ulong)random.NextInt(1, 97);
            var requested = random.NextInt(1, 30);
            foreach (var expired in reference.Where(p => now - p.Value.Start > 0.18f).Select(p => p.Key).ToArray())
                reference.Remove(expired);
            var exists = reference.TryGetValue(key, out var state);
            var expected = 0;
            if (exists)
                expected = state.Events < 2 ? Math.Min(requested, 18 - state.Particles) : 0;
            else if (reference.Count < 64)
                expected = Math.Min(requested, 18);

            // Some valid plans are rejected by the global budget or emitter. Preview
            // must leave no trace; commit receives only the actually submitted count.
            var preview = budget.Preview(true, key, requested, now);
            Require(preview.AllowedParticles == expected, $"reference preview mismatch at contact {i}");
            var submitted = random.NextInt(0, expected + 1);
            var actual = budget.Consume(true, key, submitted, now).AllowedParticles;
            Require(actual == submitted, $"reference commit mismatch at contact {i}");
            if (submitted > 0)
                reference[key] = (exists ? state.Start : now, state.Particles + submitted, state.Events + 1);
        }
    }

    private static void InvalidBudgetInputs()
    {
        var global = new BallisticSparkBudget();
        var family = new BallisticSparkClusterBudget();
        foreach (var now in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Require(global.Consume(24, now, 1) == 0, "invalid clock received global tokens");
            Require(family.Consume(true, 1, 18, now).AllowedParticles == 0, "invalid clock received family tokens");
        }
        foreach (var requested in new[] { int.MinValue, -1, 0 })
        {
            Require(global.Consume(requested, 1f, 1) == 0, "nonpositive global request accepted");
            Require(family.Consume(true, 1, requested, 1f).AllowedParticles == 0, "nonpositive family request accepted");
        }
        Require(family.Consume(true, 0, 18, 1f).AllowedParticles == 0, "missing family identity accepted");
        Require(global.Consume(24, 1f, 1) == 24, "invalid request poisoned global state");
        Require(family.Consume(true, 1, 18, 1f).AllowedParticles == 18, "invalid request poisoned family state");
    }
}
