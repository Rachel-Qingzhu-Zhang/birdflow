using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    public sealed class VoiceAnalyzer : MonoBehaviour
    {
        AvianVoiceSettings settings;
        AvianSignals signals;
        float[] analysisBuffer;
        float smoothedLoudness;

        public VoiceFrame Current { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings, AvianSignals avianSignals)
        {
            settings = voiceSettings;
            signals = avianSignals;
            analysisBuffer = new float[settings.frameSize];
        }

        public void PushSamples(float[] samples, int count)
        {
            if (settings == null || count <= 0)
                return;

            var copyCount = Mathf.Min(count, analysisBuffer.Length);
            for (var i = 0; i < copyCount; i++)
                analysisBuffer[i] = samples[i];

            var rms = 0f;
            for (var i = 0; i < copyCount; i++)
                rms += analysisBuffer[i] * analysisBuffer[i];
            rms = Mathf.Sqrt(rms / copyCount);

            var dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            smoothedLoudness = Mathf.Lerp(
                smoothedLoudness,
                rms,
                1f - Mathf.Exp(-settings.loudnessSmooth * dt));

            var pitch = EstimatePitchAutocorrelation(
                analysisBuffer,
                settings.sampleRate,
                settings.minPitchHz,
                settings.maxPitchHz);

            var voiced = smoothedLoudness > settings.voicedThreshold ? 1f : 0f;
            var frameMs = (settings.frameSize / (float)settings.sampleRate) * 1000f;
            var hopMs = (Mathf.Clamp(settings.analysisHopSize, 64, settings.frameSize) /
                           (float)settings.sampleRate) * 1000f;
            var dspMs = AudioSettings.GetConfiguration().dspBufferSize > 0
                ? AudioSettings.GetConfiguration().dspBufferSize / (float)AudioSettings.outputSampleRate * 1000f
                : 10f;

            Current = new VoiceFrame
            {
                pitchHz = pitch,
                loudness = Mathf.Clamp01(smoothedLoudness * settings.loudnessGain),
                voiced = voiced,
                latencyMs = frameMs * 0.5f + hopMs + dspMs * 2f
            };

            signals?.PublishVoice(Current);
        }

        static float EstimatePitchAutocorrelation(float[] x, int sampleRate, float minHz, float maxHz)
        {
            var minLag = Mathf.RoundToInt(sampleRate / maxHz);
            var maxLag = Mathf.RoundToInt(sampleRate / minHz);
            if (maxLag >= x.Length - 1)
                maxLag = x.Length - 2;
            if (minLag < 1)
                minLag = 1;

            var bestCorr = 0f;
            var bestLag = minLag;
            for (var lag = minLag; lag <= maxLag; lag++)
            {
                var sum = 0f;
                for (var i = 0; i < x.Length - lag; i++)
                    sum += x[i] * x[i + lag];
                if (sum > bestCorr)
                {
                    bestCorr = sum;
                    bestLag = lag;
                }
            }

            return bestCorr > 1e-6f ? sampleRate / (float)bestLag : 0f;
        }
    }
}
