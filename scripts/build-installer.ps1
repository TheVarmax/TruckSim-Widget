param(
    [string]$Version = "1.6.4-beta.1",
    [string]$Configuration = "Release",
    [string]$PublishDir = "C:\Users\mrpry\Desktop\TruckSim Widget\TruckSim Widget ($Version)",
    [string]$OutputDir = "C:\Users\mrpry\Desktop\TruckSim Widget\Releases"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " TruckSim Widget - Custom Installer Build Pipeline" -ForegroundColor Cyan
Write-Host " Version       : $Version" -ForegroundColor Yellow
Write-Host " Configuration : $Configuration" -ForegroundColor Yellow
Write-Host " Publish Dir   : $PublishDir" -ForegroundColor Yellow
Write-Host " Output Dir    : $OutputDir" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

$RepoRoot = Split-Path -Parent $PSScriptRoot
$WidgetProject = Join-Path $RepoRoot "TruckSim Widget.csproj"
$InstallerProject = Join-Path $RepoRoot "TruckSimWidgetSetup\TruckSimWidgetSetup.csproj"
$InstallerResourcesDir = Join-Path $RepoRoot "TruckSimWidgetSetup\Resources"
$PayloadZipPath = Join-Path $InstallerResourcesDir "payload.zip"
$BundledPluginPath = Join-Path $InstallerResourcesDir "scs-telemetry.dll"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# 1. Publish Main Widget
Write-Host ""
Write-Host "[1/5] Publishing TruckSim Widget..." -ForegroundColor Green
dotnet publish $WidgetProject -c $Configuration -r win-x64 --self-contained false -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish TruckSim Widget."
}

# 2. Package Manifest and Payload
Write-Host ""
Write-Host "[2/5] Generating Manifest and Packaging Payload..." -ForegroundColor Green
if (-not (Test-Path $InstallerResourcesDir)) {
    New-Item -ItemType Directory -Path $InstallerResourcesDir -Force | Out-Null
}

# Copy scs-telemetry.dll as separate plugin resource
$SourcePlugin = Join-Path $PublishDir "plugin\scs-telemetry.dll"
if (Test-Path $SourcePlugin) {
    Copy-Item $SourcePlugin $BundledPluginPath -Force
    Write-Host "Bundled telemetry plugin copied to: $BundledPluginPath" -ForegroundColor DarkGray
} elseif (Test-Path (Join-Path $RepoRoot "plugin\scs-telemetry.dll")) {
    Copy-Item (Join-Path $RepoRoot "plugin\scs-telemetry.dll") $BundledPluginPath -Force
}

# Create temporary packaging directory for application files
$guid1 = [Guid]::NewGuid().ToString("N")
$TempStaging = Join-Path ([System.IO.Path]::GetTempPath()) ("tsw_package_" + $guid1)
New-Item -ItemType Directory -Path $TempStaging -Force | Out-Null

try {
    # Copy all application files except plugin/
    $Files = Get-ChildItem -Path $PublishDir -Recurse -File
    $ManifestEntries = @()

    $FullPublishDir = [System.IO.Path]::GetFullPath($PublishDir).TrimEnd('\')
    foreach ($File in $Files) {
        $RelPath = $File.FullName.Substring($FullPublishDir.Length + 1).Replace('\', '/')

        # STRICT RULE: scs-telemetry.dll is NOT part of application manifest
        if ($RelPath.StartsWith("plugin/", [System.StringComparison]::OrdinalIgnoreCase) -or
            $RelPath.Equals("plugin", [System.StringComparison]::OrdinalIgnoreCase) -or
            $File.Name.Equals("scs-telemetry.dll", [System.StringComparison]::OrdinalIgnoreCase) -or
            $File.Name.StartsWith("TruckSimWidgetSetup", [System.StringComparison]::OrdinalIgnoreCase) -or
            $File.Name.StartsWith("unins000", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        # Copy to staging
        $DestFile = Join-Path $TempStaging ($RelPath.Replace('/', '\'))
        $DestDir = Split-Path -Parent $DestFile
        if (-not (Test-Path $DestDir)) {
            New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
        }
        Copy-Item $File.FullName $DestFile -Force

        # Compute SHA-256
        $Sha = (Get-FileHash -Path $File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $ManifestEntries += @{
            path = $RelPath
            size = $File.Length
            sha256 = $Sha
        }
    }

    # Write manifest.json into staging
    $Manifest = @{
        version = $Version
        createdAt = [System.DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        files = $ManifestEntries
    }

    $ManifestJsonPath = Join-Path $TempStaging "manifest.json"
    $Manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $ManifestJsonPath -Encoding UTF8

    # Create payload.zip
    if (Test-Path $PayloadZipPath) {
        Remove-Item $PayloadZipPath -Force
    }
    Write-Host "Creating payload.zip with $($ManifestEntries.Count) application files..." -ForegroundColor DarkGray
    [System.IO.Compression.ZipFile]::CreateFromDirectory($TempStaging, $PayloadZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    Remove-Item -Path $TempStaging -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Publish Installer
Write-Host ""
Write-Host "[3/5] Publishing TruckSimWidgetSetup..." -ForegroundColor Green
$guid2 = [Guid]::NewGuid().ToString("N")
$TempInstallerOut = Join-Path ([System.IO.Path]::GetTempPath()) ("tsw_installer_out_" + $guid2)
dotnet publish $InstallerProject -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $TempInstallerOut
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish TruckSimWidgetSetup."
}

# 4. Copy Output Artifact
Write-Host ""
Write-Host "[4/5] Copying final release artifact..." -ForegroundColor Green
$FinalExeName = "TruckSimWidgetSetup-" + $Version + ".exe"
$FinalExePath = Join-Path $OutputDir $FinalExeName
$SourceExe = Join-Path $TempInstallerOut "TruckSimWidgetSetup.exe"

Copy-Item $SourceExe $FinalExePath -Force
Remove-Item -Path $TempInstallerOut -Recurse -Force -ErrorAction SilentlyContinue

# 5. Clean up temporary payload artifacts
Write-Host ""
Write-Host "[5/6] Cleaning up temporary payload artifacts..." -ForegroundColor Green
Remove-Item $PayloadZipPath -Force -ErrorAction SilentlyContinue
Remove-Item $BundledPluginPath -Force -ErrorAction SilentlyContinue

# 6. Output summary
$FinalFileInfo = Get-Item $FinalExePath
Write-Host ""
Write-Host "[6/6] Build Completed Successfully!" -ForegroundColor Green
Write-Host ("Artifact : " + $FinalExePath) -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

