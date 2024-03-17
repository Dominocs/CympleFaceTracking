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
            // Get latest tracking data from interface and transform to VRCFaceTracking data.
            if (ReadData(_CympleFaceConnection, _CympleFaceRemoteEndpoint, ref _latestData))
            {
                UpdateLowerFaceExpression(ref UnifiedTracking.Data.Shapes, ref _latestData);
            }
            Thread.Sleep(10);
        }
        private void UpdateLowerFaceExpression(ref UnifiedExpressionShape[] unifiedExpressions, ref CympleFaceDataStructs _latestData) {
            if ((_latestData.Flags & FLAG_MOUTH_E) != 0)
            {
                #region Cheek
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = _latestData.CheekPuffLeft;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = _latestData.CheekPuffRight;
                #endregion
                #region Lip
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = _latestData.MouthRoll_Up;
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _latestData.MouthRoll_Down;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = _latestData.LipRaise_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = _latestData.LipRaise_R;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = _latestData.LipDepress_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = _latestData.LipDepress_R;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = _latestData.MouthFunnel_Up;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _latestData.MouthFunnel_Down;
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = _latestData.MouthPucker;
                unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = _latestData.MouthPucker;
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
                #endregion
                #region Mouth
                unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = _latestData.JawOpen;
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
                unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _latestData.Smile_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _latestData.Smile_R;
                unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = _latestData.Sad_L;
                unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = _latestData.Sad_R;
                #endregion
                #region Tongue
                unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = _latestData.TongueOut;
                if (_latestData.TongueX > 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = 0;
                    unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = _latestData.TongueX;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = -_latestData.TongueX;
                    unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = 0;
                }
                if (_latestData.TongueY >= 0)
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = _latestData.TongueY;
                    unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = 0.0f;
                }
                else
                {
                    unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = -_latestData.TongueY;
                    unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = 0.0f;
                }
                #endregion
            }
            if ((_latestData.Flags & FLAG_EYE_E) != 0)
            {
                #region Eye
                UnifiedTracking.Data.Eye.Left.Gaze = new Vector2(_latestData.EyeYaw_L, _latestData.EyePitch);
                UnifiedTracking.Data.Eye.Right.Gaze = new Vector2(_latestData.EyeYaw_R,  _latestData.EyePitch);
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
            Dictionary<string, float> values = new Dictionary<string, float>();
            int flags = 0;
            try
            {
                // Grab the packet
                // will block but with a timeout set in the init function
                Byte[] receiveBytes = cympleFaceConnection.Receive(ref cympleFaceRemoteEndpoint);

                if (receiveBytes.Length < 4)
                {
                    return false;
                }
                // got a good message
                if (disconnectWarned)
                {
                    Logger.LogInformation("cympleFace connection reestablished");
                    disconnectWarned = false;
                }

                List<List<Byte>> chunkedBytes = receiveBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();
                // Process each float in out chunked out list
                var flagByte = chunkedBytes[0];
                flagByte.Reverse();
                flags = BitConverter.ToInt32(flagByte.ToArray());
                chunkedBytes.RemoveAt(0);
                foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
                {
                    // First, reverse the list because the data will be in big endian, then convert it to a float
                    item.value.Reverse();
                    values.Add(Constants.blendShapeNames[item.i], BitConverter.ToSingle(item.value.ToArray(), 0));
                }
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
            if (values.Count() < 31)
            {
                Logger.LogInformation("Too short: " + values.Count());
                return false;
            }
            trackingData.Flags = flags;
            trackingData.CheekPuffLeft = values["CheekPuffLeft"];
            trackingData.CheekPuffRight = values["CheekPuffRight"];
            trackingData.JawOpen = values["JawOpen"];
            trackingData.Jaw_Left_Right = values["Jaw_Left_Right"];
            trackingData.LipRaise_L = values["LipRaise_L"];
            trackingData.LipRaise_R = values["LipRaise_R"];
            trackingData.LipDepress_L = values["LipDepress_L"];
            trackingData.LipDepress_R = values["LipDepress_R"];
            trackingData.LipShift_Up = values["LipShift_Up"];
            trackingData.LipShift_Down = values["LipShift_Down"];
            trackingData.MouthRoll_Up = values["MouthRoll_Up"];
            trackingData.MouthRoll_Down = values["MouthRoll_Down"];
            trackingData.MouthFunnel_Up = values["MouthFunnel_Up"];
            trackingData.MouthFunnel_Down = values["MouthFunnel_Down"];
            trackingData.MouthPucker = values["MouthPucker"];
            trackingData.Smile_L = values["Smile_L"];
            trackingData.Smile_R = values["Smile_R"];
            trackingData.Sad_L = values["Sad_L"];
            trackingData.Sad_R = values["Sad_R"];
            trackingData.TongueOut = values["TongueOut"];
            trackingData.TongueX = values["TongueX"];
            trackingData.TongueY = values["TongueY"];
            trackingData.EyePitch = values["EyePitch"];
            trackingData.EyeYaw_L = values["EyeYaw_L"];
            trackingData.EyeYaw_R = values["EyeYaw_R"];
            trackingData.Eye_Pupil_Left = values["Eye_Pupil_Left"];
            trackingData.Eye_Pupil_Right = values["Eye_Pupil_Right"];
            trackingData.EyeLidCloseLeft = values["EyeLidCloseLeft"];
            trackingData.EyeLidCloseRight = values["EyeLidCloseRight"];
            trackingData.EyeSquintLeft = values["EyeSquintLeft"];
            trackingData.EyeSquintRight = values["EyeSquintRight"];
            return true;
        }

        // Called when the module is unloaded or VRCFaceTracking itself tears down.
        public override void Teardown()
        {
            // shut down the upd client
            Logger.LogInformation("Closing UDP client...");
            _CympleFaceConnection.Close();
            _CympleFaceConnection.Dispose();
        }
    }
}