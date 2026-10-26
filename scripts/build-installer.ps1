param(
    [string]$IsccPath,
    [string]$SignToolPath,
    [switch]$SkipPublish,
    [switch]$Sign
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "Clippy.csproj"
$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw "Project version is missing: $projectPath" }
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$exePath = Join-Path $publishDir "ClipRack.exe"
$installerScript = Join-Path $repoRoot "installer.iss"
$installerPath = Join-Path $repoRoot "installer_output\ClipRack_Setup_$version.exe"

function Resolve-SignTool {
    param([string]$Preferred)

    if (-not [string]::IsNullOrWhiteSpace($Preferred)) {
        if (-not (Test-Path -LiteralPath $Preferred)) { throw "signtool.exe was not found: $Preferred" }
        return (Resolve-Path -LiteralPath $Preferred).Path
    }

    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "${env:ProgramFiles}\Windows Kits\10\bin") |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    foreach ($root in $roots) {
        $candidates = Get-ChildItem -LiteralPath $root -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue
        $found = $candidates | Where-Object { $_.Directory.Name -eq "x64" } | Select-Object -First 1
        if (-not $found) { $found = $candidates | Where-Object { $_.Directory.Name -eq "x86" } | Select-Object -First 1 }
        if ($found) { return $found.FullName }
    }

    throw 'signtool.exe was not found. Install the Windows SDK or pass -SignToolPath "C:\Path\To\signtool.exe".'
}

function Get-SigningSettings {
    $certificate = $env:CODESIGN_CERT
    if ([string]::IsNullOrWhiteSpace($certificate)) {
        throw "-Sign was specified but the CODESIGN_CERT environment variable is not set."
    }

    $timestampUrl = $env:CODESIGN_TIMESTAMP_URL
    if ([string]::IsNullOrWhiteSpace($timestampUrl)) { $timestampUrl = "http://timestamp.digicert.com" }

    if (Test-Path -LiteralPath $certificate) {
        if ($certificate -notmatch '\.(pfx|p12)$') { throw "CODESIGN_CERT file must be a .pfx or .p12 certificate: $certificate" }
        $password = [string]$env:CODESIGN_PASSWORD
        $pfx = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            (Resolve-Path -LiteralPath $certificate).Path,
            $password,
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::My,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        try {
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($pfx)
        }
        catch {
            $store.Dispose()
            $pfx.Dispose()
            throw
        }
        $store.Close()
        return [pscustomobject]@{
            Mode = "Thumbprint"
            Certificate = $pfx.Thumbprint
            TimestampUrl = $timestampUrl
            ImportedCertificate = $pfx
        }
    }
    if ($certificate -match '\.(pfx|p12)$') { throw "Certificate file was not found: $certificate" }

    $thumbprint = ($certificate -replace '[\s:]', '')
    if ($thumbprint -match '^[0-9a-fA-F]{40}$') {
        return [pscustomobject]@{ Mode = "Thumbprint"; Certificate = $thumbprint; TimestampUrl = $timestampUrl }
    }
    return [pscustomobject]@{ Mode = "Subject"; Certificate = $certificate; TimestampUrl = $timestampUrl }
}

function Add-SigningCertificateArguments {
    param([System.Collections.Generic.List[string]]$Arguments, [object]$Settings)

    switch ($Settings.Mode) {
        "Thumbprint" { $Arguments.Add("/sha1"); $Arguments.Add($Settings.Certificate) }
        "Subject" { $Arguments.Add("/n"); $Arguments.Add($Settings.Certificate) }
    }
}

function Invoke-Sign {
    param([string]$Tool, [object]$Settings, [string]$Target)

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($arg in @("sign", "/fd", "SHA256", "/tr", $Settings.TimestampUrl, "/td", "SHA256")) { $arguments.Add($arg) }
    Add-SigningCertificateArguments -Arguments $arguments -Settings $Settings
    $arguments.Add("/v")
    $arguments.Add($Target)
    $argumentArray = $arguments.ToArray()
    & $Tool @argumentArray
    if ($LASTEXITCODE -ne 0) { throw "Code signing failed ($LASTEXITCODE): $Target" }
}

$signing = $null
$signTool = $null
if ($Sign) {
    $signTool = Resolve-SignTool -Preferred $SignToolPath
    $signing = Get-SigningSettings
}

try {
if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "build-release.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Release build failed ($LASTEXITCODE)." }
}
if (-not (Test-Path -LiteralPath $exePath)) { throw "Release executable was not found: $exePath" }

if ($Sign) {
    Invoke-Sign -Tool $signTool -Settings $signing -Target $exePath
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "${env:ProgramFiles}\Inno Setup 6\ISCC.exe")
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Inno Setup compiler was not found. Install Inno Setup or pass -IsccPath "C:\Path\To\ISCC.exe".'
}

$isccArguments = [System.Collections.Generic.List[string]]::new()
$isccArguments.Add("/DMyAppVersion=$version")
if ($Sign) {
    $isccArguments.Add("/DSIGN")
    $signCommand = "`$q$signTool`$q sign /fd SHA256 /tr $($signing.TimestampUrl) /td SHA256"
    switch ($signing.Mode) {
        "Thumbprint" { $signCommand += " /sha1 $($signing.Certificate)" }
        "Subject" { $signCommand += " /n `$q$($signing.Certificate)`$q" }
    }
    $signCommand += " /v `$f"
    $isccArguments.Add("/Sclippysign=$signCommand")
}
$isccArguments.Add($installerScript)
$isccArgumentArray = $isccArguments.ToArray()
& $IsccPath @isccArgumentArray
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed ($LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath $installerPath)) { throw "Installer was not created: $installerPath" }

if ($Sign) {
    foreach ($target in @($exePath, $installerPath)) {
        & $signTool verify /pa /all $target
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $target" }
    }
}

Write-Host "Installer created: $installerPath"
}
finally {
    if ($null -ne $signing -and $null -ne $signing.ImportedCertificate) {
        try {
            $cleanupStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
                [System.Security.Cryptography.X509Certificates.StoreName]::My,
                [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
            $cleanupStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $cleanupStore.Remove($signing.ImportedCertificate)
            $cleanupStore.Close()
            $signing.ImportedCertificate.Dispose()
        }
        catch {
            Write-Warning "Could not remove the temporary signing certificate from the current-user store: $($_.Exception.Message)"
        }
    }
}
