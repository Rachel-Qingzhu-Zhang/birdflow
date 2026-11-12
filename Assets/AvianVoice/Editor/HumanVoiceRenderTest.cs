#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using AvianVoice.Audio;
using AvianVoice.Study;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Renders the human feedback voice (<see cref="HumanVoiceSynth"/>) to WAV files without Play mode, by the
    /// same code as the live voice, so that it can be listened to and measured outside the headset:
    /// <c>StudyData/diagnostics/human_voice_&lt;time&gt;/</c> gets the listening files (the voice as the
    /// participant hears it) and <c>measure/</c> the same notes with jitter, shimmer, drift and vibrato off
    /// (exactly periodic, for spectrum measurements).
    /// </summary>
    static class HumanVoiceRenderTest
    {
        const int Rate = 48000;
        const int Block = 256;

        [MenuItem("Avian Voice/Embodiment/Debug/Render Human Voice Test Files (WAV)")]
        static void Render()
        {
            if (Application.isPlaying)
            {
                // In Play mode the synth component would also be driven by the audio thread.
                Debug.LogWarning("[AvianVoice] Render Human Voice Test Files works in Edit mode: leave Play mode first.");
                return;
            }

            var dir = Path.Combine(StudyRecorder.RootDirectory, "diagnostics", "human_voice_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(Path.Combine(dir, "measure"));
            var report = new StringBuilder("[AvianVoice] Human voice test files: " + dir + "\n");

            // Listening files: the voice as it is heard (micro-variation and vibrato on).
            Note(dir, "01_guide_note_330Hz", report, false, 1.5f, t => 330f, t => 0.85f, guideNote: true);
            Note(dir, "02_soft_220Hz", report, false, 1.5f, t => 220f, t => 0.1f);
            Note(dir, "03_neutral_220Hz", report, false, 1.5f, t => 220f, t => 0.36f);
            Note(dir, "04_loud_220Hz", report, false, 1.5f, t => 220f, t => 1f);
            Note(dir, "05_crescendo_decrescendo_220Hz", report, false, 4f, t => 220f, t => Mathf.Lerp(0.1f, 1f, 1f - Mathf.Abs(t / 2f - 1f)));
            Note(dir, "06_neutral_110Hz", report, false, 1.5f, t => 110f, t => 0.36f);
            Note(dir, "07_neutral_440Hz", report, false, 1.5f, t => 440f, t => 0.36f);
            Note(dir, "08_glide_165_to_330Hz", report, false, 2f, t => 165f * Mathf.Pow(2f, Mathf.Clamp01(t / 2f)), t => 0.36f);

            // Measurement files: exactly periodic, one per pitch and effort (soft / neutral / loud) plus the guide note.
            foreach (var hz in new[] { 110f, 165f, 220f, 330f, 440f, 660f })
            {
                var pitch = hz;
                Note(dir, "measure/guide_" + (int)hz + "Hz", report, true, 1f, t => pitch, t => 0.85f, guideNote: true);
                Note(dir, "measure/soft_" + (int)hz + "Hz", report, true, 1f, t => pitch, t => 0.1f);
                Note(dir, "measure/neutral_" + (int)hz + "Hz", report, true, 1f, t => pitch, t => 0.36f);
                Note(dir, "measure/loud_" + (int)hz + "Hz", report, true, 1f, t => pitch, t => 1f);
            }

            Debug.Log(report.ToString());
            EditorUtility.RevealInFinder(dir);
        }

        static void Note(string dir, string name, StringBuilder report, bool periodic, float seconds,
            Func<float, float> hzAt, Func<float, float> loudnessAt, bool guideNote = false)
        {
            var go = new GameObject("HumanVoiceRenderTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.AddComponent<AudioSource>();
                var synth = go.AddComponent<HumanVoiceSynth>();
                if (periodic)
                {
                    var so = new SerializedObject(synth);
                    foreach (var field in new[] { "jitterPercent", "shimmerDb", "driftSemitones", "vibratoDepthSemitones" })
                    {
                        var p = so.FindProperty(field);
                        if (p == null)
                            throw new InvalidOperationException("HumanVoiceSynth has no field '" + field + "'.");
                        p.floatValue = 0f;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // The note, then 0.3 s with the gate closed (the release tail).
                var voicedFrames = Mathf.RoundToInt(seconds * Rate);
                var total = voicedFrames + Mathf.RoundToInt(0.3f * Rate);
                var all = new float[total];
                var block = new float[Block];
                double sumSquares = 0;
                var peak = 0f;
                for (var start = 0; start < total; start += Block)
                {
                    var t = start / (float)Rate;
                    synth.RenderOffline(block, Rate, hzAt(t), loudnessAt(t), start < voicedFrames, guideNote);
                    var count = Mathf.Min(Block, total - start);
                    Array.Copy(block, 0, all, start, count);
                }

                // Level of the steady part (from 0.2 s to the end of the note).
                var from = Mathf.Min(voicedFrames, Mathf.RoundToInt(0.2f * Rate));
                for (var i = from; i < voicedFrames; i++)
                {
                    sumSquares += all[i] * (double)all[i];
                    peak = Mathf.Max(peak, Mathf.Abs(all[i]));
                }

                var rmsDb = 10.0 * Math.Log10(Math.Max(1e-12, sumSquares / Math.Max(1, voicedFrames - from)));
                var path = Path.Combine(dir, name + ".wav");
                WavWriter.WriteFile(path, all, total, Rate, 1);
                report.Append("  ").Append(name).Append(".wav: ").Append(rmsDb.ToString("F1")).Append(" dBFS rms, peak ")
                    .Append(peak.ToString("F2")).Append('\n');
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
