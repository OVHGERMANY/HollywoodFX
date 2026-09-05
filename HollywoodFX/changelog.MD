# 2.0.18
* Added restrained realism-oriented defaults while retaining all existing controls and saved settings.
* Anchored ballistic spark particles to the impact point with independent world-space size, velocity and local seeds.
* Replaced flattened grazing sprays with bounded outward cone sampling.
* Fixed active-family eviction under saturation and duplicate refill after a backwards clock value.
* Charge pellet/fragment families only for submitted particles; cache diagnostic names outside the impact hot path.
* Added geometry, budget-model, allocation, default-binding and runtime-wiring regressions. User-observed metal, concrete, soft-surface and automatic-fire checks passed; see the release notes for remaining untested cases.

# 2.0.17
* Targeted official SPT 4.1.4 (build 072d534, EFT 0.16.9.5.40743) and rebuilt against its patched game assembly and client modules.
* Audited the 4.1.4 serialized-field changes and muzzle reflection lookups; no effect behavior change or asset-bundle rebuild was needed.
* Retained the hardened Ballistic Impact Sparks system, particle limits, and existing 2.0 configuration.
* Added a release-metadata consistency check and documented the exact compatibility evidence and remaining runtime acceptance tests.

# 2.0.16
* Reworked ballistic impact sparks into one material-, energy-, angle-, and impact-state-aware system.
* Metallic impacts and ricochets now produce directional, bounded hot-fragment sprays.
* Mineral impacts produce only rare restrained flecks; soft surfaces and bodies produce no generic sparks.
* Penetration exits cannot repeat the full entry shower.
* Centralized spark ownership prevents main-impact and tracer effects from stacking contact sparks.
* Added smooth distance scaling, strict automatic-fire particle budgets, and bounded raid diagnostics.
* RC2 hardening now uses raw incoming projectile energy, deterministic local sampling, and a fixed pellet/fragment family budget.
* Particle leaves are selected by validated cached criteria and report one-time initialization metadata for runtime evidence.
* No gameplay or ballistics behavior changes; the external asset bundle is unchanged.

# 2.0.15
* Added character-attached impact marks and explicit character/corpse geometry policy.
* Preserved independent impacts and replace stock decals only after a custom mark succeeds.
* Added restrained blood presentation and TraumaCore blood-render ownership gates.
* Added shot-lifecycle cleanup coverage and a portable 25-check validation suite.
* Rebuilt and validated against official SPT 4.1.3 (EFT 0.16.9.40743).
* Offline validation is complete; in-game visual acceptance remains pending.

# 1.4.0
* Overhauled blood & gore
  * Finisher effects for killshots
  * Bleed-like effects now get attached to the ragdoll
  * Penetrating shots generate high definition blood splatter behind enemies (16 different varieties)
  * Bleed drops variety and quality vastly improved (16 different varieties)
* Ambient smoke improved
* Dynamic ragdoll reactivation! No need to keep active ragdolls eating your CPU.
* Removed blood effects appearing when the local player is shot at
* Performance optimization and improvements across the board

Thanks to Arttumiro, Soren and Devraccoon for testing and providing feedback, much appreciated!
