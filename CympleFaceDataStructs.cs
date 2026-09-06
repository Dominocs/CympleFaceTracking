namespace CympleFaceTracking
{
    class CympleFaceDataStructs
    {
        // eye
        public uint Flags;
        public float EyePitch_L;
        public float EyePitch_R;
        public float EyeYaw_L;
        public float EyeYaw_R;
        public float Eye_Pupil_Left;
        public float Eye_Pupil_Right;
        public float EyeLidCloseLeft;
        public float EyeLidCloseRight;
        public float EyeSquintLeft;
        public float EyeSquintRight;
        public float EyeWideLeft;
        public float EyeWideRight;
        public float BrowLowererLeft;
        public float BrowLowererRight;
        public float BrowInnerUpLeft;
        public float BrowInnerUpRight;
        public float BrowOuterUpLeft;
        public float BrowOuterUpRight;
        public float BrowPinchLeft;
        public float BrowPinchRight;
        // mouth
        public float NoseSneerLeft;
        public float NoseSneerRight;
        public float CheekSquintLeft;
        public float CheekSquintRight;
        public float CheekPuffLeft;
        public float CheekPuffRight;
        public float CheekSuckLeft;
        public float CheekSuckRight;
        public float JawLeft;
        public float JawRight;
        public float JawFwd;
        public float JawOpen;
        public float MouthClosed;
        public float MouthUpperShiftLeft;
        public float MouthUpperShiftRight;
        public float MouthLowerShiftLeft;
        public float MouthLowerShiftRight;
        public float LipSuckUL;
        public float LipSuckUR;
        public float LipSuckDL;
        public float LipSuckDR;
        public float MouthFunnelUL;
        public float MouthFunnelUR;
        public float MouthFunnelDL;
        public float MouthFunnelDR;
        public float MouthPuckerUpper;
        public float MouthPuckerLower;
        public float LipRaiseUL;
        public float LipRaiseUR;
        public float LipDepressDL;
        public float LipDepressDR;
        public float MouthSmileLeft;
        public float MouthSmileRight;
        public float MouthDimpleLeft;
        public float MouthDimpleRight;
        public float MouthFrownLeft;
        public float MouthFrownRight;
        public float MouthStretchLeft;
        public float MouthStretchRight;
        public float MouthShrugUpper;
        public float MouthShrugLower;
        public float TongueOut;
        public float TongueLeft;
        public float TongueRight;
        public float TongueUp;
        public float TongueDown;
        public float TongueCurlUp;
        public float TongueBendDown;
        public float TongueFlat;
        public float TongueRoll;
    }
    public static class Constants
    {
        public const int BlendShapeCount = 70;
        public const int HeaderSize = 12;
        public const int PayloadSize = BlendShapeCount * sizeof(float);
        public const int PacketSize = HeaderSize + PayloadSize;

        // Message types
        public const int OSC_MSG_BLENDSHAPEDATA = 0;
        public const int OSC_MSG_MAX = 1;
        
        // Message prefix
        public const int MSG_PREFIX = unchecked((int)0xFFFFFFFD);
        
        // The proper names of each cympleFace blendshape
        public static readonly string[] blendShapeNames = {
            "EyePitch_L",
            "EyePitch_R",
            "EyeYaw_L",
            "EyeYaw_R",
            "Eye_Pupil_Left",
            "Eye_Pupil_Right",
            "EyeLidCloseLeft",
            "EyeLidCloseRight",
            "EyeSquintLeft",
            "EyeSquintRight",
            "EyeWideLeft",
            "EyeWideRight",
            "BrowLowererLeft",
            "BrowLowererRight",
            "BrowInnerUpLeft",
            "BrowInnerUpRight",
            "BrowOuterUpLeft",
            "BrowOuterUpRight",
            "BrowPinchLeft",
            "BrowPinchRight",
            // mouth
            "NoseSneerLeft",
            "NoseSneerRight",
            "CheekSquintLeft",
            "CheekSquintRight",
            "CheekPuffLeft",
            "CheekPuffRight",
            "CheekSuckLeft",
            "CheekSuckRight",
            "JawLeft",
            "JawRight",
            "JawFwd",
            "JawOpen",
            "MouthClosed",
            "MouthUpperShiftLeft",
            "MouthUpperShiftRight",
            "MouthLowerShiftLeft",
            "MouthLowerShiftRight",
            "LipSuckUL",
            "LipSuckUR",
            "LipSuckDL",
            "LipSuckDR",
            "MouthFunnelUL",
            "MouthFunnelUR",
            "MouthFunnelDL",
            "MouthFunnelDR",
            "MouthPuckerUpper",
            "MouthPuckerLower",
            "LipRaiseUL",
            "LipRaiseUR",
            "LipDepressDL",
            "LipDepressDR",
            "MouthSmileLeft",
            "MouthSmileRight",
            "MouthDimpleLeft",
            "MouthDimpleRight",
            "MouthFrownLeft",
            "MouthFrownRight",
            "MouthStretchLeft",
            "MouthStretchRight",
            "MouthShrugUpper",
            "MouthShrugLower",
            "TongueOut",
            "TongueLeft",
            "TongueRight",
            "TongueUp",
            "TongueDown",
            "TongueCurlUp",
            "TongueBendDown",
            "TongueFlat",
            "TongueRoll",
        };
        
        public static int Port = 22999;
    }
}

