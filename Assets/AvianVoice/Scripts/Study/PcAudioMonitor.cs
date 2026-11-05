using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// Mirrors the Unity listener output to a PC speaker device (Windows waveOut) so the experimenter hears
    /// the scene even when Unity's own output goes to the headset (Windows default output = Oculus Virtual
    /// Audio Device under Quest Link). Lives on the AudioListener object. Also reports the Windows default
    /// output device and whether it is muted, which is the usual reason for "no sound from the PC".
    ///
    /// Modes: Auto = mirror only when the Windows default output is a headset device (no double audio when
    /// Unity already plays through the PC speakers); Always; Off.
    /// </summary>
    [DefaultExecutionOrder(30990)]
    public class PcAudioMonitor : MonoBehaviour
    {
        public enum MonitorMode { Auto, Always, Off }

        public const string ModePrefKey = "AvianVoice.Study.PcMonitorMode";
        public const string DevicePrefKey = "AvianVoice.Study.PcMonitorDevice";

        [SerializeField]
        MonitorMode mode = MonitorMode.Auto;

        [Tooltip("Substring of the waveOut device name to mirror to. Empty = first device that is not a headset / virtual device.")]
        [SerializeField]
        string deviceNameContains = "";

        [Range(0f, 1f)]
        [SerializeField]
        float volume = 1f;

        [Tooltip("Device names containing any of these are treated as headset / virtual outputs (never auto-picked, trigger Auto mirroring when default).")]
        [SerializeField]
        string[] headsetKeywords = { "Oculus", "Virtual", "Quest", "Rift", "Steam", "VR" };

        const int BufferFrames = 1024;   // 21 ms at 48 kHz
        const int BufferCount = 16;      // ~340 ms of device queue
        const int MaxPendingChunks = 96; // ~500 ms of listener chunks (dsp 256) before dropping

        IntPtr handle = IntPtr.Zero;
        int sampleRate;
        int channels;
        readonly List<WaveBuffer> buffers = new();
        readonly ConcurrentQueue<float[]> pending = new();
        readonly AutoResetEvent signal = new(false);
        Thread worker;
        volatile bool running;
        float[] carry = Array.Empty<float>();
        int carryCount;
        int dropped;
        bool mirroring;
        float nextStatusCheck;

        public static PcAudioMonitor Instance { get; private set; }

        public MonitorMode Mode
        {
            get => mode;
            set
            {
                if (mode == value)
                    return;
                mode = value;
                PlayerPrefs.SetString(ModePrefKey, value.ToString());
                Refresh();
            }
        }

        public string DeviceNameContains
        {
            get => deviceNameContains;
            set
            {
                if (deviceNameContains == (value ?? ""))
                    return;
                deviceNameContains = value ?? "";
                PlayerPrefs.SetString(DevicePrefKey, deviceNameContains);
                Refresh();
            }
        }

        public bool IsMirroring => mirroring;
        public string MirrorDeviceName { get; private set; } = "";
        public string Status { get; private set; } = "";
        public int DroppedChunks => dropped;

        /// <summary>Windows default playback device (empty when unknown).</summary>
        public static string DefaultOutputName { get; private set; } = "";
        public static bool DefaultOutputMuted { get; private set; }
        public static float DefaultOutputVolume { get; private set; } = -1f;
        public static string DefaultOutputError { get; private set; } = "";

        /// <summary>
        /// Playback device that Unity's own audio session is on (empty when not found). This is not always the
        /// Windows default: with Quest Link the Oculus runtime moves Unity onto "Oculus Virtual Audio Device".
        /// </summary>
        public static string UnityOutputName { get; private set; } = "";
        public static bool UnityOutputMuted { get; private set; }
        public static float UnityOutputVolume { get; private set; } = -1f;

        /// <summary>Device Unity's audio actually goes to: its own session's device, else the Windows default.</summary>
        public static string EffectiveOutputName => string.IsNullOrEmpty(UnityOutputName) ? DefaultOutputName : UnityOutputName;
        public static bool EffectiveOutputMuted => string.IsNullOrEmpty(UnityOutputName) ? DefaultOutputMuted : UnityOutputMuted;

        public static PcAudioMonitor Ensure()
        {
            if (Instance != null)
                return Instance;
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
                return null;
            return listener.GetComponent<PcAudioMonitor>() ?? listener.gameObject.AddComponent<PcAudioMonitor>();
        }

        public static string[] OutputDeviceNames()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var n = waveOutGetNumDevs();
            var names = new string[n];
            for (var i = 0; i < n; i++)
            {
                var caps = new WAVEOUTCAPS();
                names[i] = waveOutGetDevCapsW((IntPtr)i, ref caps, Marshal.SizeOf<WAVEOUTCAPS>()) == 0 ? caps.szPname : "device " + i;
            }
            return names;
#else
            return Array.Empty<string>();
#endif
        }

        public bool IsHeadsetDevice(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            foreach (var key in headsetKeywords)
            {
                if (!string.IsNullOrEmpty(key) && name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Re-read the Windows default output and (re)start or stop the mirror according to the mode.</summary>
        public void Refresh()
        {
            RefreshDefaultOutput();
            var unityOut = EffectiveOutputName;
            var want = mode == MonitorMode.Always || (mode == MonitorMode.Auto && IsHeadsetDevice(unityOut));
            if (!want)
            {
                Status = mode == MonitorMode.Off ? "off" : "not needed (Unity already plays on '" + unityOut + "')";
                StopMirror();
                return;
            }

            var index = PickDevice(out var name);
            if (index < 0)
            {
                Status = "no PC output device found";
                StopMirror();
                return;
            }

            if (mirroring && MirrorDeviceName == name)
                return;

            StopMirror();
            StartMirror(index, name);
        }

        public static void RefreshDefaultOutput()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                WindowsDefaultOutput.Query(out var name, out var muted, out var vol);
                DefaultOutputName = name;
                DefaultOutputMuted = muted;
                DefaultOutputVolume = vol;
                DefaultOutputError = "";
            }
            catch (Exception e)
            {
                DefaultOutputError = e.GetType().Name + ": " + e.Message;
                // winmm lists the current default playback device first.
                var names = OutputDeviceNames();
                DefaultOutputName = names.Length > 0 ? names[0] : "";
                DefaultOutputVolume = -1f;
                DefaultOutputMuted = false;
            }

            try
            {
                // The mirror's own waveOut stream is also a session of this process, on the PC device: ignore
                // that device while mirroring, and keep the last known answer if it was the only match.
                var exclude = Instance != null && Instance.mirroring ? Instance.MirrorDeviceName : "";
                if (WindowsDefaultOutput.QueryProcessOutput(System.Diagnostics.Process.GetCurrentProcess().Id, exclude, out var uname, out var umuted, out var uvol, out var onlyExcluded))
                {
                    UnityOutputName = uname;
                    UnityOutputMuted = umuted;
                    UnityOutputVolume = uvol;
                }
                else if (!onlyExcluded)
                {
                    UnityOutputName = "";
                    UnityOutputMuted = false;
                    UnityOutputVolume = -1f;
                }
            }
            catch (Exception e)
            {
                UnityOutputName = "";
                UnityOutputMuted = false;
                UnityOutputVolume = -1f;
                if (string.IsNullOrEmpty(DefaultOutputError))
                    DefaultOutputError = "session: " + e.GetType().Name + ": " + e.Message;
            }
#endif
        }

        /// <summary>Unmute the device Unity's own audio session is on (e.g. the muted Oculus virtual device).</summary>
        public static bool UnmuteUnityOutput()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (string.IsNullOrEmpty(UnityOutputName))
                    return UnmuteDefaultOutput();
                WindowsDefaultOutput.SetMuteForProcessOutput(System.Diagnostics.Process.GetCurrentProcess().Id, UnityOutputName, false);
                RefreshDefaultOutput();
                return !UnityOutputMuted;
            }
            catch (Exception e)
            {
                DefaultOutputError = e.GetType().Name + ": " + e.Message;
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Unmute the Windows default playback device (experimenter action from the panel).</summary>
        public static bool UnmuteDefaultOutput()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                WindowsDefaultOutput.SetMute(false);
                RefreshDefaultOutput();
                return !DefaultOutputMuted;
            }
            catch (Exception e)
            {
                DefaultOutputError = e.GetType().Name + ": " + e.Message;
                return false;
            }
#else
            return false;
#endif
        }

        public string Describe()
        {
            return "pcMonitor mode=" + mode + " mirroring=" + mirroring + " device='" + MirrorDeviceName + "' status='" + Status
                   + "' defaultOutput='" + DefaultOutputName + "' muted=" + DefaultOutputMuted + " vol=" + DefaultOutputVolume.ToString("0.00")
                   + " unityOutput='" + UnityOutputName + "' unityMuted=" + UnityOutputMuted + " unityVol=" + UnityOutputVolume.ToString("0.00")
                   + (string.IsNullOrEmpty(DefaultOutputError) ? "" : " err=" + DefaultOutputError) + " dropped=" + dropped;
        }

        void Awake()
        {
            Instance = this;
            if (Enum.TryParse(PlayerPrefs.GetString(ModePrefKey, mode.ToString()), out MonitorMode stored))
                mode = stored;
            deviceNameContains = PlayerPrefs.GetString(DevicePrefKey, deviceNameContains);
        }

        void OnEnable()
        {
            sampleRate = AudioSettings.outputSampleRate;
            Refresh();
            nextStatusCheck = Time.unscaledTime + 5f;
        }

        void OnDisable()
        {
            StopMirror();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (Time.unscaledTime < nextStatusCheck)
                return;
            nextStatusCheck = Time.unscaledTime + 5f;
            var before = EffectiveOutputName;
            RefreshDefaultOutput();
            if (before != EffectiveOutputName && mode == MonitorMode.Auto)
                Refresh();
        }

        int PickDevice(out string name)
        {
            name = "";
            var names = OutputDeviceNames();
            var filter = deviceNameContains ?? "";
            for (var i = 0; i < names.Length; i++)
            {
                if (filter.Length > 0)
                {
                    if (names[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        name = names[i];
                        return i;
                    }
                }
                else if (!IsHeadsetDevice(names[i]))
                {
                    name = names[i];
                    return i;
                }
            }
            return -1;
        }

        void StartMirror(int deviceIndex, string name)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            channels = Mathf.Clamp(ChannelsOf(AudioSettings.speakerMode), 1, 2);
            var format = new WAVEFORMATEX
            {
                wFormatTag = 1,
                nChannels = (short)channels,
                nSamplesPerSec = sampleRate,
                wBitsPerSample = 16,
            };
            format.nBlockAlign = (short)(format.nChannels * format.wBitsPerSample / 8);
            format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;

            var result = waveOutOpen(out handle, deviceIndex, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (result != 0)
            {
                Status = "waveOutOpen failed (" + result + ") for " + name;
                handle = IntPtr.Zero;
                return;
            }

            for (var i = 0; i < BufferCount; i++)
                buffers.Add(new WaveBuffer(handle, BufferFrames * channels));

            running = true;
            worker = new Thread(Pump) { IsBackground = true, Name = "AvianVoice PC audio monitor", Priority = System.Threading.ThreadPriority.AboveNormal };
            worker.Start();
            mirroring = true;
            MirrorDeviceName = name;
            Status = "mirroring to " + name;
            Debug.Log("[AvianVoice] PC audio monitor: mirroring the listener to '" + name + "' (" + sampleRate + " Hz, " + channels + " ch). Windows default output: '" + DefaultOutputName + "'.");
#else
            Status = "Windows only";
#endif
        }

        void StopMirror()
        {
            running = false;
            signal.Set();
            if (worker != null)
            {
                worker.Join(500);
                worker = null;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (handle != IntPtr.Zero)
            {
                waveOutReset(handle);
                foreach (var b in buffers)
                    b.Dispose(handle);
                buffers.Clear();
                waveOutClose(handle);
                handle = IntPtr.Zero;
            }
#endif
            while (pending.TryDequeue(out _)) { }
            carryCount = 0;
            if (mirroring)
                Debug.Log("[AvianVoice] PC audio monitor stopped.");
            mirroring = false;
            MirrorDeviceName = "";
        }

        void OnAudioFilterRead(float[] data, int dataChannels)
        {
            if (!mirroring || !running)
                return;
            if (pending.Count > MaxPendingChunks)
            {
                dropped++;
                return;
            }
            var copy = new float[data.Length + 1];
            Array.Copy(data, copy, data.Length);
            copy[data.Length] = dataChannels;
            pending.Enqueue(copy);
            signal.Set();
        }

        void Pump()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var frameBuffer = new short[BufferFrames * channels];
            var fill = 0;
            while (running)
            {
                if (!pending.TryDequeue(out var chunk))
                {
                    signal.WaitOne(50);
                    continue;
                }

                var srcChannels = Mathf.Max(1, (int)chunk[chunk.Length - 1]);
                var frames = (chunk.Length - 1) / srcChannels;
                for (var f = 0; f < frames; f++)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var s = chunk[f * srcChannels + Mathf.Min(c, srcChannels - 1)] * volume;
                        frameBuffer[fill++] = (short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), short.MinValue, short.MaxValue);
                    }

                    if (fill < frameBuffer.Length)
                        continue;

                    var buffer = FindFreeBuffer();
                    if (buffer != null)
                        buffer.Write(handle, frameBuffer);
                    else
                        dropped++;
                    fill = 0;
                }
            }
#endif
        }

        WaveBuffer FindFreeBuffer()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                foreach (var b in buffers)
                {
                    if (b.IsFree)
                        return b;
                }
                if (!running)
                    return null;
                Thread.Sleep(2);
            }
            return null;
        }

        static int ChannelsOf(AudioSpeakerMode speakerMode)
        {
            return speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                _ => 2,
            };
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // ---- winmm waveOut -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WAVEOUTCAPS
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public int dwFormats;
            public short wChannels;
            public short wReserved1;
            public int dwSupport;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        const int WHDR_DONE = 1;
        static readonly int FlagsOffset = (int)Marshal.OffsetOf<WAVEHDR>("dwFlags");

        [DllImport("winmm.dll")] static extern int waveOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int waveOutGetDevCapsW(IntPtr uDeviceID, ref WAVEOUTCAPS caps, int size);
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WAVEFORMATEX format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hWaveOut, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hWaveOut);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hWaveOut);

        sealed class WaveBuffer
        {
            readonly IntPtr data;
            readonly IntPtr header;
            readonly int bytes;
            bool used;

            public WaveBuffer(IntPtr device, int samples)
            {
                bytes = samples * 2;
                data = Marshal.AllocHGlobal(bytes);
                header = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
                var h = new WAVEHDR { lpData = data, dwBufferLength = bytes };
                Marshal.StructureToPtr(h, header, false);
                waveOutPrepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
            }

            public bool IsFree
            {
                get
                {
                    if (!used)
                        return true;
                    var flags = Marshal.ReadInt32(header, FlagsOffset);
                    return (flags & WHDR_DONE) != 0;
                }
            }

            public void Write(IntPtr device, short[] samples)
            {
                Marshal.Copy(samples, 0, data, samples.Length);
                // Clear WHDR_DONE before re-queueing (keep WHDR_PREPARED).
                var flags = Marshal.ReadInt32(header, FlagsOffset);
                Marshal.WriteInt32(header, FlagsOffset, flags & ~WHDR_DONE);
                used = true;
                waveOutWrite(device, header, Marshal.SizeOf<WAVEHDR>());
            }

            public void Dispose(IntPtr device)
            {
                waveOutUnprepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
                Marshal.FreeHGlobal(header);
                Marshal.FreeHGlobal(data);
            }
        }

        // ---- Windows default playback device (Core Audio COM) ---------------------------------------

        static class WindowsDefaultOutput
        {
            [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
            class MMDeviceEnumeratorComObject { }

            [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IMMDeviceEnumerator
            {
                int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
                int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            }

            [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IMMDeviceCollection
            {
                [PreserveSig] int GetCount(out int count);
                [PreserveSig] int Item(int index, out IMMDevice device);
            }

            [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IAudioSessionManager2
            {
                [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
                [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
                [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
            }

            [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IAudioSessionEnumerator
            {
                [PreserveSig] int GetCount(out int count);
                [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
            }

            [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IAudioSessionControl2
            {
                [PreserveSig] int GetState(out int state);
                [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
                [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr context);
                [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
                [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr context);
                [PreserveSig] int GetGroupingParam(out Guid grouping);
                [PreserveSig] int SetGroupingParam(ref Guid grouping, IntPtr context);
                [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
                [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
                [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
                [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
                [PreserveSig] int GetProcessId(out int processId);
                [PreserveSig] int IsSystemSoundsSession();
                [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
            }

            [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IMMDevice
            {
                int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
                int OpenPropertyStore(int access, out IPropertyStore store);
            }

            [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IPropertyStore
            {
                int GetCount(out int count);
                int GetAt(int index, out PROPERTYKEY key);
                int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
            }

            [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IAudioEndpointVolume
            {
                int RegisterControlChangeNotify(IntPtr notify);
                int UnregisterControlChangeNotify(IntPtr notify);
                int GetChannelCount(out int count);
                int SetMasterVolumeLevel(float db, ref Guid context);
                int SetMasterVolumeLevelScalar(float level, ref Guid context);
                int GetMasterVolumeLevel(out float db);
                int GetMasterVolumeLevelScalar(out float level);
                int SetChannelVolumeLevel(int channel, float db, ref Guid context);
                int SetChannelVolumeLevelScalar(int channel, float level, ref Guid context);
                int GetChannelVolumeLevel(int channel, out float db);
                int GetChannelVolumeLevelScalar(int channel, out float level);
                int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
                int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct PROPERTYKEY
            {
                public Guid fmtid;
                public int pid;
            }

            [StructLayout(LayoutKind.Explicit)]
            struct PROPVARIANT
            {
                [FieldOffset(0)] public short vt;
                [FieldOffset(8)] public IntPtr pointerValue;
            }

            const int ERender = 0;
            const int RoleMultimedia = 1;
            const int ClsctxAll = 23;
            static readonly Guid FriendlyNameFmtid = new("a45c254e-df1c-4efd-8020-67d146a850e0");

            static IMMDevice DefaultDevice()
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(ERender, RoleMultimedia, out var device));
                return device;
            }

            static IAudioEndpointVolume Volume(IMMDevice device)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var obj));
                return (IAudioEndpointVolume)obj;
            }

            public static void Query(out string name, out bool muted, out float volume)
            {
                var device = DefaultDevice();
                Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out var store));
                var key = new PROPERTYKEY { fmtid = FriendlyNameFmtid, pid = 14 };
                Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
                name = value.vt == 31 && value.pointerValue != IntPtr.Zero ? Marshal.PtrToStringUni(value.pointerValue) : "";
                var vol = Volume(device);
                Marshal.ThrowExceptionForHR(vol.GetMute(out muted));
                Marshal.ThrowExceptionForHR(vol.GetMasterVolumeLevelScalar(out volume));
            }

            public static void SetMute(bool mute)
            {
                var context = Guid.Empty;
                Marshal.ThrowExceptionForHR(Volume(DefaultDevice()).SetMute(mute, ref context));
            }

            static string FriendlyName(IMMDevice device)
            {
                Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out var store));
                var key = new PROPERTYKEY { fmtid = FriendlyNameFmtid, pid = 14 };
                Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
                return value.vt == 31 && value.pointerValue != IntPtr.Zero ? Marshal.PtrToStringUni(value.pointerValue) : "";
            }

            /// <summary>
            /// -1 = no session of the process on this device; otherwise the best session state found
            /// (1 = active stream, 0 = inactive: a stream that was closed, e.g. an earlier mirror).
            /// </summary>
            static int SessionStateOf(IMMDevice device, int processId)
            {
                const int SessionActive = 1;
                const int SessionExpired = 2;
                var managerIid = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref managerIid, ClsctxAll, IntPtr.Zero, out var obj) != 0)
                    return -1;
                var manager = (IAudioSessionManager2)obj;
                if (manager.GetSessionEnumerator(out var sessions) != 0 || sessions.GetCount(out var sessionCount) != 0)
                    return -1;
                var best = -1;
                for (var j = 0; j < sessionCount; j++)
                {
                    if (sessions.GetSession(j, out var session) != 0 || session == null)
                        continue;
                    if (session.GetProcessId(out var pid) != 0 || pid != processId || session.GetState(out var state) != 0 || state == SessionExpired)
                        continue;
                    best = Math.Max(best, state == SessionActive ? 1 : 0);
                }
                return best;
            }

            static bool HasSessionOf(IMMDevice device, int processId)
            {
                return SessionStateOf(device, processId) >= 0;
            }

            /// <summary>
            /// The active render endpoint that has an audio session of the given process, skipping the device
            /// named <paramref name="excludeName"/> (the mirror's own output). <paramref name="onlyExcluded"/>
            /// is true when the excluded device was the only match.
            /// </summary>
            static IMMDevice ProcessDevice(int processId, string excludeName, out string name, out bool onlyExcluded)
            {
                const int DeviceStateActive = 1;
                name = "";
                onlyExcluded = false;
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(ERender, DeviceStateActive, out var devices));
                Marshal.ThrowExceptionForHR(devices.GetCount(out var count));
                // Unity's own (FMOD) stream is always active, even when silent; a device where this process only
                // has an inactive session is one it used to play on (an earlier mirror), so it ranks lower.
                IMMDevice best = null;
                var bestState = -1;
                var excludedSeen = false;
                for (var i = 0; i < count; i++)
                {
                    if (devices.Item(i, out var device) != 0 || device == null)
                        continue;
                    var state = SessionStateOf(device, processId);
                    if (state < 0)
                        continue;
                    var friendly = FriendlyName(device);
                    if (!string.IsNullOrEmpty(excludeName) && friendly == excludeName)
                    {
                        excludedSeen = true;
                        continue;
                    }
                    if (state > bestState)
                    {
                        best = device;
                        bestState = state;
                        name = friendly;
                    }
                }
                onlyExcluded = best == null && excludedSeen;
                return best;
            }

            public static bool QueryProcessOutput(int processId, string excludeName, out string name, out bool muted, out float volume, out bool onlyExcluded)
            {
                muted = false;
                volume = -1f;
                var device = ProcessDevice(processId, excludeName, out name, out onlyExcluded);
                if (device == null)
                    return false;
                var vol = Volume(device);
                Marshal.ThrowExceptionForHR(vol.GetMute(out muted));
                Marshal.ThrowExceptionForHR(vol.GetMasterVolumeLevelScalar(out volume));
                return true;
            }

            /// <summary>Mute / unmute the device named <paramref name="deviceName"/> among those this process plays on.</summary>
            public static void SetMuteForProcessOutput(int processId, string deviceName, bool mute)
            {
                const int DeviceStateActive = 1;
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(ERender, DeviceStateActive, out var devices));
                Marshal.ThrowExceptionForHR(devices.GetCount(out var count));
                for (var i = 0; i < count; i++)
                {
                    if (devices.Item(i, out var device) != 0 || device == null || FriendlyName(device) != deviceName || !HasSessionOf(device, processId))
                        continue;
                    var context = Guid.Empty;
                    Marshal.ThrowExceptionForHR(Volume(device).SetMute(mute, ref context));
                    return;
                }
                throw new InvalidOperationException("no audio session of this process on '" + deviceName + "'");
            }
        }
#endif
    }
}
