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
        public float jawOpen;
        public float jaw_Left_Right;
        public float jaw_Forward;
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
        public float cheekPuffLeft;
        public float cheekPuffRight;
    }
    public static class Constants
    {
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "noseSneerLeft",
            "noseSneerRight",
            "jawOpen",
            "jaw_Left_Right",
            "jaw_Forward",
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
            "cheekPuffLeft",
            "cheekPuffRight",

    };
        public static int Port = 23300;
    }
}

