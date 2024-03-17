using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CympleFaceTracking
{
    class CympleFaceDataStructs
    {
        public int Flags;
        public float CheekPuffLeft;
        public float CheekPuffRight;
        public float JawOpen;
        public float Jaw_Left_Right;
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
        public float TongueOut;
        public float TongueX;
        public float TongueY;
        public float EyePitch;
        public float EyeYaw_L;
        public float EyeYaw_R;
        public float Eye_Pupil_Left;
        public float Eye_Pupil_Right;
        public float EyeLidCloseLeft;
        public float EyeLidCloseRight;
        public float EyeSquintLeft;
        public float EyeSquintRight;
    }
    public static class Constants
    {
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "CheekPuffLeft",
            "CheekPuffRight",
            "JawOpen",
            "Jaw_Left_Right",
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
            "TongueOut",
            "TongueX",
            "TongueY",
            "EyePitch",
            "EyeYaw_L",
            "EyeYaw_R",
            "Eye_Pupil_Left",
            "Eye_Pupil_Right",
            "EyeLidCloseLeft",
            "EyeLidCloseRight",
            "EyeSquintLeft",
            "EyeSquintRight",
    };
        public static int Port = 22999;
    }
}

