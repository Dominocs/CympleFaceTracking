﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

using VRCFaceTracking;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;


namespace CympleFaceTracking
{
    public class CympleFaceTrackingModule : ExtTrackingModule
    {
        static int FLAG_MOUTH_E = 0x01;
        static int FLAG_EYE_E = 0x02;
        private UdpClient _CympleFaceConnection;
        private IPEndPoint _CympleFaceRemoteEndpoint;
        private CympleFaceDataStructs _latestData;
        private (bool, bool) trackingSupported = (false, false);
        private volatile bool _isExiting = false;
        private bool disconnectWarned = false;
        private Thread _thread;
        private (bool, bool) validateModule = (false, false);
        

        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);
        // This is the first function ran by VRCFaceTracking. Make sure to completely initialize 
        // your tracking interface or the data to be accepted by VRCFaceTracking here. This will let 
        // VRCFaceTracking know what data is available to be sent from your tracking interface at initialization.
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
        string section, string key, string defaultValue,
        StringBuilder result, int size, string filePath);
        public bool GetBoolValue(string filePath, string section, string key, bool defaultValue = false)
        {
            const int bufferSize = 255;
            var result = new StringBuilder(bufferSize);
            // 默认开启
            if (defaultValue)
            {
                GetPrivateProfileString(section, key, "1", result, bufferSize, filePath);
            }else{
                GetPrivateProfileString(section, key, "", result, bufferSize, filePath);
            }


                string value = result.ToString().ToLower();
            // 支持"true"/"1"为真，"false"/"0"为假
            return value == "1" || value == "true";
        }

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "CympleFaceTracking";
            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            var img = a.GetManifestResourceStream("CympleFaceTracking.Assets.logo.png");
            Logger.LogInformation("Initializing CympleFaceTracking module.");

            // UPD client stuff
            _CympleFaceConnection = new UdpClient(Constants.Port);
            // 修改UDP客户端初始化
            _CympleFaceConnection.Client.ReceiveTimeout = 1000; // 设置1秒超时
            // bind port
            _CympleFaceRemoteEndpoint = new IPEndPoint(IPAddress.Any, Constants.Port);

            _latestData = new CympleFaceDataStructs();

            string iniDir = @"C:\Cymple\iniFile.ini";
            bool eyeEnabled = false;
            bool lipEnabled = false;
            if (!File.Exists(iniDir))
            {
                Logger.LogError("Cymple face INI file not found");
            }
            else{
                eyeEnabled = GetBoolValue(iniDir, "Function Switch", "cymple_eye_sw", true);
                lipEnabled = GetBoolValue(iniDir, "Function Switch", "cymple_mouth_sw", true);
            }
            Logger.LogInformation($"CympleFace module eye: {eyeEnabled} mouth: {lipEnabled}");
            trackingSupported = (eyeEnabled, lipEnabled);
            ModuleInformation.Name = "Cymple Facial Tracking V1.3.0";
            if (img != null)
            {
                List<Stream> streams = new List<Stream>();
                streams.Add(img);
                ModuleInformation.StaticImages = streams;
            }
            else
            {
                Logger.LogError("Could not find logo.");
            }
            _thread = new Thread(ReadLoop);
            _thread.Start();
            return trackingSupported;
        }
        private void ReadLoop()
        {
            Logger.LogInformation("CympleFace readLoop start.");
            while (!_isExiting)
            {
                // Read data from UDP
                try
                {
                    // Grab the packet - will block but with a timeout set in the init function
                    Byte[] receiveBytes = _CympleFaceConnection.Receive(ref _CympleFaceRemoteEndpoint);

                    if (receiveBytes.Length < 12) // At least prefix, flags, type, length
                    {
                        continue;
                    }

                    // Connection status handling
                    if (disconnectWarned)
                    {
                        Logger.LogInformation("cympleFace connection reestablished");
                        disconnectWarned = false;
                    }

                    // Read header fields with new format
                    int prefix = BitConverter.ToInt32(receiveBytes, 0);
                    uint flags = BitConverter.ToUInt32(receiveBytes, 4);
                    // Convert endianness if needed (assuming network byte order is big-endian)
                    if (BitConverter.IsLittleEndian)
                    {
                        flags = (uint)((flags & 0xFF) << 24 | (flags & 0xFF00) << 8 | (flags & 0xFF0000) >> 8 | (flags & 0xFF000000) >> 24);
                    }
                    ushort type = BitConverter.ToUInt16(receiveBytes, 8);
                    short length = BitConverter.ToInt16(receiveBytes, 10);

                    // Verify message prefix and type
                    if (prefix != Constants.MSG_PREFIX || type != Constants.OSC_MSG_BLENDSHAPEDATA)
                    {
                        Logger.LogWarning($"Invalid message: prefix={prefix:X}, type={type}");
                        continue;
                    }

                    // Set flags
                    _latestData.Flags = flags;

                    // Use a more efficient approach to read all blendshape values
                    ReadBlendshapeValues(receiveBytes, ref _latestData);
                }
                catch (SocketException se)
                {
                    HandleSocketException(se);
                }
                catch (Exception e)
                {
                    // some other exception
                    Logger.LogError(e.ToString());
                }
            }
            Logger.LogInformation("CympleFace readLoop end.");
        }
        // Polls data from the tracking interface.
        // VRCFaceTracking will run this function in a separate thread;
        public override void Update()
        {
            if (_isExiting) return;
            
            if (_CympleFaceConnection == null)
            {
                Logger.LogError("CympleFace connection is null");
                return;
            }

            // Update expressions with the latest data
            UpdateExpressions();
            
            // Add a small sleep to prevent CPU hogging, similar to ETVRTrackingModule
            Thread.Sleep(5);
        }
        private void UpdateExpressions()
        {
            // Update eye tracking data
            if ((_latestData.Flags & FLAG_EYE_E) != 0)
            {
                if (false == validateModule.Item1)
                {
                    validateModule.Item1 = true;
                    Logger.LogInformation("eye tracking activated");
                }
                else
                {
                    UpdateEyeData();
                }
            }
            
            // Update facial expressions
            if ((_latestData.Flags & FLAG_MOUTH_E) != 0)
            {
                if (false == validateModule.Item2)
                {
                    validateModule.Item2 = true;
                    Logger.LogInformation("facial expression tracking activated");
                }
                else
                {
                    UpdateFacialExpressions();
                }
            }
        }

        private void UpdateEyeData()
        {
            UnifiedTracking.Data.Eye.Left.Gaze = new Vector2(_latestData.EyeYaw_L, _latestData.EyePitch);
            UnifiedTracking.Data.Eye.Right.Gaze = new Vector2(_latestData.EyeYaw_R, _latestData.EyePitch);
            
            // Use raw eye openness values
            UnifiedTracking.Data.Eye.Left.Openness = 1.0f - _latestData.EyeLidCloseLeft;
            UnifiedTracking.Data.Eye.Right.Openness = 1.0f - _latestData.EyeLidCloseRight;

            // Eye wide values
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = _latestData.EyeWideLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideRight].Weight = _latestData.EyeWideRight;

            // Brow values
            if(_latestData.BrowLeftUpDown > 0)
            {
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = _latestData.BrowLeftUpDown;
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererLeft].Weight = 0;
            }else
            {
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = 0;
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererLeft].Weight = -_latestData.BrowLeftUpDown;
            }
            if(_latestData.BrowRightUpDown > 0)
            {
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpRight].Weight = _latestData.BrowRightUpDown;
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererRight].Weight = 0;
            }else
            {
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpRight].Weight = 0;
                UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererRight].Weight = -_latestData.BrowRightUpDown;
            }

                // Pupil dilation - raw values
                UnifiedTracking.Data.Eye._minDilation = 0;
            UnifiedTracking.Data.Eye._maxDilation = 10;
            UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = 5.0f + _latestData.Eye_Pupil_Left * 5.0f;
            UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = 5.0f + _latestData.Eye_Pupil_Right * 5.0f;
            
            // Eye squint - raw values
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight = _latestData.EyeSquintLeft;
            UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintRight].Weight = _latestData.EyeSquintRight;

        }

        private void UpdateFacialExpressions()
        {
            var shapes = UnifiedTracking.Data.Shapes;
            
            // Cheek region
            UpdateCheekExpressions(ref shapes);
            
            // Lip region
            UpdateLipExpressions(ref shapes);
            
            // Mouth region
            UpdateMouthExpressions(ref shapes);
            
            // Tongue region
            UpdateTongueExpressions(ref shapes);
        }

        private void UpdateCheekExpressions(ref UnifiedExpressionShape[] shapes)
        {
            shapes[(int)UnifiedExpressions.CheekPuffLeft].Weight = _latestData.CheekPuffLeft;
            shapes[(int)UnifiedExpressions.CheekPuffRight].Weight = _latestData.CheekPuffRight;
            shapes[(int)UnifiedExpressions.CheekSuckLeft].Weight = shapes[(int)UnifiedExpressions.CheekSuckRight].Weight = _latestData.CheekSuck;
        }

        private void UpdateLipExpressions(ref UnifiedExpressionShape[] shapes)
        {
            // Lip suck
            shapes[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipSuckUpperRight].Weight = _latestData.LipSuckUpper;
            
            shapes[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _latestData.LipSuckLower;
            
            // Lip raise/depress
            shapes[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = _latestData.LipRaise_L;
            shapes[(int)UnifiedExpressions.MouthUpperUpRight].Weight = _latestData.LipRaise_R;
            
            shapes[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = _latestData.LipDepress_L;
            shapes[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = _latestData.LipDepress_R;
            
            // Lip funnel/pucker
            shapes[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = 
            shapes[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _latestData.MouthFunnel;
            
            shapes[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = 
            shapes[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = _latestData.MouthPucker;
            
            // Lip shift
            HandleLipShift(ref shapes);
            
            // Mouth roll
            shapes[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipSuckUpperRight].Weight = _latestData.MouthRoll_Up;
            shapes[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = 
            shapes[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _latestData.MouthRoll_Down;
            shapes[(int)UnifiedExpressions.MouthRaiserLower].Weight = _latestData.MouthShrugLower;
        }

        private void HandleLipShift(ref UnifiedExpressionShape[] shapes)
        {
            // Upper lip shift
            if(_latestData.LipShift_Up > 0)
            {
                shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = 0.0f;
                shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = _latestData.LipShift_Up;
            }
            else
            {
                shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = -_latestData.LipShift_Up;
                shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = 0.0f;
            }
            
            // Lower lip shift
            if (_latestData.LipShift_Down > 0)
            {
                shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = 0;
                shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.LipShift_Down;
            }
            else
            {
                shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = -_latestData.LipShift_Down;
                shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = 0;
            }
        }

        private void UpdateMouthExpressions(ref UnifiedExpressionShape[] shapes)
        {
            // Jaw
            shapes[(int)UnifiedExpressions.JawOpen].Weight = _latestData.JawOpen;
            shapes[(int)UnifiedExpressions.JawForward].Weight = _latestData.JawFwd;
            
            // Jaw left/right
            if (_latestData.Jaw_Left_Right > 0)
            {
                shapes[(int)UnifiedExpressions.JawLeft].Weight = 0;
                shapes[(int)UnifiedExpressions.JawRight].Weight = _latestData.Jaw_Left_Right;
            }
            else
            {
                shapes[(int)UnifiedExpressions.JawLeft].Weight = -_latestData.Jaw_Left_Right;
                shapes[(int)UnifiedExpressions.JawRight].Weight = 0;
            }
            
            // Mouth left/right
            if (_latestData.Mouth_Left_Right > 0)
            {
                shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = 0;
                shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.Mouth_Left_Right;
            }
            else
            {
                shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = -_latestData.Mouth_Left_Right;
                shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = 0;
            }
            
            // Other mouth expressions
            shapes[(int)UnifiedExpressions.MouthClosed].Weight = _latestData.MouthClose;
            shapes[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _latestData.MouthSmileLeft;
            shapes[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = _latestData.MouthSmileLeft;
            shapes[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _latestData.MouthSmileRight;
            shapes[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = _latestData.MouthSmileRight;
            shapes[(int)UnifiedExpressions.MouthFrownLeft].Weight = _latestData.MouthSadLeft;
            shapes[(int)UnifiedExpressions.MouthFrownRight].Weight = _latestData.MouthSadRight;
        }

        private void UpdateTongueExpressions(ref UnifiedExpressionShape[] shapes)
        {
            shapes[(int)UnifiedExpressions.TongueOut].Weight = _latestData.TongueOut;
            
            // Tongue left/right
            if (_latestData.Tongue_Left_Right > 0)
            {
                shapes[(int)UnifiedExpressions.TongueLeft].Weight = 0;
                shapes[(int)UnifiedExpressions.TongueRight].Weight = _latestData.Tongue_Left_Right;
            }
            else
            {
                shapes[(int)UnifiedExpressions.TongueLeft].Weight = -_latestData.Tongue_Left_Right;
                shapes[(int)UnifiedExpressions.TongueRight].Weight = 0;
            }
            
            // Tongue up/down
            if (_latestData.Tongue_Up_Down >= 0)
            {
                shapes[(int)UnifiedExpressions.TongueUp].Weight = _latestData.Tongue_Up_Down;
                shapes[(int)UnifiedExpressions.TongueDown].Weight = 0.0f;
            }
            else
            {
                shapes[(int)UnifiedExpressions.TongueDown].Weight = -_latestData.Tongue_Up_Down;
                shapes[(int)UnifiedExpressions.TongueUp].Weight = 0.0f;
            }
            
            // Add new tongue parameters if supported by VRCFaceTracking
            if (Enum.IsDefined(typeof(UnifiedExpressions), "TongueRoll"))
                shapes[(int)UnifiedExpressions.TongueRoll].Weight = _latestData.TongueRoll;
                
            if (Enum.IsDefined(typeof(UnifiedExpressions), "TongueWide"))
                shapes[(int)UnifiedExpressions.TongueFlat].Weight = _latestData.TongueWide;
        }

        // Optimized method to read blendshape values
        private void ReadBlendshapeValues(byte[] receiveBytes, ref CympleFaceDataStructs trackingData)
        {
            int offset = 12; // Start after header (prefix, flags, type, length)
            int expectedLength = offset + (Constants.blendShapeNames.Length * 4);
            
            // Check if we have enough data for all blendshapes
            if (receiveBytes.Length < expectedLength)
            {
                Logger.LogWarning($"Message too short: got {receiveBytes.Length} bytes, expected {expectedLength}");
                return;
            }
            
            // More efficient bulk reading
            for (int i = 0; i < Constants.blendShapeNames.Length; i++)
            {
                float value = BitConverter.ToSingle(receiveBytes, offset);
                SetBlendshapeValue(ref trackingData, i, value);
                offset += 4;
            }
        }

        // Handle socket exceptions separately for cleaner code
        private void HandleSocketException(SocketException se)
        {
            if (se.SocketErrorCode == SocketError.TimedOut)
            {
                if (!disconnectWarned)
                {
                    Logger.LogWarning("cympleFace connection lost");
                    disconnectWarned = true;
                    validateModule = (false, false);
                }
            }
            else
            {
                // some other network socket exception
                Logger.LogError(se.ToString());
            }
        }

        // Helper method to set blendshape values by index
        private void SetBlendshapeValue(ref CympleFaceDataStructs data, int index, float value)
        {
            switch (index)
            {
                case 0: data.EyePitch = value; break;
                case 1: data.EyeYaw_L = value; break;
                case 2: data.EyeYaw_R = value; break;
                case 3: data.Eye_Pupil_Left = value; break;
                case 4: data.Eye_Pupil_Right = value; break;
                case 5: data.EyeLidCloseLeft = value; break;
                case 6: data.EyeLidCloseRight = value; break;
                case 7: data.EyeSquintLeft = value; break;
                case 8: data.EyeSquintRight = value; break;
                case 9: data.CheekPuffLeft = value; break;
                case 10: data.CheekPuffRight = value; break;
                case 11: data.CheekSuck = value; break;
                case 12: data.Jaw_Left_Right = value; break;
                case 13: data.JawOpen = value; break;
                case 14: data.JawFwd = value; break;
                case 15: data.MouthClose = value; break;
                case 16: data.Mouth_Left_Right = value; break;
                case 17: data.LipSuckUpper = value; break;
                case 18: data.LipSuckLower = value; break;
                case 19: data.MouthFunnel = value; break;
                case 20: data.MouthPucker = value; break;
                case 21: data.LipRaise_L = value; break;
                case 22: data.LipRaise_R = value; break;
                case 23: data.LipDepress_L = value; break;
                case 24: data.LipDepress_R = value; break;
                case 25: data.LipShift_Up = value; break;
                case 26: data.LipShift_Down = value; break;
                case 27: data.MouthRoll_Up = value; break;
                case 28: data.MouthRoll_Down = value; break;
                case 29: data.MouthShrugLower = value; break;
                case 30: data.MouthSmileLeft = value; break;
                case 31: data.MouthSmileRight = value; break;
                case 32: data.MouthSadLeft = value; break;
                case 33: data.MouthSadRight = value; break;
                case 34: data.TongueOut = value; break;
                case 35: data.Tongue_Left_Right = value; break;
                case 36: data.Tongue_Up_Down = value; break;
                case 37: data.TongueWide = value; break;
                case 38: data.TongueRoll = value; break;
                case 39: data.EyeWideLeft = value; break;
                case 40: data.EyeWideRight = value; break;
                case 41: data.BrowLeftUpDown = value; break;
                case 42: data.BrowRightUpDown = value; break;
            }
        }

        // Called when the module is unloaded or VRCFaceTracking itself tears down.
        public override void Teardown()
        {
            // Set exit flag first
            Logger.LogInformation("Tearing down CympleFaceTracking module...");
            _isExiting = true;
            
            try
            {
                // Close UDP client to interrupt any blocking receive operations
                if (_CympleFaceConnection != null)
                {
                    _CympleFaceConnection.Close();
                    _CympleFaceConnection.Dispose();
                    _CympleFaceConnection = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during teardown: {ex.Message}");
            }
            _thread.Join();
            Logger.LogInformation("CympleFaceTracking module teardown complete");
        }
    }
}
