# SPT 4.1.4 compatibility audit

HollywoodFX 2.0.17 targets [official SPT 4.1.4](https://github.com/SP-Tushonka/build/releases/tag/4.1.4), build `072d5340dc91218d010778972d85bbbbc7b3d6a9`, and EFT `0.16.9.5.40743`. HollywoodFX 2.0.16 and the `spt-4.1.3` branch remain available for the previous target.

## Build reference provenance

The official archive was downloaded and checked against GitHub's SHA-256 digest. Its client modules and BepInEx references were staged separately from the installed game. The updated game assembly was produced from the installed game's original `Assembly-CSharp.dll.spt-bak` and the official 4.1.4 delta, using SharpHDiffPatch.Core 2.3.0, the same package and patch API used by the upstream launcher. The unchanged EFT/Unity dependencies came from the installed EFT 40743 client. No live game assembly or profile was replaced.

| Artifact | SHA-256 |
| --- | --- |
| `SPT-4.1.4-40743-072d534.7z` | `BFC392E53ECF4CE2FF77C8C119FA5AF4552EA63A6FF4AE8B5DB663837ADC6B5B` |
| Original game assembly | `43A539F5AD00FCCD87EE54A084D8DBE1C5F63D12F8D855C8A392D68B3A1DEAF9` |
| Official 4.1.4 assembly delta | `8EF8A0DECBE5936550DC06246251386935B151446D54150FF576AA9EA7C8E026` |
| Patched 4.1.4 game assembly | `EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436` |
| `spt-common.dll` | `713343429010F81F630A652A6F51CA65F80A42CDD8FA489207810EA68AEF79A6` |
| `spt-core.dll` | `2E928EF688784E93B597320C9A996D2B09978E3732F07597D1C12FD6ADB09007` |
| `spt-reflection.dll` | `312823E4017202D714E35279C0D620C66731F0F8564B5ABB4FBDCB778A0D362B` |

## Compatibility checks

- The resulting assembly contains the restored `AmbianceAffectedComponent.MethodName` field and no old `String` field on that type. This distinguishes the target assembly from 4.1.3.
- No HollywoodFX source use of the types/fields listed in the [4.1.4 migration notice](https://github.com/SP-Tushonka/wiki/blob/main/modding/SPT_41_Modding/414_Changes.md) was found.
- All three string-based muzzle field lookups were checked in the patched assembly: `MuzzleManager.__muzzleJets`, `_muzzleFumes`, and `_muzzleSmokes`. Their respective types remain `MuzzleJet[]`, `MuzzleFume[]`, and `MuzzleSmoke[]`.
- The remaining property lookup, `HollywoodGraphics.Plugin.lensDustIntensity`, targets an optional companion mod, not a renamed SPT field. Companion-mod runtime interaction remains untested.
- UnityPy 1.25.3 parsed the unchanged asset bundle. It contains 3 MonoScripts and 3 MonoBehaviours: `TextureDecalsPainter`, `DeferredDecals.DeferredDecalRenderer`, and `Systems.Effects.Effects`. None of the migration-listed types appeared in the script inventory or serialized type trees; no type trees were missing. No bundle rebuild was indicated by this audit.
- Release build against the staged 4.1.4 references: 0 warnings and 0 errors.
- Portable validation: 76/76 checks passed, retaining all 75 existing checks and adding release-metadata consistency coverage.

The migration changes release metadata and documentation, not spark policy, reflection formulas, particle budgets, gameplay ballistics, or configuration compatibility. The external `hollywoodfx` bundle remains `A3531204B8E13DCEC7BBD0A403153D28D4570417D6CA7BC5B1959166B9562EC0`.

## Runtime limits

This is build, metadata, and static bundle compatibility evidence, not in-game acceptance. SPT 4.1.4 was not launched for this audit. Direct/grazing hits, ricochet direction semantics, penetration entry/exit, automatic-fire and buckshot frame time/GC, particle-leaf selection, raid cleanup, and decal/gore/tracer/companion-mod regressions still require a gameplay pass. No compatibility claim is made for custom or ported clients.

Installing HollywoodFX does not upgrade the SPT server or launcher. Use the official SPT update procedure separately before installing the 4.1.4-targeted mod package.
