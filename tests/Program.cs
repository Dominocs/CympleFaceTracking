using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using CympleFaceTracking;
using Microsoft.Extensions.Logging.Abstractions;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

internal static class Program
{
    // Wire order independently taken from Cymple/include/cympleface/cympleFaceMsg.h.
    // Do not derive the fixture from Constants.blendShapeNames: that would hide order bugs.
    private static readonly string[] WireFields = """
        EyePitch_L EyePitch_R EyeYaw_L EyeYaw_R Eye_Pupil_Left Eye_Pupil_Right
        EyeLidCloseLeft EyeLidCloseRight EyeSquintLeft EyeSquintRight EyeWideLeft EyeWideRight
        BrowLowererLeft BrowLowererRight BrowInnerUpLeft BrowInnerUpRight BrowOuterUpLeft BrowOuterUpRight BrowPinchLeft BrowPinchRight
        NoseSneerLeft NoseSneerRight CheekSquintLeft CheekSquintRight CheekPuffLeft CheekPuffRight CheekSuckLeft CheekSuckRight
        JawLeft JawRight JawFwd JawOpen MouthClosed MouthUpperShiftLeft MouthUpperShiftRight MouthLowerShiftLeft MouthLowerShiftRight
        LipSuckUL LipSuckUR LipSuckDL LipSuckDR MouthFunnelUL MouthFunnelUR MouthFunnelDL MouthFunnelDR MouthPuckerUpper MouthPuckerLower
        LipRaiseUL LipRaiseUR LipDepressDL LipDepressDR MouthSmileLeft MouthSmileRight MouthDimpleLeft MouthDimpleRight
        MouthFrownLeft MouthFrownRight MouthStretchLeft MouthStretchRight MouthShrugUpper MouthShrugLower
        TongueOut TongueLeft TongueRight TongueUp TongueDown TongueCurlUp TongueBendDown TongueFlat TongueRoll
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static readonly Type ModuleType = typeof(CympleFaceTrackingModule);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _passed;
    private static readonly string FixtureRoot = Path.Combine(Path.GetTempPath(), $"CympleFaceTracking-test-{Guid.NewGuid():N}");
    private static string FixtureIni => Path.Combine(FixtureRoot, "Cymple", "iniFile.ini");

    private static int Main(string[] args)
    {
        string? originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixtureIni)!);
            WriteConfiguration();
            Environment.SetEnvironmentVariable("LOCALAPPDATA", FixtureRoot);
            if (args.Length > 0) Run("Cymple C++ wire contract still matches the independent fixture", () => CheckCppProtocol(args[0]));
            Run("292-byte mixed-endian packet decodes all 70 fields", ParseEveryField);
            Run("malformed datagrams cannot publish data", RejectMalformedPackets);
            Run("zero and unknown flags cannot update either domain", InactiveFlags);
            Run("first eye and mouth frame maps immediately", FirstFrameMapsAllFields);
            Run("interleaved eye and mouth frames preserve the other domain", InterleavedFrames);
            Run("host-disabled domains never overwrite another module", DisabledDomains);
            Run("bad packets do not replace the last valid frame", BadPacketPreservesData);
            Run("INI boolean parsing uses isolated temporary fixtures", IniBooleans);
            Run("host availability overrides every INI eye and mouth switch combination", InitializeAvailability);
            Run("INI switch changes cannot override running host-controlled tracking", IniSwitchChangesIgnored);
            Run("per-domain timeout resets only the stale domain", IndependentTimeout);
            Run("UDP receive, idle timeout, teardown and reinitialize", UdpLifecycle);
            Run("disconnect reset preference preserves the last expression", HoldOnDisconnect);
            Console.WriteLine($"PASS: {_passed} regression scenarios.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL after {_passed} scenarios: {exception}");
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
            File.Delete(FixtureIni);
            Directory.Delete(Path.GetDirectoryName(FixtureIni)!);
            Directory.Delete(FixtureRoot);
        }
    }

    private static void Run(string name, Action body)
    {
        UnifiedTracking.Data = new UnifiedTrackingData();
        body();
        ++_passed;
        Console.WriteLine($"PASS {name}");
    }

    private static void CheckCppProtocol(string source)
    {
        string header = File.ReadAllText(Path.Combine(source, "include", "cympleface", "cympleFaceMsg.h"));
        var match = Regex.Match(header, @"typedef\s+struct\s*\{(?:(?!typedef\s+struct).)*?\}\s*MSG_VRCFT_BLENDSHAPES_S\s*;", RegexOptions.Singleline);
        Check(match.Success, "Cymple C++ blendshape struct not found");
        string[] fields = Regex.Matches(match.Value, @"\bfloat\s+(\w+)\s*;").Select(item => item.Groups[1].Value).ToArray();
        Check(WireFields.SequenceEqual(fields), "Cymple's C++ float order changed; update the parser and independent fixture together");
        Check(Regex.IsMatch(header, @"#define\s+VRC_FLAG_MOUTH\s+0x01\b"), "Mouth flag changed");
        Check(Regex.IsMatch(header, @"#define\s+VRC_FLAG_EYE\s+0x02\b"), "Eye flag changed");
        string sender = File.ReadAllText(Path.Combine(source, "cympleface", "osc", "face_osc.cpp"));
        Check(sender.Contains("stBlendshapeCopy.Flags = BigEndian(stBlendshapeCopy.Flags)"), "Cymple sender flag endianness changed");
        Check(sender.Contains("stBlendshapeCopy.length = sizeof(MSG_VRCFT_BLENDSHAPES_S) - 12"), "Cymple sender payload length changed");
    }

    private static void ParseEveryField()
    {
        float[] values = Values();
        Check(TryParse(Packet(3, values), out object? data), "Valid packet rejected");
        Check((uint)data!.GetType().GetField("Flags")!.GetValue(data)! == 3, "Flags must decode as big-endian");
        for (int index = 0; index < WireFields.Length; index++)
            Near(values[index], (float)data.GetType().GetField(WireFields[index])!.GetValue(data)!, WireFields[index]);
    }

    private static void RejectMalformedPackets()
    {
        byte[] valid = Packet(3, Values());
        var malformed = new List<byte[]> { Array.Empty<byte>(), valid[..11], valid[..12], valid[..^1], valid.Concat(new byte[1]).ToArray() };
        malformed.Add(Changed(valid, bytes => bytes[0] ^= 1));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes, 0xFFFFFFFD)));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1)));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 0)));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), -1)));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 276)));
        malformed.Add(Changed(valid, bytes => BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(10), 280)));
        for (int index = 0; index < WireFields.Length; index++)
        {
            foreach (float nonFinite in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                malformed.Add(Changed(valid, bytes => WriteFloat(bytes, index, nonFinite)));
        }
        foreach (byte[] packet in malformed) Check(!TryParse(packet, out _), $"Malformed packet accepted (length {packet.Length})");
    }

    private static void InactiveFlags()
    {
        var module = NewModule();
        float[] values = Values();
        Process(module, Packet(3, values));
        foreach (uint flags in new uint[] { 0, 0x80000000, 0x03000000 })
        {
            Process(module, Packet(flags, Values(0.2f)));
            Map(module);
            CheckEye(values);
            Near(values[31], Shape("JawOpen"), "Flags with no supported domains must not publish data");
        }
    }

    private static void FirstFrameMapsAllFields()
    {
        var module = NewModule();
        float[] values = Values();
        Process(module, Packet(3, values));
        Map(module);
        CheckEye(values);
        for (int index = 8; index < WireFields.Length; index++)
        {
            foreach (string shape in ShapesFor(WireFields[index]))
                Near(values[index], Shape(shape), $"{WireFields[index]} -> {shape}");
        }
    }

    private static void InterleavedFrames()
    {
        var module = NewModule();
        float[] eye = Values();
        float[] mouth = Values(0.1f);
        Process(module, Packet(2, OnlyDomain(eye, eye: true)));
        Process(module, Packet(1, OnlyDomain(mouth, eye: false)));
        Map(module);
        CheckEye(eye);
        Near(mouth[31], Shape("JawOpen"), "Latest mouth frame");
        float[] newEye = Values(0.2f);
        Process(module, Packet(2, OnlyDomain(newEye, eye: true)));
        Map(module);
        CheckEye(newEye);
        Near(mouth[31], Shape("JawOpen"), "Eye packet must retain mouth data");
    }

    private static void DisabledDomains()
    {
        foreach (var enabled in new[] { (true, false), (false, true), (false, false) })
        {
            UnifiedTracking.Data = new UnifiedTrackingData();
            var module = NewModule(enabled.Item1, enabled.Item2);
            UnifiedTracking.Data.Eye.Left.Gaze.x = 0.93f;
            SetShape("JawOpen", 0.91f);
            Process(module, Packet(3, Values()));
            Map(module);
            Near(enabled.Item1 ? Values()[2] : 0.93f, UnifiedTracking.Data.Eye.Left.Gaze.x, "Eye availability");
            Near(enabled.Item2 ? Values()[31] : 0.91f, Shape("JawOpen"), "Expression availability");
        }
    }

    private static void BadPacketPreservesData()
    {
        var module = NewModule();
        float[] values = Values();
        Process(module, Packet(3, values));
        byte[] bad = Packet(2, Values(0.2f));
        BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(10), 276);
        Process(module, bad);
        Process(module, Packet(1, Values(0.2f))[..40]);
        Map(module);
        CheckEye(values);
        Near(values[31], Shape("JawOpen"), "Invalid packet must retain mouth data");
    }

    private static void IniBooleans()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CympleFaceTracking-test-{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Function Switch]\ntrueWord=true\ntrueNumber=1\nfalseWord=false\nfalseNumber=0\n");
            var module = NewModule();
            Check(module.GetBoolValue(path, "Function Switch", "trueWord"), "true INI parsing");
            Check(module.GetBoolValue(path, "Function Switch", "trueNumber"), "1 INI parsing");
            Check(!module.GetBoolValue(path, "Function Switch", "falseWord", true), "false INI parsing");
            Check(!module.GetBoolValue(path, "Function Switch", "falseNumber", true), "0 INI parsing");
            Check(module.GetBoolValue(path, "Function Switch", "missing", true), "Missing INI value with true default");
            Check(!module.GetBoolValue(path, "Function Switch", "missing"), "Missing INI value with false default");
        }
        finally { File.Delete(path); }
    }

    private static void InitializeAvailability()
    {
        MethodInfo resolver = ModuleType.GetMethod("ResolveIniPath", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check(string.Equals(Path.GetFullPath(FixtureIni), Path.GetFullPath((string)resolver.Invoke(null, null)!), StringComparison.OrdinalIgnoreCase), "Current LOCALAPPDATA INI must take precedence for the disconnect preference");
        int originalPort = Constants.Port;
        var combinations = new[] { (true, true), (true, false), (false, true), (false, false) };
        try
        {
            foreach (var configured in combinations)
            foreach (var available in combinations)
            {
                WriteConfiguration(eye: configured.Item1, mouth: configured.Item2, reset: false);
                using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                    Constants.Port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
                var module = NewModule();
                try
                {
                    Check(module.Initialize(available.Item1, available.Item2) == available,
                        $"INI {configured} overrode host availability {available}");
                    Check(!Get<bool>(module, "_resetExpressionOnDisconnect"), "Disconnect preference must still be respected");
                    UnifiedTracking.Data = new UnifiedTrackingData();
                    UnifiedTracking.Data.Eye.Left.Gaze.x = 0.93f;
                    SetShape("JawOpen", 0.91f);
                    Process(module, Packet(3, Values()));
                    Map(module);
                    Near(available.Item1 ? Values()[2] : 0.93f, UnifiedTracking.Data.Eye.Left.Gaze.x, "Host eye selection");
                    Near(available.Item2 ? Values()[31] : 0.91f, Shape("JawOpen"), "Host mouth selection");
                }
                finally { module.Teardown(); }
            }
        }
        finally
        {
            Constants.Port = originalPort;
            WriteConfiguration();
        }
    }

    private static void IniSwitchChangesIgnored()
    {
        var module = NewModule();
        try
        {
            Process(module, Packet(3, Values()));
            Map(module);
            CheckEye(Values());
            foreach (var configured in new[] { (false, true), (true, false), (false, false), (true, true) })
            {
                WriteConfiguration(eye: configured.Item1, mouth: configured.Item2);
                Set(module, "_nextSettingsReadMs", 0L);
                float[] values = Values(configured.Item1 ? 0.1f : 0.2f);
                Process(module, Packet(3, values));
                Map(module);
                CheckEye(values);
                Near(values[31], Shape("JawOpen"), "INI changes must not disable host-enabled mouth data");
            }
        }
        finally { WriteConfiguration(); }
    }

    private static void IndependentTimeout()
    {
        var module = NewModule();
        SetResetPreference(module, true);
        float[] values = Values();
        Process(module, Packet(3, values));
        Map(module);
        // Real elapsed time verifies the configured 1000 ms boundary, not a copied timestamp formula.
        Thread.Sleep(1100);
        Process(module, Packet(2, values)[..^1]);
        Process(module, Packet(1, OnlyDomain(values, eye: false)));
        Map(module);
        Near(0, UnifiedTracking.Data.Eye.Left.Gaze.x, "Stale gaze resets");
        Near(1, UnifiedTracking.Data.Eye.Left.Openness, "Stale eye returns to open");
        Near(0, Shape("BrowLowererLeft"), "Stale eye expression resets");
        Near(values[31], Shape("JawOpen"), "Active mouth survives stale eye reset");
        Process(module, Packet(2, OnlyDomain(values, eye: true)));
        Map(module);
        CheckEye(values);
        Thread.Sleep(1100);
        Process(module, Packet(2, OnlyDomain(values, eye: true)));
        Map(module);
        CheckEye(values);
        Near(0, Shape("JawOpen"), "Stale mouth resets independently");
        Near(0, Shape("TongueFlat"), "All stale mouth expressions reset");
    }

    private static void HoldOnDisconnect()
    {
        var module = NewModule();
        float[] values = Values();
        try
        {
            WriteConfiguration(reset: false);
            Set(module, "_nextSettingsReadMs", 0L);
            Process(module, Packet(3, values));
            Map(module);
            Thread.Sleep(1100);
            Map(module);
            CheckEye(values);
            Near(values[31], Shape("JawOpen"), "Reset disabled preserves mouth");

            WriteConfiguration(reset: true);
            Set(module, "_nextSettingsReadMs", 0L);
            Map(module);
            Near(0, UnifiedTracking.Data.Eye.Left.Gaze.x, "Enabling reset clears held eye data");
            Near(1, UnifiedTracking.Data.Eye.Left.Openness, "Enabling reset restores neutral openness");
            Near(0, Shape("JawOpen"), "Enabling reset clears held mouth data");

            Process(module, Packet(3, Values(0.1f)));
            Map(module);
            CheckEye(Values(0.1f));
            Near(Values(0.1f)[31], Shape("JawOpen"), "First reconnect frame maps immediately");
        }
        finally { WriteConfiguration(); }
    }

    private static void UdpLifecycle()
    {
        int originalPort = Constants.Port;
        var module = NewModule();
        try
        {
            using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                Constants.Port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
            // LOCALAPPDATA points to the isolated fixture for this test process.
            WriteConfiguration(eye: false, mouth: false);
            Check(module.Initialize(true, true) == (true, true), "Local switches must not block UDP initialization");
            SetResetPreference(module, true);
            using var sender = new UdpClient();
            byte[] packet = Packet(3, Values());
            sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, Constants.Port));
            Eventually(() => { module.Update(); return Math.Abs(Shape("JawOpen") - Values()[31]) < 0.00001f; }, "UDP first frame was not mapped");
            Eventually(() => { module.Update(); return Shape("JawOpen") == 0; }, "Silence did not reset expressions", 2500);

            var watch = Stopwatch.StartNew();
            module.Teardown();
            Check(watch.ElapsedMilliseconds < 1500, "Teardown did not stop the blocked receiver promptly");
            Check(Get<Thread?>(module, "_thread")?.IsAlive != true, "Receive thread remained alive after teardown");
            module.Teardown();

            UnifiedTracking.Data.Eye.Left.Gaze.x = 0.93f;
            Check(module.Initialize(false, true) == (false, true), "Reinitialize must apply new host domains");
            packet = Packet(3, Values(0.2f));
            sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, Constants.Port));
            Eventually(() => { module.Update(); return Math.Abs(Shape("JawOpen") - Values(0.2f)[31]) < 0.00001f; }, "Reinitialized receiver did not accept mouth packets");
            Near(0.93f, UnifiedTracking.Data.Eye.Left.Gaze.x, "Reinitialized host-disabled eye must remain untouched");
            module.Teardown();
            SetShape("JawOpen", 0.91f);
            Check(module.Initialize(true, false) == (true, false), "Reinitialize must enable eye and disable mouth");
            packet = Packet(3, Values(0.1f));
            sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, Constants.Port));
            Eventually(() => { module.Update(); return Math.Abs(UnifiedTracking.Data.Eye.Left.Gaze.x - Values(0.1f)[2]) < 0.00001f; }, "Reinitialized receiver did not accept eye packets");
            Near(0.91f, Shape("JawOpen"), "Reinitialized host-disabled mouth must remain untouched");
            Check(module.ModuleInformation.Name.Contains("1.5.2"), "Displayed module version was not incremented");
        }
        finally
        {
            module.Teardown();
            Constants.Port = originalPort;
            WriteConfiguration();
        }
    }

    private static CympleFaceTrackingModule NewModule(bool eye = true, bool mouth = true)
    {
        var module = new CympleFaceTrackingModule { Logger = NullLogger.Instance };
        Set(module, "trackingSupported", (eye, mouth));
        Set(module, "_nextSettingsReadMs", long.MaxValue);
        return module;
    }

    private static void WriteConfiguration(bool eye = true, bool mouth = true, bool reset = true)
    {
        File.WriteAllText(FixtureIni, $"[Function Switch]\ncymple_eye_sw={eye.ToString().ToLowerInvariant()}\ncymple_mouth_sw={mouth.ToString().ToLowerInvariant()}\n[Cymple_Mouth]\nreset_expression_on_tracking_disconnect={reset.ToString().ToLowerInvariant()}\n");
    }

    private static void SetResetPreference(CympleFaceTrackingModule module, bool value)
    {
        FieldInfo[] candidates = ModuleType.GetFields(PrivateInstance).Where(field => field.FieldType == typeof(bool) && field.Name.Contains("reset", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(candidates.Length == 1, "Expected one disconnect reset preference field");
        candidates[0].SetValue(module, value);
    }

    private static bool TryParse(byte[] packet, out object? data)
    {
        MethodInfo method = ModuleType.GetMethod("TryParsePacket", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new Exception("TryParsePacket was not found; build the updated module first");
        object?[] arguments = { packet, null };
        bool success = (bool)method.Invoke(null, arguments)!;
        data = arguments[1];
        return success;
    }

    private static void Process(CympleFaceTrackingModule module, byte[] packet) => ModuleType.GetMethod("ProcessPacket", PrivateInstance)!.Invoke(module, new object[] { packet });
    private static void Map(CympleFaceTrackingModule module) => ModuleType.GetMethod("UpdateExpressions", PrivateInstance)!.Invoke(module, null);
    private static void Set(CympleFaceTrackingModule module, string name, object value) => ModuleType.GetField(name, PrivateInstance)!.SetValue(module, value);
    private static T Get<T>(CympleFaceTrackingModule module, string name) => (T)ModuleType.GetField(name, PrivateInstance)!.GetValue(module)!;

    private static float[] Values(float increment = 0)
    {
        float[] values = Enumerable.Range(1, 70).Select(index => index / 100f + increment).ToArray();
        values[0] = -0.31f + increment;
        values[1] = 0.29f + increment;
        values[2] = -0.37f + increment;
        values[3] = 0.41f + increment;
        values[4] = 0.31f + increment;
        values[5] = 0.42f + increment;
        return values;
    }

    private static float[] OnlyDomain(float[] values, bool eye) => values.Select((value, index) => (index < 20) == eye ? value : 0).ToArray();

    private static byte[] Packet(uint flags, float[] values)
    {
        Check(values.Length == 70, "Fixture must contain 70 floats");
        var packet = new byte[292];
        BinaryPrimitives.WriteUInt32LittleEndian(packet, 0xFFFFFFFD);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), 0);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(10), 280);
        for (int index = 0; index < values.Length; index++) WriteFloat(packet, index, values[index]);
        return packet;
    }

    private static void WriteFloat(byte[] packet, int index, float value) => BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(12 + index * 4), BitConverter.SingleToInt32Bits(value));
    private static byte[] Changed(byte[] original, Action<byte[]> change) { byte[] copy = (byte[])original.Clone(); change(copy); return copy; }

    private static void CheckEye(float[] values)
    {
        Near(values[2], UnifiedTracking.Data.Eye.Left.Gaze.x, "Left yaw");
        Near(values[0], UnifiedTracking.Data.Eye.Left.Gaze.y, "Left pitch");
        Near(values[3], UnifiedTracking.Data.Eye.Right.Gaze.x, "Right yaw");
        Near(values[1], UnifiedTracking.Data.Eye.Right.Gaze.y, "Right pitch");
        Near(values[4], UnifiedTracking.Data.Eye.Left.PupilDiameter_MM, "Left pupil");
        Near(values[5], UnifiedTracking.Data.Eye.Right.PupilDiameter_MM, "Right pupil");
        Near(1 - values[6], UnifiedTracking.Data.Eye.Left.Openness, "Left openness");
        Near(1 - values[7], UnifiedTracking.Data.Eye.Right.Openness, "Right openness");
    }

    private static string[] ShapesFor(string field) => field switch
    {
        "JawFwd" => ["JawForward"],
        "MouthUpperShiftLeft" => ["MouthUpperLeft"], "MouthUpperShiftRight" => ["MouthUpperRight"],
        "MouthLowerShiftLeft" => ["MouthLowerLeft"], "MouthLowerShiftRight" => ["MouthLowerRight"],
        "LipSuckUL" => ["LipSuckUpperLeft"], "LipSuckUR" => ["LipSuckUpperRight"],
        "LipSuckDL" => ["LipSuckLowerLeft"], "LipSuckDR" => ["LipSuckLowerRight"],
        "MouthFunnelUL" => ["LipFunnelUpperLeft"], "MouthFunnelUR" => ["LipFunnelUpperRight"],
        "MouthFunnelDL" => ["LipFunnelLowerLeft"], "MouthFunnelDR" => ["LipFunnelLowerRight"],
        "MouthPuckerUpper" => ["LipPuckerUpperLeft", "LipPuckerUpperRight"],
        "MouthPuckerLower" => ["LipPuckerLowerLeft", "LipPuckerLowerRight"],
        "LipRaiseUL" => ["MouthUpperUpLeft", "MouthUpperDeepenLeft"],
        "LipRaiseUR" => ["MouthUpperUpRight", "MouthUpperDeepenRight"],
        "LipDepressDL" => ["MouthLowerDownLeft"], "LipDepressDR" => ["MouthLowerDownRight"],
        "MouthSmileLeft" => ["MouthCornerPullLeft", "MouthCornerSlantLeft"],
        "MouthSmileRight" => ["MouthCornerPullRight", "MouthCornerSlantRight"],
        "MouthShrugUpper" => ["MouthRaiserUpper"], "MouthShrugLower" => ["MouthRaiserLower"],
        _ => [field]
    };

    private static float Shape(string name) => UnifiedTracking.Data.Shapes[(int)Enum.Parse<UnifiedExpressions>(name)].Weight;
    private static void SetShape(string name, float value) => UnifiedTracking.Data.Shapes[(int)Enum.Parse<UnifiedExpressions>(name)].Weight = value;
    private static void Near(float expected, float actual, string description) => Check(float.IsFinite(actual) && Math.Abs(expected - actual) < 0.00001f, $"{description}: expected {expected}, got {actual}");
    private static void Check(bool condition, string description) { if (!condition) throw new Exception(description); }

    private static void Eventually(Func<bool> condition, string failure, int timeout = 1500)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeout)
        {
            if (condition()) return;
            Thread.Sleep(10);
        }
        throw new Exception(failure);
    }
}
