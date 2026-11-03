using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// Sits on the AudioListener and copies the final mix (what the participant hears: guide notes,
    /// the bird voice, ambience) as mono chunks for <see cref="StudyRecorder"/>. Audio passes through
    /// unchanged. Runs on the audio thread; the recorder drains the queue on the main thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ListenerOutputTap : MonoBehaviour
    {
        readonly ConcurrentQueue<float[]> chunks = new();
        volatile bool capturing;
        volatile int channels;
        long framesSeen;

        public bool Capturing
        {
            get => capturing;
            set => capturing = value;
        }

        public int Channels => channels;
        public long FramesSeen => Interlocked.Read(ref framesSeen);
        public int QueuedChunks => chunks.Count;

        void OnAudioFilterRead(float[] data, int channelCount)
        {
            channels = channelCount;
            if (!capturing || channelCount <= 0)
                return;

            var frames = data.Length / channelCount;
            var mono = new float[frames];
            var scale = 1f / channelCount;
            for (var n = 0; n < frames; n++)
            {
                var sum = 0f;
                var baseIndex = n * channelCount;
                for (var c = 0; c < channelCount; c++)
                    sum += data[baseIndex + c];
                mono[n] = sum * scale;
            }

            chunks.Enqueue(mono);
            Interlocked.Add(ref framesSeen, frames);
            // Drop the oldest chunks if nobody drains the queue (recorder destroyed), bounded memory.
            while (chunks.Count > 2048 && chunks.TryDequeue(out _))
            {
            }
        }

        public bool TryDequeue(out float[] mono) => chunks.TryDequeue(out mono);

        public void Clear()
        {
            while (chunks.TryDequeue(out _))
            {
            }
        }
    }
}
