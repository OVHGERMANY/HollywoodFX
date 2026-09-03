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
