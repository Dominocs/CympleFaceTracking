using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CympleFaceTracking
{
    class CympleFaceDataStructs
    {
        public int flags;
        public float noseSneerLeft;
        public float noseSneerRight;
        public float cheekPuffLeft;
        public float cheekPuffRight;
        public float jawOpen;
        public float jaw_Left_Right;
        public float LipRaise_L;
        public float LipRaise_R;
        public float LipDepress_L;
        public float LipDepress_R;
        public float LipShift_Up;
        public float LipShift_Down;
        public float MouthRoll_Up;
        public float MouthRoll_Down;
        public float MouthFunnel_Up;
        public float MouthFunnel_Down;
        public float MouthPucker;
        public float Smile_L;
        public float Smile_R;
        public float Sad_L;
        public float Sad_R;
        public float tongueOut;
        public float tongueX;
        public float tongueY;
        public float eyePitch;
        public float eyeYaw_L;
        public float eyeYaw_R;
        public float eyeLidCloseLeft;
        public float eyeLidCloseRight;
        public float eyeSquintLeft;
        public float eyeSquintRight;
        public float browUpDownLeft;
        public float browUpDownRight;
    }
    public static class Constants
    {
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "noseSneerLeft",
            "noseSneerRight",
            "cheekPuffLeft",
            "cheekPuffRight",
            "jawOpen",
            "jaw_Left_Right",
            "LipRaise_L",
            "LipRaise_R",
            "LipDepress_L",
            "LipDepress_R",
            "LipShift_Up",
            "LipShift_Down",
            "MouthRoll_Up",
            "MouthRoll_Down",
            "MouthFunnel_Up",
            "MouthFunnel_Down",
            "MouthPucker",
            "Smile_L",
            "Smile_R",
            "Sad_L",
            "Sad_R",
            "tongueOut",
            "tongueX",
            "tongueY",
            "eyePitch",
            "eyeYaw_L",
            "eyeYaw_R",
            "eyeLidCloseLeft",
            "eyeLidCloseRight",
            "eyeSquintLeft",
            "eyeSquintRight",
            "browUpDownLeft",
            "browUpDownRight"
    };
        public static int Port = 22999;
    }
}

