param(
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "Clippy.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed ($LASTEXITCODE)."
}

$exePath = Join-Path $publishDir "ClipRack.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Release executable was not created: $exePath"
}

foreach ($requiredFile in @("LICENSE", "THIRD_PARTY_NOTICES.md")) {
    $sourcePath = Join-Path $repoRoot $requiredFile
    $publishedPath = Join-Path $publishDir $requiredFile
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required license document is missing from the repository: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination $publishedPath -Force
}

Write-Host "Release build created: $exePath"
