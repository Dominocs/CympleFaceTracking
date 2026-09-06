using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;

namespace CympleFaceTracking
{
    public class CympleFaceTrackingModule : ExtTrackingModule
    {
        private const uint FLAG_MOUTH_E = 0x01;
        private const uint FLAG_EYE_E = 0x02;
        private const int TrackingTimeoutMs = 1000;
        private const string MouthSection = "Cymple_Mouth";
        private const string ResetExpressionOnDisconnectKey = "reset_expression_on_tracking_disconnect";
        private UdpClient? _CympleFaceConnection;
        private Thread? _thread;
        private volatile bool _isExiting;
        private readonly object _dataLock = new();
        private (bool Eye, bool Expression) trackingSupported;
        private CympleFaceDataStructs? _latestEyeData;
        private CympleFaceDataStructs? _latestExpressionData;
        private long _lastEyeFrameMs;
        private long _lastExpressionFrameMs;
        private bool _eyeOutputActive;
        private bool _expressionOutputActive;
        private bool _eyeHasOutput;
        private bool _expressionHasOutput;
        private bool _resetExpressionOnDisconnect = true;
        private long _nextSettingsReadMs;
        private static readonly CympleFaceDataStructs NeutralData = new();

        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string section, string key, string defaultValue,
            StringBuilder result, int size, string filePath);

        public bool GetBoolValue(string filePath, string section, string key, bool defaultValue = false)
        {
            var result = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue ? "1" : "0", result, result.Capacity, filePath);
            string value = result.ToString().Trim();
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1") return true;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0") return false;
            return defaultValue;
        }

        internal static string ResolveIniPath()
        {
            string? root = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string currentPath = Path.Combine(root, "Cymple", "iniFile.ini");
            const string legacyPath = @"C:\Cymple\iniFile.ini";
            return File.Exists(currentPath) || !File.Exists(legacyPath) ? currentPath : legacyPath;
        }

        private void RefreshSettings()
        {
            long now = Environment.TickCount64;
            if (now < _nextSettingsReadMs) return;
            _nextSettingsReadMs = now + 1000;
            string iniPath = ResolveIniPath();
            _resetExpressionOnDisconnect = GetBoolValue(iniPath, MouthSection, ResetExpressionOnDisconnectKey, true);
        }

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            _isExiting = false;
            _nextSettingsReadMs = 0;
            RefreshSettings();
            // The host owns domain selection; Cymple's local device switches do not control this module.
            trackingSupported = (eyeAvailable, expressionAvailable);
            var assembly = typeof(CympleFaceTrackingModule).Assembly;
            var version = assembly.GetName().Version!;
            ModuleInformation.Name = $"Cymple Facial Tracking V{version.Major}.{version.Minor}.{version.Build}";
            Logger.LogInformation("Initializing {Name}; eye: {Eye}, mouth: {Mouth}; settings: {Path}",
                ModuleInformation.Name, trackingSupported.Eye, trackingSupported.Expression, ResolveIniPath());
            if (!trackingSupported.Eye && !trackingSupported.Expression) return (false, false);

            try
            {
                _CympleFaceConnection = new UdpClient(Constants.Port);
                // A synchronous receive on the owned background thread makes timeout and teardown observable.
                _CympleFaceConnection.Client.ReceiveTimeout = 250;
            }
            catch (SocketException ex)
            {
                Logger.LogError(ex, "Could not bind Cymple UDP port {Port}", Constants.Port);
                _CympleFaceConnection?.Dispose();
                _CympleFaceConnection = null;
                trackingSupported = (false, false);
                return (false, false);
            }

            var img = assembly.GetManifestResourceStream("CympleFaceTracking.Assets.logo.png");
            if (img != null) ModuleInformation.StaticImages = new List<Stream> { img };
            lock (_dataLock)
            {
                _latestEyeData = _latestExpressionData = null;
                _eyeOutputActive = _expressionOutputActive = false;
                _eyeHasOutput = _expressionHasOutput = false;
            }
            UdpClient connection = _CympleFaceConnection;
            _thread = new Thread(() => ReadLoop(connection)) { IsBackground = true, Name = "CympleFace UDP" };
            _thread.Start();
            return trackingSupported;
        }

        private void ReadLoop(UdpClient connection)
        {
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            while (!_isExiting)
            {
                try
                {
                    ProcessPacket(connection.Receive(ref remoteEndpoint));
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Update() expires each tracking domain even if the other domain keeps arriving.
                }
                catch (ObjectDisposedException) when (_isExiting) { break; }
                catch (SocketException) when (_isExiting) { break; }
                catch (SocketException ex)
                {
                    Logger.LogError(ex, "Cymple UDP receive failed");
                    break;
                }
            }
        }

        internal static bool TryParsePacket(byte[] packet, out CympleFaceDataStructs data)
        {
            data = new CympleFaceDataStructs();
            // Cymple serializes the Windows struct directly; only Flags is converted to big endian.
            if (packet.Length != Constants.PacketSize
                || BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(0, 4)) != Constants.MSG_PREFIX
                || BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(8, 2)) != Constants.OSC_MSG_BLENDSHAPEDATA
                || BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(10, 2)) != Constants.PayloadSize)
                return false;

            data.Flags = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(4, 4));
            for (int i = 0; i < Constants.BlendShapeCount; i++)
            {
                float value = BinaryPrimitives.ReadSingleLittleEndian(packet.AsSpan(Constants.HeaderSize + i * 4, 4));
                if (!float.IsFinite(value)) return false;
                SetBlendshapeValue(ref data, i, Math.Clamp(value, i < 4 ? -1.0f : 0.0f, 1.0f));
            }
            return true;
        }

        private void ProcessPacket(byte[] packet)
        {
            if (!TryParsePacket(packet, out var data)) return;
            lock (_dataLock)
            {
                if (_isExiting) return;
                long now = Environment.TickCount64;
                if ((data.Flags & FLAG_EYE_E) != 0)
                {
                    _latestEyeData = data;
                    _lastEyeFrameMs = now;
                }
                if ((data.Flags & FLAG_MOUTH_E) != 0)
                {
                    _latestExpressionData = data;
                    _lastExpressionFrameMs = now;
                }
            }
        }

        public override void Update()
        {
            if (_isExiting) return;
            UpdateExpressions();
            Thread.Sleep(10);
        }

        private void UpdateExpressions()
        {
            RefreshSettings();
            lock (_dataLock)
            {
                if (_isExiting) return;
                long now = Environment.TickCount64;
                bool eyeActive = trackingSupported.Eye && _latestEyeData != null
                    && now - _lastEyeFrameMs < TrackingTimeoutMs;
                bool expressionActive = trackingSupported.Expression && _latestExpressionData != null
                    && now - _lastExpressionFrameMs < TrackingTimeoutMs;
                if (eyeActive)
                {
                    if (!_eyeOutputActive) Logger.LogInformation("Eye tracking activated");
                    UpdateEyeData(_latestEyeData!);
                    _eyeHasOutput = true;
                }
                else
                {
                    if (_eyeHasOutput && _resetExpressionOnDisconnect)
                    {
                        UpdateEyeData(NeutralData);
                        _eyeHasOutput = false;
                    }
                    if (_eyeOutputActive) Logger.LogInformation("Eye tracking stopped");
                }
                if (expressionActive)
                {
                    if (!_expressionOutputActive) Logger.LogInformation("Facial expression tracking activated");
                    UpdateFacialExpressions(_latestExpressionData!);
                    _expressionHasOutput = true;
                }
                else
                {
                    if (_expressionHasOutput && _resetExpressionOnDisconnect)
                    {
                        UpdateFacialExpressions(NeutralData);
                        _expressionHasOutput = false;
                    }
                    if (_expressionOutputActive) Logger.LogInformation("Facial expression tracking stopped");
                }
                _eyeOutputActive = eyeActive;
                _expressionOutputActive = expressionActive;
            }
        }

        private void UpdateEyeData(CympleFaceDataStructs data)
        {
            UnifiedTracking.Data.Eye.Left.Gaze = new Vector2(data.EyeYaw_L, data.EyePitch_L);
            UnifiedTracking.Data.Eye.Right.Gaze = new Vector2(data.EyeYaw_R, data.EyePitch_R);

            // Use raw eye openness values
            UnifiedTracking.Data.Eye.Left.Openness = 1.0f - data.EyeLidCloseLeft;
            UnifiedTracking.Data.Eye.Right.Openness = 1.0f - data.EyeLidCloseRight;

            // Eye wide values
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = data.EyeWideLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideRight].Weight = data.EyeWideRight;

            // Brow values
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererLeft].Weight = data.BrowLowererLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererRight].Weight = data.BrowLowererRight;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = data.BrowInnerUpLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpRight].Weight = data.BrowInnerUpRight;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = data.BrowOuterUpLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpRight].Weight = data.BrowOuterUpRight;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowPinchLeft].Weight = data.BrowPinchLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowPinchRight].Weight = data.BrowPinchRight;
            // Pupil dilation - raw values
            UnifiedTracking.Data.Eye._minDilation = 0;
            UnifiedTracking.Data.Eye._maxDilation = 1;
            UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = data.Eye_Pupil_Left;
            UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = data.Eye_Pupil_Right;

            // Eye squint - raw values
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight = data.EyeSquintLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintRight].Weight = data.EyeSquintRight;

        }

        private void UpdateFacialExpressions(CympleFaceDataStructs data)
        {
            var shapes = UnifiedTracking.Data.Shapes;

            #region Direct Jaw

            shapes[(int)UnifiedExpressions.JawOpen].Weight = data.JawOpen;
            shapes[(int)UnifiedExpressions.JawForward].Weight = data.JawFwd;
            shapes[(int)UnifiedExpressions.MouthClosed].Weight = data.MouthClosed;

            shapes[(int)UnifiedExpressions.JawLeft].Weight = data.JawLeft;
            shapes[(int)UnifiedExpressions.JawRight].Weight = data.JawRight;

            #endregion

            #region Direct Mouth and Lip
            shapes[(int)UnifiedExpressions.MouthUpperUpLeft].Weight =
            shapes[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = data.LipRaiseUL;
            shapes[(int)UnifiedExpressions.MouthUpperUpRight].Weight =
            shapes[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = data.LipRaiseUR;

            shapes[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = data.LipDepressDL;
            shapes[(int)UnifiedExpressions.MouthLowerDownRight].Weight = data.LipDepressDR;

            shapes[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight =
            shapes[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = data.MouthPuckerUpper;
            shapes[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight =
            shapes[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = data.MouthPuckerLower;

            shapes[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = data.MouthFunnelUL;
            shapes[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = data.MouthFunnelUR;
            shapes[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = data.MouthFunnelDL;
            shapes[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = data.MouthFunnelDR;

            shapes[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = data.LipSuckUL;
            shapes[(int)UnifiedExpressions.LipSuckUpperRight].Weight = data.LipSuckUR;
            shapes[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = data.LipSuckDL;
            shapes[(int)UnifiedExpressions.LipSuckLowerRight].Weight = data.LipSuckDR;

            shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = data.MouthUpperShiftLeft;
            shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = data.MouthUpperShiftRight;
            shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = data.MouthLowerShiftLeft;
            shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = data.MouthLowerShiftRight;

            shapes[(int)UnifiedExpressions.MouthCornerPullLeft].Weight =
            shapes[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = data.MouthSmileLeft;
            shapes[(int)UnifiedExpressions.MouthCornerPullRight].Weight =
            shapes[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = data.MouthSmileRight;

            shapes[(int)UnifiedExpressions.MouthFrownLeft].Weight = data.MouthFrownLeft;
            shapes[(int)UnifiedExpressions.MouthFrownRight].Weight = data.MouthFrownRight;

            shapes[(int)UnifiedExpressions.MouthRaiserUpper].Weight = data.MouthShrugUpper;
            shapes[(int)UnifiedExpressions.MouthRaiserLower].Weight = data.MouthShrugLower;

            shapes[(int)UnifiedExpressions.CheekSquintLeft].Weight = data.CheekSquintLeft;
            shapes[(int)UnifiedExpressions.CheekSquintRight].Weight = data.CheekSquintRight;

            shapes[(int)UnifiedExpressions.MouthDimpleLeft].Weight = data.MouthDimpleLeft;
            shapes[(int)UnifiedExpressions.MouthDimpleRight].Weight = data.MouthDimpleRight;

            shapes[(int)UnifiedExpressions.MouthStretchLeft].Weight = data.MouthStretchLeft;
            shapes[(int)UnifiedExpressions.MouthStretchRight].Weight = data.MouthStretchRight;

            shapes[(int)UnifiedExpressions.MouthRaiserUpper].Weight = data.MouthShrugUpper;
            shapes[(int)UnifiedExpressions.MouthRaiserLower].Weight = data.MouthShrugLower;
            #endregion

            #region Direct Cheek

            shapes[(int)UnifiedExpressions.CheekPuffLeft].Weight = data.CheekPuffLeft;
            shapes[(int)UnifiedExpressions.CheekPuffRight].Weight = data.CheekPuffRight;
            shapes[(int)UnifiedExpressions.CheekSuckLeft].Weight =  data.CheekSuckLeft;
            shapes[(int)UnifiedExpressions.CheekSuckRight].Weight = data.CheekSuckRight;
            #endregion

            #region Direct Tongue

            shapes[(int)UnifiedExpressions.TongueOut].Weight = data.TongueOut;
            shapes[(int)UnifiedExpressions.TongueLeft].Weight = data.TongueLeft;
            shapes[(int)UnifiedExpressions.TongueRight].Weight = data.TongueRight;
            shapes[(int)UnifiedExpressions.TongueUp].Weight = data.TongueUp;
            shapes[(int)UnifiedExpressions.TongueDown].Weight = data.TongueDown;
            shapes[(int)UnifiedExpressions.TongueCurlUp].Weight = data.TongueCurlUp;
            shapes[(int)UnifiedExpressions.TongueBendDown].Weight = data.TongueBendDown;
            shapes[(int)UnifiedExpressions.TongueFlat].Weight = data.TongueFlat;
            shapes[(int)UnifiedExpressions.TongueRoll].Weight = data.TongueRoll;
            #endregion

            #region Direct Nose
            shapes[(int)UnifiedExpressions.NoseSneerLeft].Weight = data.NoseSneerLeft;
            shapes[(int)UnifiedExpressions.NoseSneerRight].Weight = data.NoseSneerRight;
            #endregion
        }

        // Helper method to set blendshape values by index
        private static void SetBlendshapeValue(ref CympleFaceDataStructs data, int index, float value)
        {
            switch (index)
            {
                case 0:  data.EyePitch_L = value; break;
                case 1:  data.EyePitch_R = value; break;
                case 2:  data.EyeYaw_L = value; break;
                case 3:  data.EyeYaw_R = value; break;
                case 4:  data.Eye_Pupil_Left = value; break;
                case 5:  data.Eye_Pupil_Right = value; break;
                case 6:  data.EyeLidCloseLeft = value; break;
                case 7:  data.EyeLidCloseRight = value; break;
                case 8:  data.EyeSquintLeft = value; break;
                case 9:  data.EyeSquintRight = value; break;
                case 10: data.EyeWideLeft = value; break;
                case 11: data.EyeWideRight = value; break;
                case 12: data.BrowLowererLeft = value; break;
                case 13: data.BrowLowererRight = value; break;
                case 14: data.BrowInnerUpLeft = value; break;
                case 15: data.BrowInnerUpRight = value; break;
                case 16: data.BrowOuterUpLeft = value; break;
                case 17: data.BrowOuterUpRight = value; break;
                case 18: data.BrowPinchLeft = value; break;
                case 19: data.BrowPinchRight = value; break;
                case 20: data.NoseSneerLeft = value; break;
                case 21: data.NoseSneerRight = value; break;
                case 22: data.CheekSquintLeft = value; break;
                case 23: data.CheekSquintRight = value; break;
                case 24: data.CheekPuffLeft = value; break;
                case 25: data.CheekPuffRight = value; break;
                case 26: data.CheekSuckLeft = value; break;
                case 27: data.CheekSuckRight = value; break;
                case 28: data.JawLeft = value; break;
                case 29: data.JawRight = value; break;
                case 30: data.JawFwd = value; break;
                case 31: data.JawOpen = value; break;
                case 32: data.MouthClosed = value; break;
                case 33: data.MouthUpperShiftLeft = value; break;
                case 34: data.MouthUpperShiftRight = value; break;
                case 35: data.MouthLowerShiftLeft = value; break;
                case 36: data.MouthLowerShiftRight = value; break;
                case 37: data.LipSuckUL = value; break;
                case 38: data.LipSuckUR = value; break;
                case 39: data.LipSuckDL = value; break;
                case 40: data.LipSuckDR = value; break;
                case 41: data.MouthFunnelUL = value; break;
                case 42: data.MouthFunnelUR = value; break;
                case 43: data.MouthFunnelDL = value; break;
                case 44: data.MouthFunnelDR = value; break;
                case 45: data.MouthPuckerUpper = value; break;
                case 46: data.MouthPuckerLower = value; break;
                case 47: data.LipRaiseUL = value; break;
                case 48: data.LipRaiseUR = value; break;
                case 49: data.LipDepressDL = value; break;
                case 50: data.LipDepressDR = value; break;
                case 51: data.MouthSmileLeft = value; break;
                case 52: data.MouthSmileRight = value; break;
                case 53: data.MouthDimpleLeft = value; break;
                case 54: data.MouthDimpleRight = value; break;
                case 55: data.MouthFrownLeft = value; break;
                case 56: data.MouthFrownRight = value; break;
                case 57: data.MouthStretchLeft = value; break;
                case 58: data.MouthStretchRight = value; break;
                case 59: data.MouthShrugUpper = value; break;
                case 60: data.MouthShrugLower = value; break;
                case 61: data.TongueOut = value; break;
                case 62: data.TongueLeft = value; break;
                case 63: data.TongueRight = value; break;
                case 64: data.TongueUp = value; break;
                case 65: data.TongueDown = value; break;
                case 66: data.TongueCurlUp = value; break;
                case 67: data.TongueBendDown = value; break;
                case 68: data.TongueFlat = value; break;
                case 69: data.TongueRoll = value; break;
            }
        }

        public override void Teardown()
        {
            _isExiting = true;
            _CympleFaceConnection?.Dispose();
            _CympleFaceConnection = null;
            _thread?.Join();
            _thread = null;
            lock (_dataLock)
            {
                _latestEyeData = _latestExpressionData = null;
                _eyeOutputActive = _expressionOutputActive = false;
                _eyeHasOutput = _expressionHasOutput = false;
            }
            Logger.LogInformation("CympleFaceTracking module teardown complete");
        }
    }
}
