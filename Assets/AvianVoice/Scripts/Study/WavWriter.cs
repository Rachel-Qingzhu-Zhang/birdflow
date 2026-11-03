using System;
using System.IO;
using System.Text;

namespace AvianVoice.Study
{
    /// <summary>
    /// 16-bit PCM WAV streamed to disk. The RIFF sizes are patched when the file is closed, so a file
    /// that was never closed (crash) still holds the samples and can be repaired by reading its length.
    /// </summary>
    public sealed class WavWriter : IDisposable
    {
        readonly FileStream stream;
        readonly BinaryWriter writer;
        readonly int sampleRate;
        readonly int channels;
        byte[] scratch;
        long dataBytes;
        bool closed;

        public string Path { get; }
        public int SampleRate => sampleRate;
        public int Channels => channels;
        public long FramesWritten => dataBytes / (2L * channels);
        public bool IsOpen => !closed;

        public WavWriter(string path, int sampleRate, int channels)
        {
            Path = path;
            this.sampleRate = Math.Max(1, sampleRate);
            this.channels = Math.Max(1, channels);
            stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            writer = new BinaryWriter(stream);
            WriteHeader(0);
        }

        /// <summary>Append interleaved float samples in [-1, 1].</summary>
        public void Write(float[] samples, int offset, int count)
        {
            if (closed || samples == null || count <= 0)
                return;

            var bytes = count * 2;
            if (scratch == null || scratch.Length < bytes)
                scratch = new byte[Math.Max(bytes, 8192)];

            for (var i = 0; i < count; i++)
            {
                var v = samples[offset + i];
                if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                var s = (short)Math.Round(v * 32767f);
                scratch[i * 2] = (byte)(s & 0xFF);
                scratch[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            stream.Write(scratch, 0, bytes);
            dataBytes += bytes;
        }

        public void Flush()
        {
            if (!closed)
                stream.Flush();
        }

        public void Close()
        {
            if (closed)
                return;

            closed = true;
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            WriteHeader(dataBytes);
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
        }

        public void Dispose() => Close();

        void WriteHeader(long data)
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((int)Math.Min(int.MaxValue, 36 + data));
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((int)Math.Min(int.MaxValue, data));
        }

        /// <summary>Write a complete mono/interleaved buffer as one file.</summary>
        public static void WriteFile(string path, float[] samples, int count, int sampleRate, int channels)
        {
            using var w = new WavWriter(path, sampleRate, channels);
            w.Write(samples, 0, count);
        }
    }
}
