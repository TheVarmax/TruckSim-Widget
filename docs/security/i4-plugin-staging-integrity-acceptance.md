# I4 Windows VM acceptance

Run on a disposable Windows VM snapshot, using the I4 installer build. Do not use
production game libraries, registration, installer state, or release publishing.
Use a split-token administrator account so UAC elevation retains the same user
profile and TEMP directory. Actual UAC and protected-target ACL validation remain
manual acceptance checks; unit tests substitute the elevated operation.

## Preparation

1. Record the I4 commit and the SHA-256 of the bundled `plugin/scs-telemetry.dll`.
   The runtime expected digest is computed from the installer payload stream;
   telemetry DLLs are intentionally excluded from the application manifest.
2. Prepare a real ETS2 or ATS game tree with its actual executable in
   `bin/win_x64`. Use one protected tree beneath Program Files and one writable
   Steam library. Preserve the helper's existing trusted-game-root ACL policy.
   If the protected tree fails that policy, record the rejection; do not weaken
   ACL/source/path validation to make the test pass.
3. Record any original plugin's bytes and SHA-256, and snapshot the VM. Start each
   scenario from the snapshot unless explicitly testing a subsequent uninstall
   or recovery. Close the game except for the locked-file regression.
4. Keep installer logs and the pending transaction journal as test evidence.
   The journal and permanent installer state remain under
   `%LOCALAPPDATA%/TruckSimWidget/installer`. Plugin transaction staging is under
   `%TEMP%/TruckSimWidget_Staging_<unique>` and is recorded as `pluginStagingDir`.

## Protected game directory

1. Select the protected game's telemetry plugin, install, and accept UAC.
2. Confirm the helper receives a TEMP source with an allowed plugin filename and
   the exact game's `bin/win_x64/plugins/scs-telemetry.dll` destination.
3. Confirm installation succeeds only after destination SHA-256 equals the
   bundled plugin digest. Check ownership/install state contains that digest.
4. Confirm temporary plugin staging was removed after transaction commit and
   permanent installer state was retained.
5. Repeat as an update/replacement with an existing Widget plugin.

## UAC cancelled

1. Start from the same protected tree and record its initial plugin contents.
2. Cancel the copy UAC prompt. Confirm installer failure, not successful update
   or a successful skip.
3. Confirm rollback leaves the original plugin byte-for-byte intact, does not
   write successful installation state, and removes temporary staging when
   rollback succeeds. An already unchanged original must not need a second UAC
   prompt merely to rewrite identical bytes.
4. For a genuinely modified target requiring elevated restore, cancel recovery
   once. Confirm the journal, original backup, and verified helper are retained;
   restart, accept recovery UAC, and confirm successful restoration and cleanup.

## Writable Steam library

1. Install into the writable game's plugin directory. Confirm no UAC request.
2. Compare destination SHA-256 with the bundled digest and verify normal state.
3. Repeat replacement/update. With the game holding its original DLL open,
   confirm the pre-copy skip preserves the DLL and does not elevate. A target
   becoming unreadable after a copy must instead cause failure and rollback.

## Existing third-party plugin

1. Put a known third-party DLL in each game tree; record its bytes and digest.
2. Choose Backup and Replace, install, then uninstall. Accept UAC where needed.
3. Confirm the third-party DLL is restored byte-for-byte and its persistent
   backup is deleted only after successful destination verification.
4. Repeat with a simulated copy returning success but wrong destination bytes.
   Confirm failure/rollback and preservation of the original backup. Automated
   tests cover this simulation without running a modified elevated executable.

## Interrupted installation and old journal

1. Under a debugger, stop after plugin copy returns but before
   `journal.CompleteStep(copyStep, installedHash)`. Terminate only the VM's
   installer process; retain its pending journal and staging files.
2. Restart the I4 installer. Confirm recovery restores the original third-party
   DLL (or removes a newly created Widget DLL), verifies the result, then removes
   the journal and plugin staging. Do not continue installation after failed
   recovery.
3. Repeat a first-install crash before install-state is saved. Recovery must use
   a helper extracted and verified against this installer's embedded payload,
   without treating a mutable journal or installed manifest as an executable
   trust anchor. Its temporary helper directory must be cleaned after use.
4. Repeat with a pre-I4 pending journal: no `pluginStagingDir` property and
   rollback backups beneath LocalAppData `TruckSimWidget/installer/staging`.
   Confirm those paths are read and verified, then restaged beneath TEMP before
   elevated restore. They must not be submitted directly to the helper or
   rejected merely because they are legacy paths.
5. Corrupt a rollback backup in the VM. Confirm recovery refuses destructive
   restore, preserves the target and pending journal, and keeps the helper for
   a later retry. Never report this state as successfully recovered.

## Automated checks

Release solution build:

```powershell
dotnet build 'TruckSim Widget.slnx' -c Release -m:1 /nodeReuse:false
```

The existing `TruckSimWidget.Tests/ManifestPathSecurity.Tests.csproj` harness now
includes installer and I4 integrity tests. Build it with the normally generated
installer payload and bundled plugin paths; the installer project requires those
resources for compilation. No new I4 test project or release pipeline is added.

```powershell
dotnet test TruckSimWidget.Tests/ManifestPathSecurity.Tests.csproj -c Release -m:1 /nodeReuse:false "-p:InstallerPayloadPath=<absolute payload.zip>" "-p:InstallerPluginPath=<absolute bundled scs-telemetry.dll>"
git diff --check
```

On a developer's real computer, exclude
`TransactionRollback_UnregistersWindowsRegistration` using the test filter
`FullyQualifiedName!~TransactionRollback_UnregistersWindowsRegistration`: that
existing test writes and deletes the real product's Windows uninstall registry
entry. Run the unfiltered harness only in the disposable VM.
