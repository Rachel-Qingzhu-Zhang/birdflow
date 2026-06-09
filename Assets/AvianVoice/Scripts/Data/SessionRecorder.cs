using System.IO;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;

namespace AvianVoice.Data
{
    public class SessionRecorder : MonoBehaviour
    {
        AvianVoiceSettings settings;
        AvianSignals signals;
        VoiceAnalyzer analyzer;
        BreathSimulator breath;
        BirdVoiceMapper mapper;

        float sampleTimer;
        string logPath;

        public void Initialize(
            AvianVoiceSettings voiceSettings,
            AvianSignals avianSignals,
            VoiceAnalyzer voiceAnalyzer,
            BreathSimulator breathSimulator,
            BirdVoiceMapper voiceMapper)
        {
            settings = voiceSettings;
            signals = avianSignals;
            analyzer = voiceAnalyzer;
            breath = breathSimulator;
            mapper = voiceMapper;

            logPath = Path.Combine(Application.persistentDataPath, "avian_trials.jsonl");
            signals.OnTaskScore += OnTaskScore;
        }

        void OnDestroy()
        {
            if (signals != null)
                signals.OnTaskScore -= OnTaskScore;
        }

        void Update()
        {
            if (settings == null)
                return;

            sampleTimer += Time.deltaTime;
            var interval = 1f / Mathf.Max(1f, settings.recorderSampleHz);
            if (sampleTimer < interval)
                return;

            sampleTimer = 0f;
            WriteSample(0f);
        }

        string activeSceneId = "ambient";
        float lastBreathSyncScore;
        string lastBreathPatternId = "";

        public void SetActiveSceneId(string sceneId) => activeSceneId = sceneId ?? "ambient";

        public void SetBreathSyncContext(float syncScore, string patternId)
        {
            lastBreathSyncScore = syncScore;
            lastBreathPatternId = patternId ?? "";
        }

        void OnTaskScore(string taskId, float score)
        {
            WriteSample(score, taskId);
        }

        void WriteSample(float score, string taskId = "ambient")
        {
            if (analyzer == null || breath == null)
                return;

            var v = analyzer.Current;
            var b = breath.Current;
            var entry = new TrialLogEntry
            {
                timestamp = System.DateTime.UtcNow.ToString("o"),
                taskId = taskId,
                voiceMode = mapper != null ? mapper.Mode.ToString() : "Unknown",
                pitchHz = v.pitchHz,
                loudness = v.loudness,
                breathDepth = b.depth,
                score = score,
                latencyMs = v.latencyMs,
                sceneId = activeSceneId,
                breathSyncScore = lastBreathSyncScore,
                breathPatternId = lastBreathPatternId
            };

            var json = JsonUtility.ToJson(entry);
            File.AppendAllText(logPath, json + "\n");
        }
    }
}
