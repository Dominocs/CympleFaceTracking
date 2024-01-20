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
    public class ExampleExtTrackingModule : ExtTrackingModule
    {
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
            Thread.Sleep(10);
            // Get latest tracking data from interface and transform to VRCFaceTracking data.
            if (ReadData(_CympleFaceConnection, _CympleFaceRemoteEndpoint, ref _latestData))
            {
                UpdateLowerFaceExpression(ref UnifiedTracking.Data.Shapes, ref _latestData);
            }
        }
        private void UpdateLowerFaceExpression(ref UnifiedExpressionShape[] unifiedExpressions, ref CympleFaceDataStructs _latestData) {
            #region Nose
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = _latestData.noseSneerLeft;
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = _latestData.noseSneerRight;
            #endregion
            #region Cheek
            if(_latestData.cheekSuckPuffLeft > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = _latestData.cheekSuckPuffLeft;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = -_latestData.cheekSuckPuffLeft;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = 0;
            }
            if (_latestData.cheekSuckPuffRight > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = _latestData.cheekSuckPuffRight;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = -_latestData.cheekSuckPuffRight;
                unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = 0;
            }
            #endregion
            #region Lip
            if (_latestData.lipSuckFunnelUpperLeft > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = _latestData.lipSuckFunnelUpperLeft;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = -_latestData.lipSuckFunnelUpperLeft;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = 0;
            }
            if (_latestData.lipSuckFunnelUpperRight > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = _latestData.lipSuckFunnelUpperRight;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = -_latestData.lipSuckFunnelUpperRight;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = 0;
            }
            if (_latestData.lipSuckFunnelLowerLeft > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = _latestData.lipSuckFunnelLowerLeft;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = -_latestData.lipSuckFunnelLowerLeft;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = 0;
            }
            if (_latestData.lipSuckFunnelLowerRight > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _latestData.lipSuckFunnelLowerRight;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = -_latestData.lipSuckFunnelLowerRight;
                unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = 0;
            }


            unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = _latestData.lipRaiseUpperLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = _latestData.lipRaiseUpperRight;
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = _latestData.lipPressLowerLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.lipPressLowerRight;
            if (_latestData.lipUpperShift > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = _latestData.lipUpperShift;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = 0;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = -_latestData.lipUpperShift;
            }
            if (_latestData.lipLowerShift > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestData.lipLowerShift;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = -_latestData.lipLowerShift;
                unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = 0;
            }
            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = _latestData.mouthTighttenerLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = _latestData.mouthTighttenerRight;
            #endregion
            #region Mouth
            unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = _latestData.mouthY;
            if (_latestData.mouthX > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = _latestData.mouthX;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = -_latestData.mouthX;
                unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = 0;
            }
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _latestData.mouthCornerPullLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _latestData.mouthCornerPullRight;
            #endregion
            #region Tongue
            unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = _latestData.tongueInout;
            if (_latestData.tongueX > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = _latestData.mouthX;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.TongueLeft].Weight = -_latestData.mouthX;
                unifiedExpressions[(int)UnifiedExpressions.TongueRight].Weight = 0;
            }
            if (_latestData.tongueY > 0)
            {
                unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = 0;
                unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = _latestData.mouthX;
            }
            else
            {
                unifiedExpressions[(int)UnifiedExpressions.TongueDown].Weight = -_latestData.mouthX;
                unifiedExpressions[(int)UnifiedExpressions.TongueUp].Weight = 0;
            }
            #endregion
        }
        // Read the data from the cympleFace UDP stream and place it into a cympleFaceTrackingDataStruct
        private bool ReadData(UdpClient cympleFaceConnection, IPEndPoint cympleFaceRemoteEndpoint, ref CympleFaceDataStructs trackingData)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();
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
            if (values.Count() < 23)
            {
                Logger.LogInformation("Too short");
                return false;
            }
            trackingData.noseSneerLeft = values["noseSneerLeft"];
            trackingData.noseSneerRight = values["noseSneerRight"];
            trackingData.cheekSuckPuffLeft = values["cheekSuckPuffLeft"];
            trackingData.cheekSuckPuffRight = values["cheekSuckPuffRight"];
            trackingData.mouthX = values["mouthX"];
            trackingData.mouthY = values["mouthY"];
            trackingData.lipSuckFunnelUpperLeft = values["lipSuckFunnelUpperLeft"];
            trackingData.lipSuckFunnelUpperRight = values["lipSuckFunnelUpperRight"];
            trackingData.lipSuckFunnelLowerLeft = values["lipSuckFunnelLowerLeft"];
            trackingData.lipSuckFunnelLowerRight = values["lipSuckFunnelLowerRight"];
            trackingData.lipRaiseUpperLeft = values["lipRaiseUpperLeft"];
            trackingData.lipRaiseUpperRight = values["lipRaiseUpperRight"];
            trackingData.lipPressLowerLeft = values["lipPressLowerLeft"];
            trackingData.lipPressLowerRight = values["lipPressLowerRight"];
            trackingData.lipUpperShift = values["lipUpperShift"];
            trackingData.lipLowerShift = values["lipLowerShift"];
            trackingData.mouthCornerPullLeft = values["mouthCornerPullLeft"];
            trackingData.mouthCornerPullRight = values["mouthCornerPullRight"];
            trackingData.mouthTighttenerLeft = values["mouthTighttenerLeft"];
            trackingData.mouthTighttenerRight = values["mouthTighttenerRight"];
            trackingData.tongueInout = values["tongueInout"];
            trackingData.tongueX = values["tongueX"];
            trackingData.tongueY = values["tongueY"];
            return true;
        }

        // Called when the module is unloaded or VRCFaceTracking itself tears down.
        public override void Teardown()
        {
            // shut down the upd client
            Logger.LogInformation("Closing LiveLink UDP client...");
            _CympleFaceConnection.Close();
            _CympleFaceConnection.Dispose();
        }
    }
}