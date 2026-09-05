# HollywoodFX

HollywoodFX expands SPT's impact, explosion, muzzle, gore, ragdoll, and ambient combat effects. This branch targets the official SPT 4.1.4 client build; it is not built against a custom or ported EFT client.

## Install

Download the current release archive and extract it into the root of an official SPT 4.1.4 installation. The archive installs:

- `BepInEx/plugins/HollywoodFX/HollywoodFX.dll`
- `BepInEx/plugins/HollywoodFX/hollywoodfx`

The `hollywoodfx` asset bundle is required. A DLL by itself is not a complete installation.

Version 2.0.17 retains the hardened Ballistic Impact Sparks system and existing 2.0 configuration. It does not upgrade SPT itself. See the [4.1.4 compatibility audit](docs/SPT-4.1.4-compatibility.md) for the exact build references, serialized-field checks, and remaining runtime tests.

## Validate

The portable validation project does not require game assemblies:

```powershell
dotnet run --project .\HollywoodFX.Validation\HollywoodFX.Validation.csproj --configuration Release
```

## Build

Point the project at an official SPT 4.1.4 installation with either `SptRoot` or the `SPT_ROOT` environment variable:

```powershell
$env:SPT_ROOT = 'E:\Games\SPT'
dotnet build .\HollywoodFX\HollywoodFX.csproj --configuration Release --property:TreatWarningsAsErrors=true
```

A normal build never copies files into SPT. Deployment is deliberately explicit:

```powershell
dotnet build .\HollywoodFX\HollywoodFX.csproj --configuration Release --target:Deploy --property:SptRoot='E:\Games\SPT' --property:DeployRoot='E:\Games\SPT'
```

## Package

The asset bundle is about 1 GB and is intentionally kept outside Git. The packaging script accepts the bundle from an installed copy, verifies its pinned SHA-256, builds the DLL, and creates the deterministic `HollywoodFX-2.0.17.zip` release archive:

```powershell
.\scripts\New-ReleasePackage.ps1 -SptRoot 'E:\Games\SPT'
```

Generated archives and checksum files are written under `artifacts/release/`, which Git ignores.
Prerelease candidates use an explicit unique label, for example `-Version '2.0.17-rc.1'`.

## Bug reports

Use the GitHub bug form and attach `BepInEx/LogOutput.log`. Reports must identify the exact SPT version and whether the issue reproduces without a custom client port.
