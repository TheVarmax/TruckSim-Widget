param(
    [Parameter(Mandatory = $true)][string]$WidgetExecutable,
    [Parameter(Mandatory = $true)][string]$UpdaterExecutable
)

$ErrorActionPreference = 'Stop'

function Assert-ReleaseExecutable([string]$Path, [string]$RequiredMethod, [string[]]$Forbidden) {
    $item = Get-Item -LiteralPath $Path
    if ($item.Extension -ne '.exe') { throw 'Supply the published executable, not a DLL, PDB or test artifact.' }
    $bytes = [IO.File]::ReadAllBytes($item.FullName)
    if ($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) { throw 'Expected a Windows executable.' }
    $utf8 = [Text.Encoding]::UTF8.GetString($bytes)
    $utf16 = [Text.Encoding]::Unicode.GetString($bytes)
    $utf16Odd = [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
    # Require the application assembly inside the executable. Scanning a bare apphost
    # would otherwise pass even when its separate managed DLL contains simulation.
    if (-not $utf8.Contains($RequiredMethod)) {
        throw 'Expected an uncompressed single-file publish with embedded application metadata.'
    }
    foreach ($marker in $Forbidden) {
        if ($utf8.Contains($marker) -or $utf16.Contains($marker) -or $utf16Odd.Contains($marker)) {
            throw "Release executable contains a simulation marker: $marker"
        }
    }
    Write-Output "PASS: $($item.FullName) (SHA-256 $((Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash))"
}

Assert-ReleaseExecutable $WidgetExecutable 'CheckForUpdatesAsync' @(
    '--simulate-update', '--test-update', '--simulate-version', '--test-version',
    'CheckUpdateSimulationArgs', 'FindCandidateInstaller', '_isUpdateSimulationActive',
    'UPDATE SIMULATION MODE ACTIVE', 'Test Update Simulation'
)
Assert-ReleaseExecutable $UpdaterExecutable 'DownloadWithProgressAsync' @(
    '--simulate-update', 'TryGetLocalSimulationSource', 'Local update file not found:'
)
