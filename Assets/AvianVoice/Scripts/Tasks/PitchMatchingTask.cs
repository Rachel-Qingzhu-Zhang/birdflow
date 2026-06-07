using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class PitchMatchingTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        BirdCallLibrary referenceLibrary;

        [SerializeField]
        AudioSource referenceSource;

        [SerializeField]
        float targetPitchHz = 440f;

        [SerializeField]
        float toleranceHz = 40f;

        [SerializeField]
        float referenceInterval = 3f;

        [SerializeField]
        [Tooltip("Press C while this task is active to simulate a perfect pitch match (debug).")]
        bool enableDebugCheat = true;

        ParrotMatchVisuals matchVisuals;
        TaskFlowUI taskUi;
        float timer;
        float score;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (referenceLibrary == null)
                referenceLibrary = FindAnyObjectByType<BirdCallLibrary>();
            EnsureReferenceSource();
            EnsureMatchVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("pitch"))
                return;

            timer = 0f;
            score = 0f;
            EnsureReferenceSource();
            EnsureMatchVisuals();
            PlayReference();
            SetHud($"Match ~{targetPitchHz:0} Hz | C=debug perfect glow");
            Debug.Log("[AvianVoice] Pitch Matching: hum to match reference. C = cheat (glow + pulse light).");
        }

        void OnDisable()
        {
            matchVisuals?.ApplyMatch(0f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("pitch"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                score = 1f;
                taskManager?.ReportScore(score);
                EnsureMatchVisuals();
                matchVisuals?.PulsePerfect();
                SetHud($"Perfect match! ({targetPitchHz:0} Hz) — cheat");
                Debug.Log("[AvianVoice] Pitch Matching cheat: glow + pulse light at " + targetPitchHz + " Hz.");
                return;
            }

            timer += Time.deltaTime;
            if (timer >= referenceInterval)
            {
                timer = 0f;
                PlayReference();
            }

            if (analyzer == null)
                return;

            var v = analyzer.Current;
            if (v.voiced < 0.5f || v.pitchHz <= 0f)
            {
                matchVisuals?.ApplyMatch(Mathf.Lerp(score, 0f, Time.deltaTime * 3f));
                return;
            }

            var error = Mathf.Abs(v.pitchHz - targetPitchHz);
            var match = Mathf.Clamp01(1f - error / toleranceHz);
            score = Mathf.Lerp(score, match, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            matchVisuals?.ApplyMatch(match);

            if (match > 0.85f)
                SetHud($"Locked in! {v.pitchHz:0} Hz (target {targetPitchHz:0})");
            else
                SetHud($"Matching… {v.pitchHz:0} / {targetPitchHz:0} Hz");
        }

        void EnsureMatchVisuals()
        {
            if (matchVisuals != null)
                return;

            var parrot = WorldPlacedBird.FindParrotInScene();
            var host = parrot != null ? parrot.gameObject : gameObject;
            matchVisuals = host.GetComponent<ParrotMatchVisuals>();
            if (matchVisuals == null)
                matchVisuals = host.AddComponent<ParrotMatchVisuals>();
            matchVisuals.BindFromScene();
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }

        void EnsureReferenceSource()
        {
            if (referenceSource != null)
                return;

            var parrot = WorldPlacedBird.FindParrotInScene();
            var host = parrot != null ? parrot.gameObject : gameObject;
            referenceSource = host.GetComponent<AudioSource>();
            if (referenceSource == null)
                referenceSource = host.AddComponent<AudioSource>();
            referenceSource.playOnAwake = false;
            referenceSource.spatialBlend = 0.65f;
            referenceSource.volume = 1f;
            referenceSource.minDistance = 1f;
            referenceSource.maxDistance = 25f;
        }

        void PlayReference()
        {
            referenceLibrary?.PlayNearest(referenceSource, targetPitchHz, 1f, 0f);
        }
    }
}
