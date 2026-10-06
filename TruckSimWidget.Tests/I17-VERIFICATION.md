# I17 update simulation verification

The existing ResumeVerification harness has an opt-in mode that compiles only
UpdateSimulationBuildTests and references Widget/Updater. The default legacy
suite is unchanged; its known installer/API incompatibilities are outside I17.

Run from the repository root on Windows:

```powershell
dotnet test TruckSimWidget.Tests/ResumeVerification.csproj -c Debug -p:UpdateSimulationVerification=true -m:1 /nodeReuse:false
dotnet test TruckSimWidget.Tests/ResumeVerification.csproj -c Release -p:UpdateSimulationVerification=true -m:1 /nodeReuse:false
dotnet build 'TruckSim Widget.slnx' -c Release -m:1 /nodeReuse:false
dotnet publish 'TruckSim Widget.csproj' -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -m:1 /nodeReuse:false
./TruckSimWidget.Tests/VerifyUpdateSimulationArtifacts.ps1 -WidgetExecutable 'bin/Release/net8.0-windows/win-x64/publish/TruckSim Widget.exe' -UpdaterExecutable 'bin/Release/net8.0-windows/win-x64/publish/updater.exe'
```

The artifact assertion scans only explicitly supplied final EXEs (UTF-8 metadata
and UTF-16 strings). It requires embedded application method names, rejecting a
bare apphost that would hide its managed implementation in a separate DLL.
PDBs, test assemblies and source files are not scanned.

Tests inspect actual compiled members/IL, exercise Debug argument aliases and
local raw/file-URI copying, reject Release local sources/flags, and exercise the
unchanged HTTP downloader on loopback. GitHub policy is checked separately with
positive/negative URLs. Compiled updater IL retains the download → SHA-256 →
comparison → executable check → installer launch sequence. Tests do not launch
an installer, modify an installed Widget, or publish a GitHub release.

For interactive Debug simulation, place the **Debug** updater.exe and its build
dependencies beside the Debug Widget executable, then start Widget with
`--simulate-update <local installer.exe> --simulate-version 9.9.9`.
The required asset-name and checksum checks still apply. Release Updater rejects
local inputs even if invoked by Debug Widget. No release URL policy is relaxed.
