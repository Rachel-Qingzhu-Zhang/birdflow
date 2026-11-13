using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AvianVoice.Study
{
    /// <summary>
    /// Chooses the Windows playback device this application's sound goes to. Unity has no API for its own output
    /// device, so this sets the per-application output of Windows (the same setting as Settings > System > Sound >
    /// Volume mixer > the app > Output device) for the current process.
    ///
    /// The setting belongs to Windows, not to the project: it is stored for the executable (the Unity Editor, or the
    /// built player), stays after the application closes and applies to every project opened with that editor,
    /// until it is set back to the Windows default (<see cref="TrySetOverride"/> with an empty id).
    /// While a headset session runs, the headset runtime can route the application to its own device regardless
    /// of this setting (Meta Quest Link: "Audio output in VR"); compare with
    /// <see cref="PcAudioMonitor.UnityOutputName"/>, the device the application's audio session is actually on.
    ///
    /// Windows offers no documented interface for this. The one used here
    /// (Windows.Media.Internal.AudioPolicyConfig) exists since Windows 10 21H2 / Windows 11 and is the one the
    /// Settings app and tools such as EarTrumpet use; on other systems the calls return false with an error text.
    /// Reading was checked on Windows 11 build 26200 (2026-11-12).
    /// </summary>
    public static class AudioOutputRouting
    {
        /// <summary>Text of the last failure ("" after a success).</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>Active Windows playback devices as (endpoint id, name). Empty when they cannot be listed.</summary>
        public static KeyValuePair<string, string>[] Devices()
        {
            try
            {
                return PcAudioMonitor.OutputEndpoints();
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return Array.Empty<KeyValuePair<string, string>>();
            }
        }

        /// <summary>
        /// The playback device set for this application: its endpoint id, or "" when the application follows the
        /// Windows default. False when the setting cannot be read.
        /// </summary>
        public static bool TryGetOverride(out string deviceId)
        {
            deviceId = "";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var hr = Call((factory, pid) =>
                {
                    var get = Slot<GetEndpoint>(factory, GetSlot);
                    var result = get(factory, pid, FlowRender, RoleConsole, out var hid);
                    if (result != 0 || hid == IntPtr.Zero)
                        return result;
                    packed = Marshal.PtrToStringUni(WindowsGetStringRawBuffer(hid, out var length), (int)length);
                    WindowsDeleteString(hid);
                    return 0;
                });
                if (hr != 0)
                {
                    LastError = Describe(hr);
                    return false;
                }

                deviceId = Unpack(packed);
                LastError = "";
                return true;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return false;
            }
#else
            LastError = "Windows only";
            return false;
#endif
        }

        /// <summary>
        /// Send this application's sound to the playback device with this endpoint id ("" = follow the Windows
        /// default again). The application's audio moves when it reopens its output; Unity does that on its own
        /// when the device changes, otherwise restart its audio (AudioSettings.Reset) or the application.
        /// </summary>
        public static bool TrySetOverride(string deviceId)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var hr = Call((factory, pid) =>
                {
                    var set = Slot<SetEndpoint>(factory, SetSlot);
                    var hid = IntPtr.Zero;
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        var text = Pack(deviceId);
                        var created = WindowsCreateString(text, text.Length, out hid);
                        if (created != 0)
                            return created;
                    }

                    try
                    {
                        // Console and multimedia are the roles applications open their output with.
                        var result = set(factory, pid, FlowRender, RoleConsole, hid);
                        return result != 0 ? result : set(factory, pid, FlowRender, RoleMultimedia, hid);
                    }
                    finally
                    {
                        if (hid != IntPtr.Zero)
                            WindowsDeleteString(hid);
                    }
                });
                LastError = hr != 0 ? Describe(hr) : "";
                return hr == 0;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return false;
            }
#else
            LastError = "Windows only";
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const string PolicyClass = "Windows.Media.Internal.AudioPolicyConfig";
        // IAudioPolicyConfigFactory as of Windows 10 21H2 / Windows 11.
        static readonly Guid PolicyInterface = new("ab3d4648-e242-459f-b02f-541c70306324");
        // IUnknown (3) + IInspectable (3) + 19 volume / chat members, then Set, Get, ClearAll.
        const int SetSlot = 25;
        const int GetSlot = 26;
        const int FlowRender = 0;
        const int RoleConsole = 0;
        const int RoleMultimedia = 1;
        const string IdPrefix = @"\\?\SWD#MMDEVAPI#";
        const string RenderInterfaceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

        static string packed;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetEndpoint(IntPtr self, uint processId, int flow, int role, IntPtr deviceId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetEndpoint(IntPtr self, uint processId, int flow, int role, out IntPtr deviceId);

        [DllImport("combase.dll")] static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
        [DllImport("combase.dll", CharSet = CharSet.Unicode)] static extern int WindowsCreateString(string source, int length, out IntPtr hstring);
        [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr hstring);
        [DllImport("combase.dll")] static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

        /// <summary>Run one call on the policy interface for this process; returns its HRESULT.</summary>
        static int Call(Func<IntPtr, uint, int> body)
        {
            packed = "";
            var hr = WindowsCreateString(PolicyClass, PolicyClass.Length, out var classId);
            if (hr != 0)
                return hr;

            try
            {
                var iid = PolicyInterface;
                hr = RoGetActivationFactory(classId, ref iid, out var factory);
                if (hr != 0)
                    return hr;

                try
                {
                    return body(factory, (uint)System.Diagnostics.Process.GetCurrentProcess().Id);
                }
                finally
                {
                    Marshal.Release(factory);
                }
            }
            finally
            {
                WindowsDeleteString(classId);
            }
        }

        static T Slot<T>(IntPtr comObject, int index) where T : Delegate
        {
            var vtable = Marshal.ReadIntPtr(comObject);
            return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, index * IntPtr.Size));
        }

        static string Pack(string endpointId) => IdPrefix + endpointId + RenderInterfaceSuffix;

        static string Unpack(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase))
                text = text.Substring(IdPrefix.Length);
            var hash = text.LastIndexOf("#{", StringComparison.Ordinal);
            return hash > 0 ? text.Substring(0, hash) : text;
        }

        static string Describe(int hr)
        {
            return (uint)hr switch
            {
                0x80004002 => "this Windows version has no per-application output setting (interface not found)",
                0x80070057 => "Windows does not offer a per-application output for this process (0x80070057)",
                _ => "Windows error 0x" + hr.ToString("X8")
            };
        }
#endif
    }
}
