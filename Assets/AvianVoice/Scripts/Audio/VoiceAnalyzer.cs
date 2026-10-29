using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Microphone loudness + pitch. Pitch uses YIN (de Cheveigne and Kawahara, 2002) with a confidence value;
    /// low-confidence frames hold the last good pitch, and pitch is smoothed in semitones.
    /// </summary>
    public sealed class VoiceAnalyzer : MonoBehaviour
    {
        AvianVoiceSettings settings;
        AvianSignals signals;
        float[] analysisBuffer;
        float[] yinBuffer;
        float smoothedLoudness;
        float smoothedSemitones;
        float heldPitchHz;
        float heldPitchAge;
        float lastPushTime;

        public VoiceFrame Current { get; private set; }

        void Update()
        {
            if (Current.loudness <= 0f && Current.voiced <= 0f)
                return;
            if (Time.time - lastPushTime < 0.2f)
                return;

            Current = default;
            signals?.PublishVoice(Current);
        }

        public void Initialize(AvianVoiceSettings voiceSettings, AvianSignals avianSignals)
        {
            settings = voiceSettings;
            signals = avianSignals;
            analysisBuffer = new float[settings.frameSize];
            yinBuffer = new float[settings.frameSize / 2 + 1];
        }

        public void PushSamples(float[] samples, int count)
        {
            if (settings == null || count <= 0)
                return;

            lastPushTime = Time.time;
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

            var voiced = smoothedLoudness > settings.voicedThreshold ? 1f : 0f;
            var rawPitch = EstimatePitchYin(
                analysisBuffer,
                copyCount,
                yinBuffer,
                settings.sampleRate,
                settings.minPitchHz,
                settings.maxPitchHz,
                settings.yinThreshold,
                out var confidence);

            var pitch = TrackPitch(rawPitch, confidence, voiced > 0.5f, dt);

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
                latencyMs = frameMs * 0.5f + hopMs + dspMs * 2f,
                pitchConfidence = confidence
            };

            signals?.PublishVoice(Current);
        }

        float TrackPitch(float rawPitch, float confidence, bool voiced, float dt)
        {
            var confident = voiced && rawPitch > 0f && confidence >= settings.pitchMinConfidence;
            if (!confident)
            {
                heldPitchAge += dt;
                if (heldPitchAge > settings.pitchHoldSeconds)
                    heldPitchHz = 0f;
                return heldPitchHz;
            }

            heldPitchAge = 0f;
            var semitones = 12f * Mathf.Log(rawPitch / 440f, 2f);
            if (heldPitchHz <= 0f || Mathf.Abs(semitones - smoothedSemitones) > settings.pitchSnapSemitones)
            {
                smoothedSemitones = semitones;
            }
            else
            {
                var k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, settings.pitchSmoothingSeconds));
                smoothedSemitones = Mathf.Lerp(smoothedSemitones, semitones, k);
            }

            heldPitchHz = 440f * Mathf.Pow(2f, smoothedSemitones / 12f);
            return heldPitchHz;
        }

        /// <summary>
        /// YIN: difference function, cumulative mean normalization, absolute threshold,
        /// parabolic interpolation. Returns 0 Hz when no period is found.
        /// </summary>
        static float EstimatePitchYin(
            float[] x,
            int length,
            float[] yin,
            int sampleRate,
            float minHz,
            float maxHz,
            float threshold,
            out float confidence)
        {
            confidence = 0f;
            var window = length / 2;
            var minLag = Mathf.Max(2, Mathf.FloorToInt(sampleRate / Mathf.Max(1f, maxHz)));
            var maxLag = Mathf.Min(window, Mathf.CeilToInt(sampleRate / Mathf.Max(1f, minHz)));
            if (maxLag >= yin.Length)
                maxLag = yin.Length - 1;
            if (maxLag <= minLag + 1)
                return 0f;

            yin[0] = 1f;
            var runningSum = 0f;
            for (var tau = 1; tau <= maxLag; tau++)
            {
                var sum = 0f;
                for (var j = 0; j < window; j++)
                {
                    var delta = x[j] - x[j + tau];
                    sum += delta * delta;
                }

                runningSum += sum;
                yin[tau] = runningSum > 1e-12f ? sum * tau / runningSum : 1f;
            }

            var bestTau = -1;
            for (var tau = minLag; tau <= maxLag; tau++)
            {
                if (yin[tau] >= threshold)
                    continue;

                while (tau + 1 <= maxLag && yin[tau + 1] < yin[tau])
                    tau++;
                bestTau = tau;
                break;
            }

            if (bestTau < 0)
            {
                // No dip under the threshold: report the global minimum with its (low) confidence.
                var minValue = float.MaxValue;
                for (var tau = minLag; tau <= maxLag; tau++)
                {
                    if (yin[tau] < minValue)
                    {
                        minValue = yin[tau];
                        bestTau = tau;
                    }
                }

                if (bestTau < 0)
                    return 0f;
            }

            confidence = Mathf.Clamp01(1f - yin[bestTau]);

            var refinedTau = (float)bestTau;
            if (bestTau > 1 && bestTau < maxLag)
            {
                var s0 = yin[bestTau - 1];
                var s1 = yin[bestTau];
                var s2 = yin[bestTau + 1];
                var denom = s0 + s2 - 2f * s1;
                if (Mathf.Abs(denom) > 1e-9f)
                    refinedTau = bestTau + 0.5f * (s0 - s2) / denom;
            }

            return refinedTau > 0f ? sampleRate / refinedTau : 0f;
        }
    }
}
