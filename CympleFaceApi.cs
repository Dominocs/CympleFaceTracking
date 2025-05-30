using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.InteropServices;

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
        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);
        // This is the first function ran by VRCFaceTracking. Make sure to completely initialize 
        // your tracking interface or the data to be accepted by VRCFaceTracking here. This will let 
        // VRCFaceTracking know what data is available to be sent from your tracking interface at initialization.
        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "CympleFaceTracking";
            Logger.LogInformation("Initializing CympleFaceTracking module");

            // UPD client stuff
            _CympleFaceConnection = new UdpClient(Constants.Port);
            // 修改UDP客户端初始化
            _CympleFaceConnection.Client.ReceiveTimeout = 1000; // 设置1秒超时
            // bind port
            _CympleFaceRemoteEndpoint = new IPEndPoint(IPAddress.Any, Constants.Port);

            _latestData = new CympleFaceDataStructs();

            trackingSupported = (true, true);
            return trackingSupported;
        }

        // Polls data from the tracking interface.
        // VRCFaceTracking will run this function in a separate thread;
        public override void Update()
        {
            while (!_isExiting)
            {
                try
                {
                    // 检查连接是否有效
                    if (_CympleFaceConnection == null)
                        break;
                    
                    // 获取最新数据
                    if (ReadData(_CympleFaceConnection, _CympleFaceRemoteEndpoint, ref _latestData))
                    {
                        UpdateLowerFaceExpression(ref UnifiedTracking.Data.Shapes, ref _latestData);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // UDP客户端已被关闭，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in Update: {ex.Message}");
                }
                
                Thread.Sleep(10);
            }
        }
        private void UpdateLowerFaceExpression(ref UnifiedExpressionShape[] unifiedExpressions, ref CympleFaceDataStructs _latestData) {
            if ((_latestData.Flags & FLAG_MOUTH_E) != 0)
            {
                #region Cheek
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = _latestData.CheekPuffLeft;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = _latestData.CheekPuffRight;
                unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight =  unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = _latestData.CheekSuck;
                #endregion
                
                #region Lip
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = _latestData.LipSuckUpper;
                
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _latestData.LipSuckLower;
                
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = _latestData.LipRaise_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = _latestData.LipRaise_R;
                
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = _latestData.LipDepress_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = _latestData.LipDepress_R;
                
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _latestData.MouthFunnel;
                
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = 
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = _latestData.MouthPucker;
                
                if(_latestData.LipShift_Up > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = 0.0f;
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = _latestData.LipShift_Up;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = -_latestData.LipShift_Up;
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = 0.0f;
                }
                
                if (_latestData.LipShift_Down > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = 0;
                    unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.LipShift_Down;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = -_latestData.LipShift_Down;
                    unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = 0;
                }
                
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight =_latestData.MouthRoll_Up;
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = _latestData.MouthRoll_Down;
                unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = _latestData.MouthShrugLower;
                #endregion
                
                #region Mouth
                unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = _latestData.JawOpen;
                unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = _latestData.JawFwd;
                
                if (_latestData.Jaw_Left_Right > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = 0;
                    unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = _latestData.Jaw_Left_Right;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = -_latestData.Jaw_Left_Right;
                    unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = 0;
                }
                
                if (_latestData.Mouth_Left_Right > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = 0;
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.Mouth_Left_Right;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = -_latestData.Mouth_Left_Right;
                    unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = 0;
                }
                
                unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = _latestData.MouthClose;
                unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _latestData.MouthSmileLeft;
                unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _latestData.MouthSmileRight;
                unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = _latestData.MouthSadLeft;
                unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = _latestData.MouthSadRight;
                #endregion
                
                #region Tongue
                unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = _latestData.TongueOut;
                
                if (_latestData.Tongue_Left_Right > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = 0;
                    unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = _latestData.Tongue_Left_Right;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = -_latestData.Tongue_Left_Right;
                    unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = 0;
                }
                
                if (_latestData.Tongue_Up_Down >= 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = _latestData.Tongue_Up_Down;
                    unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = 0.0f;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = -_latestData.Tongue_Up_Down;
                    unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = 0.0f;
                }
                
                // Add new tongue parameters if supported by VRCFaceTracking
                if (Enum.IsDefined(typeof(UnifiedExpressions), "TongueRoll"))
                    unifiedExpressions[(int)UnifiedExpressions.TongueRoll].Weight = _latestData.TongueRoll;
                    
                if (Enum.IsDefined(typeof(UnifiedExpressions), "TongueWide"))
                    unifiedExpressions[(int)UnifiedExpressions.TongueFlat].Weight = _latestData.TongueWide;
                #endregion
            }
            
            if ((_latestData.Flags & FLAG_EYE_E) != 0)
            {
                #region Eye
                UnifiedTracking.Data.Eye.Left.Gaze = new Vector2(_latestData.EyeYaw_L, _latestData.EyePitch);
                UnifiedTracking.Data.Eye.Right.Gaze = new Vector2(_latestData.EyeYaw_R, _latestData.EyePitch);
                UnifiedTracking.Data.Eye.Left.Openness = 1.0f - _latestData.EyeLidCloseLeft;
                UnifiedTracking.Data.Eye.Right.Openness = 1.0f - _latestData.EyeLidCloseRight;
                UnifiedTracking.Data.Eye._minDilation = 0;
                UnifiedTracking.Data.Eye._maxDilation = 10;
                UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = 5.0f + _latestData.Eye_Pupil_Left * 5.0f;
                UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = 5.0f + _latestData.Eye_Pupil_Right * 5.0f;
                // Force the normalization values of Dilation to fit avg. pupil values.
                unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = _latestData.EyeSquintLeft;
                unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = _latestData.EyeSquintRight;
                #endregion
            }
        }
        // Read the data from the cympleFace UDP stream and place it into a cympleFaceTrackingDataStruct
        private bool ReadData(UdpClient cympleFaceConnection, IPEndPoint cympleFaceRemoteEndpoint, ref CympleFaceDataStructs trackingData)
        {
            try
            {
                // Grab the packet
                // will block but with a timeout set in the init function
                Byte[] receiveBytes = cympleFaceConnection.Receive(ref cympleFaceRemoteEndpoint);

                if (receiveBytes.Length < 12) // At least prefix, flags, type, length
                {
                    return false;
                }
                
                // got a good message
                if (disconnectWarned)
                {
                    Logger.LogInformation("cympleFace connection reestablished");
                    disconnectWarned = false;
                }

                // Read header fields with new format
                int prefix = BitConverter.ToInt32(receiveBytes, 0);
                int flags = BitConverter.ToInt32(receiveBytes, 4);
                ushort type = BitConverter.ToUInt16(receiveBytes, 8);
                short length = BitConverter.ToInt16(receiveBytes, 10);
                
                // Verify message prefix and type
                if (prefix != Constants.MSG_PREFIX || type != Constants.OSC_MSG_BLENDSHAPEDATA)
                {
                    Logger.LogWarning($"Invalid message: prefix={prefix:X}, type={type}");
                    return false;
                }
                
                // Set flags
                trackingData.Flags = flags;
                
                // Read all blendshape values
                int offset = 12; // Start after header (prefix, flags, type, length)
                for (int i = 0; i < Constants.blendShapeNames.Length; i++)
                {
                    if (offset + 4 <= receiveBytes.Length)
                    {
                        float value = BitConverter.ToSingle(receiveBytes, offset);
                        SetBlendshapeValue(ref trackingData, i, value);
                        offset += 4;
                    }
                    else
                    {
                        Logger.LogWarning($"Message too short: expected more data at offset {offset}");
                        return false;
                    }
                }
                
                return true;
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    if (!disconnectWarned)
                    {
                        Logger.LogWarning("cympleFace connection lost");
                        disconnectWarned = true;
                    }
                }
                else
                {
                    // some other network socket exception
                    Logger.LogError(se.ToString());
                }
                return false;
            }
            catch (Exception e)
            {
                // some other exception
                Logger.LogError(e.ToString());
                return false;
            }
        }

        // Helper method to set blendshape values by index
        private void SetBlendshapeValue(ref CympleFaceDataStructs data, int index, float value)
        {
            string name = Constants.blendShapeNames[index];
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
            }
        }

        // Called when the module is unloaded or VRCFaceTracking itself tears down.
        public override void Teardown()
        {
            // 先设置退出标志
            Logger.LogInformation("Tearing down CympleFaceTracking module...");
            _isExiting = true;
            
            try
            {
                // 关闭UDP客户端以中断任何阻塞的接收操作
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
            
            Logger.LogInformation("CympleFaceTracking module teardown complete");
        }
    }
}
