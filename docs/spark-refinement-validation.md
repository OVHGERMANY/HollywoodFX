# Spark refinements and realism defaults — 2.0.18

## Scope

Local branch: `codex/spark-refinements`, based on `3b05e4053c7151afbc91cf1911df2904edf81148`.
The user selected **realism defaults, controls retained**. No configuration control is removed or locked.
The initial refinement pass changed no installed file, release asset, tag, or remote branch. The user subsequently approved local installation alongside published mod updates; deployment evidence is recorded separately below.

The tested candidate was `2.0.18-preview.1`. Final `2.0.18` retains its effect implementation and changes only release identity/log text and documentation. See [release notes](releases/2.0.18.md) for the September 5 user-observed checks and explicit coverage limits. Earlier entries below are historical checkpoints.

## Fixed behavior

- **Active family limits survive saturation.** The old 64-slot tracker evicted live entries. A regression test reproduced 36 particles for one family inside its 18-particle window. Full storage now rejects a new family until a slot expires, without erasing an existing allowance.
- **Family accounting follows particle submission.** Preview does not reserve a slot or spend an event. Global rejection, a full particle leaf, or invalid particle parameters no longer spend the family's two events without submitting particles. The global budget remains conservative and counts reservations, including a reservation the emitter could not use.
- **Clock regression cannot mint tokens.** A backwards timestamp followed by the previous timestamp formerly granted another 24 particles from an empty global bucket. The refill timestamp now keeps its high-water mark; raid disposal still resets both budgets.
- **Contact-point emission replaces inherited spawn volumes.** Fourteen particle systems under the two spark groups were inspected in the installed bundle; their spawn radii reach 0.15 m. Launch positions are now explicit, 0.001 m outside the impact plane. Unity's shape volume is not applied to those positions. This uses the documented [position override behavior](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystem.EmitParams-applyShapeToPosition.html).
- **Reusing an emitter does not rescale earlier sparks.** World-space position, velocity, size and seed are supplied per particle. The spark transform is not moved, rotated or scaled per impact. Shape-only scaling avoids transform-dependent particle size, following [Unity's scaling contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystem.MainModule-scalingMode.html).
- **Grazing launch directions keep depth.** A fixed-cost cone sampler mirrors inward samples outward instead of projecting them flat onto the surface. It takes two local random draws per direction. This changes visual fragment spread, not EFT's ricochet calculation.
- **Malformed geometry fails closed.** Normalization avoids squared-length overflow for large finite vectors, and emission rejects non-finite scale, position and particle parameters.
- **Diagnostics avoid repeated native name lookups.** Emitter and particle-system names are cached during preparation. Particle seeds are passed through the [per-particle seed API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystem.EmitParams-randomSeed.html). Initialization logs distinguish the source prefab settings from runtime emission overrides.

## Default tuning

These are restrained presentation choices, not experimentally calibrated physical constants. Smaller effects alone do not establish physical accuracy. Day/night, material, ammunition and distance comparisons in the game remain necessary.

| Existing control | Previous default | New default |
| --- | ---: | ---: |
| Ballistic Impact Spark Intensity | 1 | 0.7 |
| Impact Effect Size | 0.75 | 0.65 |
| Fireball Density | 1 | 0.5 |
| Sparks Density (CPU HEAVY) | 1 | 0.5 |
| Muzzle Jet Size | 1 | 0.8 |
| Muzzle Sparks Size | 1 | 0.65 |
| Muzzle Sparks Emission Rate (RESTART) | 1 | 0.5 |
| Muzzle Smoke Size | 1 | 0.85 |
| Muzzle Smoke Emission Rate (RESTART) | 1 | 0.8 |
| Concussion Duration | 1 | 0.75 |
| Enable Suppression FX | true | false |
| Battle Blur Intensity | 1 | 0.35 |
| Ambient Effect Emission Rate | 1 | 0.7 |
| Enable Cinematic Ragdolls (RESTART) | true | false |
| Spent Shells Size | 1.5 | 1 |
| Shell Ejection Velocity | 1.5 | 1 |

Spark intensity changes probability and count, not particle size; reducing it to 0.7 does not mean exactly 30% fewer visible particles. The ordinary metal profile remains eligible, mineral profiles stay below metal, and the Potato preset remains lower-load.

Scope depth-of-field was already off by default and stays off. Smoke/dust/debris explosion defaults, bullet marks, tracer logic, blood ownership, kinetic scaling, shell lifetime and all hard particle limits remain unchanged. Extra near-impact suppression blur and cinematic ragdoll overrides are off by default; users may still enable them.

Existing configuration values are preserved, including older default values already saved to disk. A fresh configuration uses the new defaults. After installing a future build of this branch, existing users can choose **F12 -> HollywoodFX -> Defaults**, then restart the game. That action resets all HollywoodFX settings, so preserve custom preferences first. Nothing automatically migrates or overwrites a saved configuration.

## Verification record

### 2026-09-04T22:28:45-05:00

- Baseline Release: 0 warnings, 0 errors; 76/76 checks passed.
- Before the limit fixes: 76/79 checks passed. The three new failures reproduced active-family eviction, saturation acceptance and clock-refill duplication.
- Current Release: 0 warnings, 0 errors; 98/98 checks passed.
- Shared production cone math: 100,000 direction samples stayed finite, unit-length, outward and within the requested cone, within floating-point tolerances.
- Independent family-budget reference model: 100,000 mixed contacts, including previews without emission and partial submissions, matched the production implementation.
- Portable hot-path allocation check: 0 bytes over 100,000 warmed iterations of the geometry and budget code on .NET 8. This is not a Unity profiler result or an FPS claim.
- All 23 added external member references resolved against the installed SPT 4.1.4 game/BepInEx assemblies, including `System.Numerics.Vector3` in the game's existing `System.Numerics.dll`. No extra runtime DLL is required for that math type.
- Release DLL SHA-256: `CB1F332DC1D285B129B588D554FDD7D3D0A153F63A5C6FCC68A1B51E0E871249`.

Repeatable local build and portable validation:

```powershell
dotnet build .\HollywoodFX.sln --configuration Release --nologo -p:SptRoot='E:\Games\SPT' -p:TreatWarningsAsErrors=true
dotnet run --project .\HollywoodFX.Validation\HollywoodFX.Validation.csproj --configuration Release --no-build
```

The suite combines behavioral tests of shared production policy/math with source-wiring checks. It does not instantiate Unity's particle engine. The local read-only API audit script and bundle-inspection script are under ignored `artifacts/` and depend on this machine's installation.

## Runtime acceptance still required

1. Compare stopped hits and shallow ricochets on metal, concrete and armor in daylight and darkness; verify only appropriate materials produce sparks.
2. Fire at separated points while earlier particles are alive. Change Impact Effect Size between bursts; earlier sparks must not move or resize.
3. Check entry/exit pairs and tracer/non-tracer hits for duplicate showers. EFT's `Shot.CurrentDirection` timing has not been reinterpreted by this patch.
4. Exercise automatic fire, 8/12-pellet shells and fragment-heavy impacts; capture spark diagnostics and Unity frame-time/allocation evidence.
5. Verify new defaults on a fresh/reset configuration, then verify existing custom values survive an upgrade. Check body/corpse marks and TraumaCore ownership.
6. Exit and start a second raid; verify budget cleanup and no stale particle or diagnostic state.

The initial refinement pass made no in-game visual acceptance, frame-rate improvement, installation, publication or stable-release claim. The local installation record follows; visual acceptance is still outstanding.

### 2026-09-04T22:30:24-05:00 — final verification

- Debug also built with 0 warnings/errors and passed 98/98 checks.
- A clean Release rebuild produced the same DLL SHA-256 recorded above; Release again passed 98/98 checks.
- Repeated the 23-reference installed-assembly audit; all resolved. `git diff --check` passed.
- Installed DLL remained `8E0CB412379BC56366B951BC2C88B7B05DC90C917EE59D72F755AE56D07E430E`.
- Installed configuration remained `E02C65B18A5BC2C1A90D5E000F84AF57EC23B00D1D8F9A5CE09D260E5EE70E2B`.
- Changes remain local and uncommitted on the refinement branch. The earlier experiment stash is retained as a recovery point.

### 2026-09-05T01:20:53-05:00 — approved local installation

- User selected published mod releases plus the HollywoodFX refinements, with realism defaults and existing controls retained.
- Assigned `2.0.18-preview.1` informational/startup identity and numeric plugin `2.0.18`; configuration compatibility stays `2.0`.
- Independent Release build and clean rebuild: 0 warnings/errors, 98/98 checks on both runs, identical DLL SHA-256 `00D9E6B2760CCFF4928BC9A447DB26A05C160E8620D338EECB1883DD2EAF8F9D`.
- All 23 added external member references resolved against the installed game before deployment.
- Installed the candidate DLL in `E:\Games\SPT`, preserving the existing asset bundle. Applied only the 16 values listed above to the existing configuration; controls and unrelated preferences remain intact. This was an explicit local configuration edit, not an automatic migration added to the mod.
- Installed configuration SHA-256: `188949AF1CB0D327B7338B4AB56F9153379BD5F83147B60BDC9BA3AE0C0C5A3C`.
- Rollback directory: `E:\Games\SPT-Mod-Backups\20260905-011404-mod-refresh`. Original replaced files and 91 user/configuration files were backed up and verified. The restore command passed its dry-run.
- Combined mod refresh verified 743 files: 6 approved replacements, 3 unchanged-profile backup copies and 1 server-generated active-mods record. Other inventoried files were unchanged; 8 historical files pruned by server retention were restored.
- SPT 4.1.4 server startup loaded WTT 3.0.6, Eco WW2 Pack 2.0.0 and Preset Studio 2.1.0, loaded all 44 bundles, returned HTTP 200, then shut down normally. Five existing backup-folder-name warnings remain. This is server-only evidence, not client/raid acceptance.
- No game was launched. No commit, push, tag, merge or GitHub release was made during this installation pass.
