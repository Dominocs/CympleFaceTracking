using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CympleFaceTracking
{
    class CympleFaceDataStructs
    {
        public float noseSneerLeft;
        public float noseSneerRight;
        public float cheekSuckPuffLeft;
        public float cheekSuckPuffRight;
        public float mouthX;
        public float mouthY;
        public float lipSuckFunnelUpperLeft;
        public float lipSuckFunnelUpperRight;
        public float lipSuckFunnelLowerLeft;
        public float lipSuckFunnelLowerRight;
        public float lipRaiseUpperLeft;
        public float lipRaiseUpperRight;
        public float lipPressLowerLeft;
        public float lipPressLowerRight;
        public float lipUpperShift;
        public float lipLowerShift;
        public float mouthCornerPullLeft;
        public float mouthCornerPullRight;
        public float mouthTighttenerLeft;
        public float mouthTighttenerRight;
        public float tongueInout;
        public float tongueX;
        public float tongueY;
    }
    public static class Constants
    {
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "noseSneerLeft",
            "noseSneerRight",
            "cheekSuckPuffLeft",
            "cheekSuckPuffRight",
            "mouthX",
            "mouthY",
            "lipSuckFunnelUpperLeft",
            "lipSuckFunnelUpperRight",
            "lipSuckFunnelLowerLeft",
            "lipSuckFunnelLowerRight",
            "lipRaiseUpperLeft",
            "lipRaiseUpperRight",
            "lipPressLowerLeft",
            "lipPressLowerRight",
            "lipUpperShift",
            "lipLowerShift",
            "mouthCornerPullLeft",
            "mouthCornerPullRight",
            "mouthTighttenerLeft",
            "mouthTighttenerRight",
            "tongueInout",
            "tongueX",
            "tongueY"
        };
        public static int Port = 11111;
    }
}

