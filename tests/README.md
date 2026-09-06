# Regression checks

Run in PowerShell with the .NET 10 SDK installed:

```powershell
.\tests\run.ps1
# If the Cymple application source is elsewhere:
.\tests\run.ps1 -CympleSource 'E:\path\to\Cymple'
```

The runner builds the original .NET 7 x64 module against the bundled VRCFaceTracking Core SDK, then loads that binary into a separate .NET 10 console test process. No test framework or VRCFaceTracking installation is required.

Coverage includes the current C++ sender's 70-field wire order, mixed-endian header, invalid lengths and non-finite values, complete expression mapping, first-frame delivery, interleaved eye/mouth updates, host-controlled domains despite conflicting or changing INI switches, independent disconnect timeouts, the reset preference, INI booleans, and real localhost UDP reception, idle timeout, teardown and reinitialization with different host availability.

INI parsing and module initialization use temporary fixtures, with `LOCALAPPDATA` redirected only in the test process. The real user INI is never edited. The test uses an ephemeral UDP port and does not occupy the normal port 22999. Source/build/localhost validation does not constitute physical tracker or live VRChat validation.
