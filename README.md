# CympleFaceTracking
The VRCFT module for [Project Cymple](https://github.com/Dominocs/Project_Cymple), a low cost and open source DIY eye tracking solution.

## Version 1.5.2

Compatible with the current Cymple 4.15.6 program and its 70-field UDP packet on port 22999.

- Install `CympleFaceTracking.dll` using VRCFaceTracking's custom module installer. The release ZIP includes the DLL and module metadata; VRCFaceTracking supplies the Core runtime.
- When using external VRCFaceTracking to receive data from Cymple itself, turn off Cymple's VRChat direct mode: Cymple sends compatibility packets only while direct mode is off. When loaded inside Cymple to receive data from another compatible application, this module follows the ordinary module enable/disable and priority controls.
- Eye and mouth availability comes directly from the host's `Initialize(eyeAvailable, expressionAvailable)` arguments. The module does not read `cymple_eye_sw` or `cymple_mouth_sw`, so Cymple's local hardware switches cannot disable another application's tracking input. Domain changes supplied by the host take effect when it reinitializes the module; packet flags indicate which domains have data.
- After one second without data for a tracking domain, its output follows `[Cymple_Mouth] reset_expression_on_tracking_disconnect` (default: reset; false: hold the last output). Only this disconnect preference is read from `%LOCALAPPDATA%\Cymple\iniFile.ini`, with the legacy `C:\Cymple\iniFile.ini` fallback, and refreshed once per second.
- The current Cymple sender can keep transmitting the last mouth sample after a mouth device disconnects. This packet format has no mouth-device freshness indicator, so the module can detect a stopped packet stream but cannot infer that device disconnect from repeated samples.
- Gaze and expressions retain Cymple's processed values; the eye deadzone is applied by Cymple. Pupil dilation uses the transmitted 0–1 range.

Build: `dotnet build CympleFaceTracking.csproj -c Release -p:Platform=x64`.
Regression checks: see [tests/README.md](tests/README.md).
