[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SptRoot,

    [string]$BundlePath,

    [string]$OutputDirectory,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '2.0.14',

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$ExpectedBundleSha256 = 'A3531204B8E13DCEC7BBD0A403153D28D4570417D6CA7BC5B1959166B9562EC0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-DeterministicZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $entry = $Archive.CreateEntry($EntryName, [System.IO.Compression.CompressionLevel]::NoCompression)
    $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $inputStream = [System.IO.File]::OpenRead($SourcePath)
    $outputStream = $entry.Open()

    try {
        $inputStream.CopyTo($outputStream)
    }
    finally {
        $outputStream.Dispose()
        $inputStream.Dispose()
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$resolvedSptRoot = [System.IO.Path]::GetFullPath($SptRoot)
$projectPath = Join-Path $repositoryRoot 'HollywoodFX\HollywoodFX.csproj'

if ([string]::IsNullOrWhiteSpace($BundlePath)) {
    $BundlePath = Join-Path $resolvedSptRoot 'BepInEx\plugins\HollywoodFX\hollywoodfx'
}
$resolvedBundlePath = [System.IO.Path]::GetFullPath($BundlePath)

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\release'
}
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

$requiredSptFile = Join-Path $resolvedSptRoot 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $requiredSptFile -PathType Leaf)) {
    throw "SptRoot is not an official build root with Assembly-CSharp.dll: $resolvedSptRoot"
}
if (-not (Test-Path -LiteralPath $resolvedBundlePath -PathType Leaf)) {
    throw "HollywoodFX asset bundle not found: $resolvedBundlePath"
}

Write-Host 'CHECK: verifying the external HollywoodFX asset bundle.'
$bundleHash = (Get-FileHash -LiteralPath $resolvedBundlePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($bundleHash -ne $ExpectedBundleSha256.ToUpperInvariant()) {
    throw "Asset bundle SHA-256 mismatch. Expected $ExpectedBundleSha256; found $bundleHash."
}

Write-Host 'CHECK: building HollywoodFX without deploying it.'
$buildArguments = @(
    'build',
    $projectPath,
    '--configuration',
    $Configuration,
    '--nologo',
    "-p:SptRoot=$resolvedSptRoot",
    '-p:TreatWarningsAsErrors=true'
)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$dllPath = Join-Path $repositoryRoot "HollywoodFX\bin\$Configuration\netstandard2.1\HollywoodFX.dll"
if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "Build succeeded but the expected DLL was not found: $dllPath"
}

New-Item -Path $resolvedOutputDirectory -ItemType Directory -Force | Out-Null
$zipPath = Join-Path $resolvedOutputDirectory "HollywoodFX-$Version.zip"
$checksumPath = "$zipPath.sha256"

if (Test-Path -LiteralPath $zipPath) {
    [System.IO.File]::Delete($zipPath)
}
if (Test-Path -LiteralPath $checksumPath) {
    [System.IO.File]::Delete($checksumPath)
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageEntries = @(
    [pscustomobject]@{
        Source = $dllPath
        Entry = 'BepInEx/plugins/HollywoodFX/HollywoodFX.dll'
    },
    [pscustomobject]@{
        Source = $resolvedBundlePath
        Entry = 'BepInEx/plugins/HollywoodFX/hollywoodfx'
    }
) | Sort-Object -Property Entry

Write-Host 'CHECK: creating the release archive.'
$zipStream = [System.IO.File]::Open(
    $zipPath,
    [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::ReadWrite,
    [System.IO.FileShare]::None
)
$archive = [System.IO.Compression.ZipArchive]::new(
    $zipStream,
    [System.IO.Compression.ZipArchiveMode]::Create,
    $false
)

try {
    foreach ($packageEntry in $packageEntries) {
        Add-DeterministicZipEntry -Archive $archive -SourcePath $packageEntry.Source -EntryName $packageEntry.Entry
    }
}
finally {
    $archive.Dispose()
    $zipStream.Dispose()
}

$readArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actualEntries = @(
        $readArchive.Entries |
            Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
            ForEach-Object { $_.FullName -replace '\\', '/' } |
            Sort-Object
    )
}
finally {
    $readArchive.Dispose()
}

$expectedEntries = @($packageEntries.Entry | Sort-Object)
$entryDifference = Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries
if ($null -ne $entryDifference) {
    throw "Release archive contents differ from the expected manifest: $($entryDifference | Out-String)"
}

$packageHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
$checksumLine = "$packageHash  $([System.IO.Path]::GetFileName($zipPath))`r`n"
[System.IO.File]::WriteAllText($checksumPath, $checksumLine, [System.Text.Encoding]::ASCII)

$packageItem = Get-Item -LiteralPath $zipPath
Write-Host "OK: $($packageItem.FullName)"
Write-Host "OK: $($packageItem.Length) bytes; SHA-256 $packageHash"

[pscustomobject]@{
    Package = $packageItem.FullName
    Bytes = $packageItem.Length
    Sha256 = $packageHash
    BundleSha256 = $bundleHash
}
