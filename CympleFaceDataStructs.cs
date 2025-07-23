using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CympleFaceTracking
{
    class CympleFaceDataStructs
    {
        public uint Flags;
        public float EyePitch;
        public float EyeYaw_L;
        public float EyeYaw_R;
        public float Eye_Pupil_Left;
        public float Eye_Pupil_Right;
        public float EyeLidCloseLeft;
        public float EyeLidCloseRight;
        public float EyeSquintLeft;
        public float EyeSquintRight;
        public float CheekPuffLeft;
        public float CheekPuffRight;
        public float CheekSuck;
        public float Jaw_Left_Right;
        public float JawOpen;
        public float JawFwd;
        public float MouthClose;
        public float Mouth_Left_Right;
        public float LipSuckUpper;
        public float LipSuckLower;
        public float MouthFunnel;
        public float MouthPucker;
        public float LipRaise_L;
        public float LipRaise_R;
        public float LipDepress_L;
        public float LipDepress_R;
        public float LipShift_Up;
        public float LipShift_Down;
        public float MouthRoll_Up;
        public float MouthRoll_Down;
        public float MouthShrugLower;
        public float MouthSmileLeft;
        public float MouthSmileRight;
        public float MouthSadLeft;
        public float MouthSadRight;
        public float TongueOut;
        public float Tongue_Left_Right;
        public float Tongue_Up_Down;
        public float TongueWide;
        public float TongueRoll;
        public float EyeWideLeft;
        public float EyeWideRight;
        public float BrowLeftUpDown;
        public float BrowRightUpDown;
    }
    public static class Constants
    {
        // Message types
        public const int OSC_MSG_BLENDSHAPEDATA = 0;
        public const int OSC_MSG_MAX = 1;
        
        // Message prefix
        public const int MSG_PREFIX = unchecked((int)0xFFFFFFFD);
        
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "EyePitch",
            "EyeYaw_L",
            "EyeYaw_R",
            "Eye_Pupil_Left",
            "Eye_Pupil_Right",
            "EyeLidCloseLeft",
            "EyeLidCloseRight",
            "EyeSquintLeft",
            "EyeSquintRight",
            "CheekPuffLeft",
            "CheekPuffRight",
            "CheekSuck",
            "Jaw_Left_Right",
            "JawOpen",
            "JawFwd",
            "MouthClose",
            "Mouth_Left_Right",
            "LipSuckUpper",
            "LipSuckLower",
            "MouthFunnel",
            "MouthPucker",
            "LipRaise_L",
            "LipRaise_R",
            "LipDepress_L",
            "LipDepress_R",
            "LipShift_Up",
            "LipShift_Down",
            "MouthRoll_Up",
            "MouthRoll_Down",
            "MouthShrugLower",
            "MouthSmileLeft",
            "MouthSmileRight",
            "MouthSadLeft",
            "MouthSadRight",
            "TongueOut",
            "Tongue_Left_Right",
            "Tongue_Up_Down",
            "TongueWide",
            "TongueRoll",
            "EyeWideLeft",
            "EyeWideRight",
            "BrowLeftUpDown",
            "BrowRightUpDown",
        };
        
        public static int Port = 22999;
    }
}

