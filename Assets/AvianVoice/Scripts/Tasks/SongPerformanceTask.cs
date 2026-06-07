using System.Collections;
using System.Collections.Generic;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class SongPerformanceTask : MonoBehaviour
    {
        enum SongPhase
        {
            Listen,
            Respond,
            Joining,
            Finale,
            Complete
        }

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        BirdCallLibrary library;

        [SerializeField]
        AudioSource source;

        [SerializeField]
        float[] melodyHz = { 330f, 392f, 440f, 494f };

        [SerializeField]
        GameObject flockBirdPrefab;

        [SerializeField]
        Transform flockAnchor;

        [SerializeField]
        float stepToleranceHz = 50f;

        [SerializeField]
        float listenDuration = 1.25f;

        [SerializeField]
        float matchHoldSeconds = 0.45f;

        [SerializeField]
        float joinPauseSeconds = 0.85f;

        [SerializeField]
        float wrongNoteFeedbackCooldown = 0.9f;

        [SerializeField]
        [Tooltip("Press C to skip the current step (debug).")]
        bool enableDebugCheat = true;

        SongPhase phase = SongPhase.Listen;
        int stepIndex;
        float totalScore;
        float phaseTimer;
        float matchHold;
        float lastWrongFeedbackTime = -10f;
        bool stepAdvanceLocked;
        ParrotMatchVisuals leadVisuals;
        TaskFlowUI taskUi;
        Coroutine flowRoutine;
        readonly List<GameObject> flock = new();

        void OnEnable()
        {
            if (!TaskActivity.IsActive("song"))
                return;

            ResetProgress();
            Debug.Log("[AvianVoice] Song: Listen → hold your pitch → flock echoes. C = debug skip step.");
        }

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
            EnsureAudioAndAnchor();
            EnsureLeadVisuals();
            ResetProgress();
        }

        void OnDisable()
        {
            if (flowRoutine != null)
                StopCoroutine(flowRoutine);
            flowRoutine = null;
            leadVisuals?.ApplyMatch(0f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("song"))
                return;

            if (phase == SongPhase.Complete || phase == SongPhase.Finale || stepAdvanceLocked)
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                if (stepIndex < melodyHz.Length)
                    StartCoroutine(CheatAdvanceRoutine());
                return;
            }

            if (analyzer == null || stepIndex >= melodyHz.Length)
                return;

            if (phase == SongPhase.Listen)
            {
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                    BeginRespondPhase();
                return;
            }

            if (phase != SongPhase.Respond)
                return;

            var v = analyzer.Current;
            if (v.voiced < 0.5f || v.pitchHz <= 0f)
            {
                matchHold = Mathf.Max(0f, matchHold - Time.deltaTime * 1.5f);
                UpdateRespondHud();
                return;
            }

            var target = melodyHz[stepIndex];
            var error = Mathf.Abs(v.pitchHz - target);
            if (error <= stepToleranceHz)
            {
                matchHold += Time.deltaTime;
                leadVisuals?.ApplyMatch(Mathf.Clamp01(matchHold / matchHoldSeconds));
                UpdateRespondHud();
                if (matchHold >= matchHoldSeconds)
                    StartCoroutine(JoinStepRoutine("voice"));
            }
            else
            {
                matchHold = 0f;
                leadVisuals?.ApplyMatch(0f);
                if (Time.time - lastWrongFeedbackTime >= wrongNoteFeedbackCooldown)
                {
                    lastWrongFeedbackTime = Time.time;
                    library?.PlayNearest(source, target * 0.7f, 0.4f, 0f);
                    SetHud($"Not quite — aim for {NoteLabel(target)} ({target:0} Hz), you were {v.pitchHz:0} Hz");
                }
            }
        }

        void ResetProgress()
        {
            if (flowRoutine != null)
                StopCoroutine(flowRoutine);

            stepIndex = 0;
            totalScore = 0f;
            phase = SongPhase.Listen;
            phaseTimer = listenDuration;
            matchHold = 0f;
            stepAdvanceLocked = false;

            foreach (var bird in flock)
            {
                if (bird != null)
                    Destroy(bird);
            }
            flock.Clear();
            EnsureAudioAndAnchor();
            BeginListenPhase();
        }

        void BeginListenPhase()
        {
            if (stepIndex >= melodyHz.Length)
            {
                phase = SongPhase.Complete;
                SetHud("Sequence complete!");
                return;
            }

            phase = SongPhase.Listen;
            phaseTimer = listenDuration;
            matchHold = 0f;
            PlayCurrentTarget();
            SetHud($"● Listen: {NoteLabel(melodyHz[stepIndex])} ({stepIndex + 1}/{melodyHz.Length})");
        }

        void BeginRespondPhase()
        {
            phase = SongPhase.Respond;
            matchHold = 0f;
            SetHud($"○ Your turn: hold {NoteLabel(melodyHz[stepIndex])} ~{melodyHz[stepIndex]:0} Hz");
        }

        void UpdateRespondHud()
        {
            if (phase != SongPhase.Respond)
                return;

            var pct = Mathf.Clamp01(matchHold / matchHoldSeconds);
            var target = melodyHz[stepIndex];
            SetHud($"○ Hold {NoteLabel(target)} — {pct * 100f:0}% (need steady pitch)");
        }

        IEnumerator JoinStepRoutine(string source)
        {
            stepAdvanceLocked = true;
            phase = SongPhase.Joining;
            var matchedHz = melodyHz[stepIndex];
            var memberIndex = stepIndex;

            totalScore += 1f;
            stepIndex++;
            taskManager?.ReportScore(totalScore);

            leadVisuals?.PulsePerfect();
            SpawnFlockMember(memberIndex, matchedHz);
            SetHud($"● Flock joins on {NoteLabel(matchedHz)}! ({source})");

            yield return new WaitForSeconds(joinPauseSeconds);

            stepAdvanceLocked = false;
            if (stepIndex < melodyHz.Length)
                BeginListenPhase();
            else
                flowRoutine = StartCoroutine(FinaleChorusRoutine());
        }

        IEnumerator CheatAdvanceRoutine()
        {
            matchHold = matchHoldSeconds;
            yield return JoinStepRoutine("cheat");
        }

        IEnumerator FinaleChorusRoutine()
        {
            phase = SongPhase.Finale;
            stepAdvanceLocked = true;
            SetHud("Finale: flock chorus…");

            for (var i = 0; i < melodyHz.Length; i++)
            {
                library?.PlayNearest(source, melodyHz[i], 1f, 0f);
                for (var j = 0; j < flock.Count; j++)
                {
                    if (flock[j] == null)
                        continue;
                    var member = flock[j].GetComponent<FlockMemberBehaviour>();
                    member?.PlayAnswerCall(library, 0.08f + j * 0.12f);
                }
                yield return new WaitForSeconds(0.6f);
            }

            phase = SongPhase.Complete;
            stepAdvanceLocked = false;
            SetHud($"Chorus complete! Score {totalScore:0}");
            Debug.Log("[AvianVoice] Song Performance complete — flock chorus finished.");
        }

        void PlayCurrentTarget()
        {
            if (stepIndex < melodyHz.Length)
                library?.PlayNearest(source, melodyHz[stepIndex], 1f, 0f);
        }

        void SpawnFlockMember(int memberIndex, float noteHz)
        {
            if (flockAnchor == null)
                return;

            var parrot = WorldPlacedBird.FindParrotInScene();
            var center = parrot != null ? parrot : flockAnchor;
            var color = FlockMemberBehaviour.ColorForNoteIndex(memberIndex);

            GameObject bird;
            if (flockBirdPrefab != null)
                bird = Instantiate(flockBirdPrefab, flockAnchor.position, Quaternion.identity, flockAnchor);
            else
            {
                bird = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bird.transform.SetParent(flockAnchor, false);
                bird.transform.localScale = Vector3.one * 0.28f;
                var col = bird.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;
            }

            var spread = memberIndex * 28f - 42f;
            var offset = Quaternion.Euler(0f, spread, 0f) * Vector3.forward * (1.1f + memberIndex * 0.15f) + Vector3.up * 0.45f;
            bird.transform.position = center.position + center.TransformDirection(offset);

            var behaviour = bird.GetComponent<FlockMemberBehaviour>();
            if (behaviour == null)
                behaviour = bird.AddComponent<FlockMemberBehaviour>();
            behaviour.Initialize(flockAnchor, noteHz, library, memberIndex, color);
            behaviour.PlayAnswerCall(library, 0.25f);
            flock.Add(bird);
        }

        void EnsureAudioAndAnchor()
        {
            var parrot = WorldPlacedBird.FindParrotInScene();
            if (flockAnchor == null)
            {
                var anchor = new GameObject("FlockAnchor");
                flockAnchor = anchor.transform;
            }

            if (parrot != null)
            {
                flockAnchor.position = parrot.position;
                flockAnchor.rotation = parrot.rotation;
            }
            else
            {
                flockAnchor.SetParent(transform, false);
                flockAnchor.localPosition = Vector3.zero;
            }

            if (source == null)
            {
                var host = parrot != null ? parrot.gameObject : gameObject;
                source = host.GetComponent<AudioSource>();
                if (source == null)
                    source = host.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 0.65f;
            source.volume = 1f;
        }

        void EnsureLeadVisuals()
        {
            if (leadVisuals != null)
                return;

            var parrot = WorldPlacedBird.FindParrotInScene();
            var host = parrot != null ? parrot.gameObject : gameObject;
            leadVisuals = host.GetComponent<ParrotMatchVisuals>();
            if (leadVisuals == null)
                leadVisuals = host.AddComponent<ParrotMatchVisuals>();
            leadVisuals.BindFromScene();
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }

        static string NoteLabel(float hz)
        {
            if (hz < 10f)
                return "?";
            var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(hz / 440f, 2f));
            var note = names[((midi % 12) + 12) % 12];
            var octave = midi / 12 - 1;
            return note + octave;
        }
    }
}
