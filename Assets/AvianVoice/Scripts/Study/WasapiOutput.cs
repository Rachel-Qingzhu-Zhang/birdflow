using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace AvianVoice.Study
{
    /// <summary>
    /// Low-latency playback of a stereo float stream on one Windows playback device (WASAPI, shared mode,
    /// event driven). Used by <see cref="PcAudioMonitor"/> for the copy of Unity's mix that goes to the PC device.
    ///
    /// The producer (Unity's audio thread, <see cref="Write"/>) and the device run on different clocks, and the
    /// producer stops for a moment now and then (about 20 ms every ~13 s in the editor on 2026-11-12). So the
    /// stream keeps a reserve: the audio waiting in the ring plus the audio already handed to the device,
    /// held at <c>targetMs</c> by resampling the stream by at most 0.3 % (5 cents). That reserve is the delay
    /// this output adds after Unity's mix (plus the device's own stream latency).
    /// <list type="bullet">
    /// <item>An underrun (the device ran dry: a short gap) means the reserve is too small for the stops: raise the target.</item>
    /// <item>A drop (far too much queued, e.g. after the render thread was held up) throws the excess away.</item>
    /// </list>
    /// The interfaces are called through their vtables (IAudioClient, IAudioRenderClient: documented COM
    /// interfaces) so that the render thread does not depend on the runtime's COM wrappers.
    /// </summary>
    public sealed class WasapiOutput : IDisposable
    {
        const int RingFrames = 1 << 15; // 0.68 s at 48 kHz
        const int RingMask = RingFrames - 1;
        const uint StreamFlagEventCallback = 0x00040000;
        const uint StreamFlagAutoConvertPcm = 0x80000000;
        const uint StreamFlagSrcDefaultQuality = 0x08000000;
        const uint BufferFlagSilent = 0x2;
        const float MaxRateTrim = 0.003f;
        const float RateTrimPerMs = 0.0002f;
        const float DropAboveTargetMs = 150f;

        static readonly Guid AudioRenderClientId = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

        // IAudioClient: Initialize 3, GetBufferSize 4, GetStreamLatency 5, GetCurrentPadding 6, GetMixFormat 8,
        // Start 10, Stop 11, SetEventHandle 13, GetService 14. IAudioRenderClient: GetBuffer 3, ReleaseBuffer 4.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int InitializeFn(IntPtr self, int shareMode, uint flags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetUIntFn(IntPtr self, out uint value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetLongFn(IntPtr self, out long value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetPointerFn(IntPtr self, out IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NoArgumentFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetHandleFn(IntPtr self, IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetServiceFn(IntPtr self, ref Guid iid, out IntPtr service);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetBufferFn(IntPtr self, uint frames, out IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReleaseBufferFn(IntPtr self, uint frames, uint flags);

        [DllImport("kernel32.dll")] static extern IntPtr CreateEventW(IntPtr attributes, bool manualReset, bool initialState, IntPtr name);
        [DllImport("kernel32.dll")] static extern bool SetEvent(IntPtr handle);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, int model);
        [DllImport("ole32.dll")] static extern void CoUninitialize();
        [DllImport("avrt.dll", CharSet = CharSet.Unicode)] static extern IntPtr AvSetMmThreadCharacteristicsW(string task, ref uint taskIndex);
        [DllImport("avrt.dll")] static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);

        readonly float[] ring = new float[RingFrames * 2];
        long writeFrames; // published by the producer
        long readFrames;  // published by the render thread (frames it no longer needs)
        double readPos = 1.0;
        double ratio = 1.0;

        IntPtr client;
        IntPtr render;
        IntPtr eventHandle;
        GetUIntFn getPadding;
        GetBufferFn getBuffer;
        ReleaseBufferFn releaseBuffer;
        float[] scratch;
        Thread thread;
        volatile bool running;
        volatile bool failed;
        bool filling = true;
        int bufferFrames;
        int sourceRate;
        int deviceRate;
        float targetMs;
        float smoothedMs = -1f;
        volatile float bufferedMs;
        volatile float volume = 1f;
        int underruns;
        int drops;
        int droppedBlocks;

        /// <summary>The device stopped accepting audio (unplugged, format changed): dispose and start again.</summary>
        public bool Failed => failed;
        public string FailureText { get; private set; } = "";

        /// <summary>Audio held in reserve (ring + device buffer), smoothed: the delay added after Unity's mix.</summary>
        public float BufferedMs => bufferedMs;
        public float TargetMs => targetMs;

        /// <summary>What the device reports for its own stream latency (engine period and hardware), in ms.</summary>
        public float StreamLatencyMs { get; private set; }
        public float DeviceBufferMs => deviceRate > 0 ? bufferFrames * 1000f / deviceRate : 0f;
        public int DeviceRate => deviceRate;

        /// <summary>Times the device ran dry (a short gap was heard).</summary>
        public int Underruns => Volatile.Read(ref underruns);

        /// <summary>Times queued audio was thrown away to get back to the target.</summary>
        public int Drops => Volatile.Read(ref drops) + Volatile.Read(ref droppedBlocks);

        public float Volume
        {
            get => volume;
            set => volume = Mathf01(value);
        }

        static float Mathf01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>
        /// Start playing on the device behind <paramref name="audioClient"/> (an IAudioClient pointer; this object
        /// takes it over and releases it). Null with <paramref name="error"/> when the device refuses.
        /// </summary>
        public static WasapiOutput TryStart(IntPtr audioClient, int sourceRate, float volume, int deviceBufferMs, float targetMs, out string error)
        {
            error = "";
            if (audioClient == IntPtr.Zero)
            {
                error = "no audio client";
                return null;
            }

            var output = new WasapiOutput { client = audioClient, sourceRate = sourceRate, volume = Mathf01(volume) };
            try
            {
                output.Open(Math.Max(10, deviceBufferMs), targetMs);
                return output;
            }
            catch (Exception e)
            {
                error = e.Message;
                output.Dispose();
                return null;
            }
        }

        static void Check(int hr, string what)
        {
            if (hr != 0)
                throw new InvalidOperationException(what + " failed (0x" + hr.ToString("X8") + ")");
        }

        static T Slot<T>(IntPtr comObject, int index) where T : Delegate
        {
            var vtable = Marshal.ReadIntPtr(comObject);
            return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, index * IntPtr.Size));
        }

        void Open(int deviceBufferMs, float target)
        {
            Check(Slot<GetPointerFn>(client, 8)(client, out var mixFormat), "GetMixFormat");
            deviceRate = Marshal.ReadInt32(mixFormat, 4); // WAVEFORMATEX.nSamplesPerSec
            Marshal.FreeCoTaskMem(mixFormat);
            if (deviceRate <= 0 || sourceRate <= 0)
                throw new InvalidOperationException("bad sample rate (device " + deviceRate + ", source " + sourceRate + ")");

            // Stereo 32-bit float at the device's own rate: Windows converts the channel layout if it has to,
            // the rate is matched here (together with the clock trim).
            var format = Marshal.AllocHGlobal(18);
            try
            {
                Marshal.WriteInt16(format, 0, 3);                 // WAVE_FORMAT_IEEE_FLOAT
                Marshal.WriteInt16(format, 2, 2);                 // channels
                Marshal.WriteInt32(format, 4, deviceRate);
                Marshal.WriteInt32(format, 8, deviceRate * 8);    // bytes per second
                Marshal.WriteInt16(format, 12, 8);                // block align
                Marshal.WriteInt16(format, 14, 32);               // bits per sample
                Marshal.WriteInt16(format, 16, 0);
                Check(Slot<InitializeFn>(client, 3)(client, 0, StreamFlagEventCallback | StreamFlagAutoConvertPcm | StreamFlagSrcDefaultQuality,
                    deviceBufferMs * 10000L, 0, format, IntPtr.Zero), "IAudioClient.Initialize");
            }
            finally
            {
                Marshal.FreeHGlobal(format);
            }

            Check(Slot<GetUIntFn>(client, 4)(client, out var frames), "GetBufferSize");
            bufferFrames = (int)frames;
            if (Slot<GetLongFn>(client, 5)(client, out var latency) == 0)
                StreamLatencyMs = latency / 10000f;

            eventHandle = CreateEventW(IntPtr.Zero, false, false, IntPtr.Zero);
            if (eventHandle == IntPtr.Zero)
                throw new InvalidOperationException("CreateEvent failed");
            Check(Slot<SetHandleFn>(client, 13)(client, eventHandle), "SetEventHandle");

            var iid = AudioRenderClientId;
            Check(Slot<GetServiceFn>(client, 14)(client, ref iid, out render), "GetService(IAudioRenderClient)");
            getPadding = Slot<GetUIntFn>(client, 6);
            getBuffer = Slot<GetBufferFn>(render, 3);
            releaseBuffer = Slot<ReleaseBufferFn>(render, 4);

            // Start with a full buffer of silence so the first real audio does not meet an empty device.
            Check(getBuffer(render, (uint)bufferFrames, out _), "GetBuffer");
            Check(releaseBuffer(render, (uint)bufferFrames, BufferFlagSilent), "ReleaseBuffer");

            scratch = new float[bufferFrames * 2];
            targetMs = Math.Max(target, DeviceBufferMs + 5f);
            ratio = (double)sourceRate / deviceRate;
            running = true;
            thread = new Thread(RenderLoop) { IsBackground = true, Name = "AvianVoice PC output", Priority = ThreadPriority.Highest };
            thread.Start();
            Check(Slot<NoArgumentFn>(client, 10)(client), "IAudioClient.Start");
        }

        /// <summary>Unity's audio thread: append one block of the mix (interleaved, any channel count; the first two are used).</summary>
        public void Write(float[] data, int channels)
        {
            if (!running || channels <= 0)
                return;

            var frames = data.Length / channels;
            var write = writeFrames;
            if (write + frames - Volatile.Read(ref readFrames) > RingFrames - 8)
            {
                Interlocked.Increment(ref droppedBlocks);
                return;
            }

            for (var f = 0; f < frames; f++)
            {
                var index = (int)((write + f) & RingMask) * 2;
                var left = data[f * channels];
                ring[index] = left;
                ring[index + 1] = channels > 1 ? data[f * channels + 1] : left;
            }

            Volatile.Write(ref writeFrames, write + frames);
        }

        void RenderLoop()
        {
            var comReady = false;
            var mmTask = IntPtr.Zero;
            try
            {
                comReady = CoInitializeEx(IntPtr.Zero, 0) >= 0; // multithreaded
                try
                {
                    uint taskIndex = 0;
                    mmTask = AvSetMmThreadCharacteristicsW("Pro Audio", ref taskIndex);
                }
                catch (Exception)
                {
                    // No multimedia scheduling: the thread keeps its normal high priority.
                }

                while (running)
                {
                    var wait = WaitForSingleObject(eventHandle, 200);
                    if (!running)
                        break;
                    if (wait != 0)
                        continue;

                    var hr = getPadding(client, out var padding);
                    if (hr != 0)
                    {
                        Fail("GetCurrentPadding", hr);
                        break;
                    }

                    // The device ran dry while audio was flowing: a gap. Hold the output back until the whole
                    // reserve is there again (one slightly longer gap instead of a series of them); the same
                    // at the start, where the device plays its silence until the reserve is built.
                    if (!filling && padding == 0)
                    {
                        Interlocked.Increment(ref underruns);
                        filling = true;
                    }

                    if (filling)
                    {
                        var waiting = Volatile.Read(ref writeFrames) - (long)readPos;
                        var haveMs = padding * 1000f / deviceRate + waiting * 1000f / sourceRate;
                        if (waiting > 0 && haveMs >= targetMs)
                        {
                            filling = false;
                            smoothedMs = haveMs;
                        }
                    }

                    var room = bufferFrames - (int)padding;
                    var produced = !filling && room > 0 ? Produce(room) : 0;
                    if (produced > 0)
                    {
                        hr = getBuffer(render, (uint)produced, out var target);
                        if (hr != 0)
                        {
                            Fail("GetBuffer", hr);
                            break;
                        }

                        Marshal.Copy(scratch, 0, target, produced * 2);
                        hr = releaseBuffer(render, (uint)produced, 0);
                        if (hr != 0)
                        {
                            Fail("ReleaseBuffer", hr);
                            break;
                        }
                    }

                    Steer((int)padding + produced);
                }
            }
            catch (Exception e)
            {
                FailureText = e.GetType().Name + ": " + e.Message;
                failed = true;
            }
            finally
            {
                if (mmTask != IntPtr.Zero)
                    AvRevertMmThreadCharacteristics(mmTask);
                if (comReady)
                    CoUninitialize();
            }
        }

        void Fail(string what, int hr)
        {
            FailureText = what + " failed (0x" + hr.ToString("X8") + ")";
            failed = true;
        }

        /// <summary>Fill up to <paramref name="maxFrames"/> device frames from the ring (4-point cubic interpolation); returns the count.</summary>
        int Produce(int maxFrames)
        {
            var write = Volatile.Read(ref writeFrames);
            var availableSource = write - (long)readPos - 3; // the interpolation reads two frames ahead
            if (availableSource <= 0)
                return 0;

            var step = ratio;
            var count = (int)Math.Min(maxFrames, Math.Floor(availableSource / step));
            var gain = volume;
            var position = readPos;
            for (var i = 0; i < count; i++)
            {
                var whole = (long)position;
                var t = (float)(position - whole);
                var i0 = (int)((whole - 1) & RingMask) * 2;
                var i1 = (int)(whole & RingMask) * 2;
                var i2 = (int)((whole + 1) & RingMask) * 2;
                var i3 = (int)((whole + 2) & RingMask) * 2;
                for (var c = 0; c < 2; c++)
                {
                    var y0 = ring[i0 + c];
                    var y1 = ring[i1 + c];
                    var y2 = ring[i2 + c];
                    var y3 = ring[i3 + c];
                    var c1 = 0.5f * (y2 - y0);
                    var c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
                    var c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
                    scratch[i * 2 + c] = (((c3 * t + c2) * t + c1) * t + y1) * gain;
                }

                position += step;
            }

            readPos = position;
            Volatile.Write(ref readFrames, (long)position - 1);
            return count;
        }

        /// <summary>Hold the reserve (device buffer + ring) at the target by trimming the resampling ratio.</summary>
        void Steer(int deviceFrames)
        {
            var ringFrames = Volatile.Read(ref writeFrames) - (long)readPos;
            var totalMs = deviceFrames * 1000f / deviceRate + ringFrames * 1000f / sourceRate;
            if (filling)
            {
                bufferedMs = totalMs;
                return;
            }

            if (totalMs > targetMs + DropAboveTargetMs)
            {
                // Far too much queued: skip ahead in the ring to the target.
                var skip = (long)((totalMs - targetMs) * sourceRate / 1000f);
                skip = Math.Min(skip, Math.Max(0, ringFrames - 4));
                readPos += skip;
                Volatile.Write(ref readFrames, (long)readPos - 1);
                Interlocked.Increment(ref drops);
                smoothedMs = targetMs;
            }
            else
            {
                smoothedMs = smoothedMs < 0f ? totalMs : smoothedMs + (totalMs - smoothedMs) * 0.02f;
            }

            var trim = (smoothedMs - targetMs) * RateTrimPerMs;
            trim = trim < -MaxRateTrim ? -MaxRateTrim : trim > MaxRateTrim ? MaxRateTrim : trim;
            ratio = (double)sourceRate / deviceRate * (1.0 + trim);
            bufferedMs = smoothedMs;
        }

        public void Dispose()
        {
            running = false;
            if (eventHandle != IntPtr.Zero)
                SetEvent(eventHandle);
            if (thread != null)
            {
                thread.Join(500);
                thread = null;
            }

            if (client != IntPtr.Zero)
            {
                try
                {
                    Slot<NoArgumentFn>(client, 11)(client); // Stop
                }
                catch (Exception)
                {
                    // The device may already be gone.
                }
            }

            if (render != IntPtr.Zero)
            {
                Marshal.Release(render);
                render = IntPtr.Zero;
            }

            if (client != IntPtr.Zero)
            {
                Marshal.Release(client);
                client = IntPtr.Zero;
            }

            if (eventHandle != IntPtr.Zero)
            {
                CloseHandle(eventHandle);
                eventHandle = IntPtr.Zero;
            }
        }
    }
}
