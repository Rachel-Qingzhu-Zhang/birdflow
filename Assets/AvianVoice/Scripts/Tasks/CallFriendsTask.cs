using System.Collections.Generic;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class CallFriendsTask : MonoBehaviour
    {
        enum SummonStage
        {
            None,
            OneBird,
            ThreeBirds,
            Flock
        }

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        BreathSimulator breath;

        [SerializeField]
        BirdCallLibrary library;

        [SerializeField]
        AudioSource source;

        [SerializeField]
        SuccessChirpBurst successChirp;

        [SerializeField]
        GameObject flockBirdPrefab;

        [SerializeField]
        float flockScale = FlockParrotBehaviour.DefaultFlockScale;

        [SerializeField]
        float[] melodyHz = { 330f, 392f };

        [SerializeField]
        float toleranceHz = 40f;

        [SerializeField]
        float matchHoldSeconds = 0.45f;

        [SerializeField]
        float breathSyncMin = 0.55f;

        [SerializeField]
        bool enableDebugCheat = true;

        BirdFriendDemonstrator demonstrator;
        ParrotMatchVisuals friendVisuals;
        TaskFlowUI taskUi;
        readonly List<GameObject> summoned = new();
        int stepIndex;
        float matchHold;
        float score;
        SummonStage stage = SummonStage.None;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            EnsureSource();
            EnsureFriendVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("call_friends"))
                return;

            ClearSummoned();
            stepIndex = 0;
            matchHold = 0f;
            score = 0f;
            stage = SummonStage.None;
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            PlayReference();
            SetHud("Call friends: sing with Pattern A breath.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            ClearSummoned();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("call_friends"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceSummon(1f);
                return;
            }

            if (analyzer == null || breath == null || demonstrator == null)
                return;

            if (stepIndex >= melodyHz.Length && stage == SummonStage.Flock)
            {
                SetHud("Flock gathered! Friends say goodbye soon — press 6 for Reflection.");
                return;
            }

            var target = melodyHz[Mathf.Min(stepIndex, melodyHz.Length - 1)];
            var v = analyzer.Current;
            var pitchMatch = 0f;
            if (v.voiced > 0.5f && v.pitchHz > 0f)
            {
                var error = Mathf.Abs(v.pitchHz - target);
                pitchMatch = Mathf.Clamp01(1f - error / toleranceHz);
            }

            var breathSync = BreathSyncEvaluator.Evaluate(breath.Current, demonstrator);
            var combined = pitchMatch * 0.6f + breathSync * 0.4f;
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(pitchMatch);
            SessionScoreCache.RecordSynchronization(breathSync);
            friendVisuals?.ApplyMatch(combined);

            if (pitchMatch > 0.7f && breathSync >= breathSyncMin)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceSummon(combined);
            else
                SetHud($"Call {NoteLabel(target)} + breath sync {breathSync * 100f:0}%");
        }

        void AdvanceSummon(float stepScore)
        {
            matchHold = 0f;
            successChirp?.PlaySuccessBurst();
            friendVisuals?.PulsePerfect();

            stepIndex++;
            if (stepIndex == 1)
            {
                stage = SummonStage.OneBird;
                SpawnBird(0f);
            }
            else if (stepIndex == 2)
            {
                stage = SummonStage.ThreeBirds;
                SpawnBird(-0.8f);
                SpawnBird(0.8f);
            }
            else
            {
                stage = SummonStage.Flock;
                for (var i = 0; i < 3; i++)
                    SpawnBird(Random.Range(-1.2f, 1.2f));
                SetHud("Flock arrived!");
            }

            if (stepIndex < melodyHz.Length)
                PlayReference();

            taskManager?.ReportScore(stepScore);
        }

        void SpawnBird(float lateralOffset)
        {
            var template = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : transform;
            var bird = CreateFlockParrotInstance();
            if (bird == null)
            {
                BirdSceneRoles.TryGetFlockOrbitCenter(out var center);
                if (center == default && template != null)
                    center = template.position;
                var pos = center + Vector3.right * lateralOffset + Vector3.up * 0.5f;
                bird = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bird.transform.position = pos;
                bird.transform.localScale = Vector3.one * 0.15f;
                var renderer = bird.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        mat.color = new Color(0.4f, 0.9f, 0.5f);
                        renderer.material = mat;
                    }
                }
                Destroy(bird.GetComponent<Collider>());
            }
            else
            {
                bird.name = $"FlockParrot_{summoned.Count + 1}";
                var behaviour = bird.GetComponent<FlockParrotBehaviour>() ?? bird.AddComponent<FlockParrotBehaviour>();
                behaviour.ConfigureScale(flockScale);
                behaviour.Initialize(template, summoned.Count, lateralOffset);
            }

            summoned.Add(bird);
        }

        GameObject CreateFlockParrotInstance()
        {
            if (flockBirdPrefab != null)
                return Instantiate(flockBirdPrefab);

            var template = BirdSceneRoles.Friend;
            if (template == null)
                return null;

            var clone = Instantiate(template.gameObject);
            clone.SetActive(false);
            clone.name = "FlockParrot";
            return clone;
        }

        void ClearSummoned()
        {
            foreach (var go in summoned)
            {
                if (go != null)
                    Destroy(go);
            }
            summoned.Clear();
        }

        void PlayReference()
        {
            if (stepIndex >= melodyHz.Length)
                return;
            library?.PlayNearest(source, melodyHz[stepIndex], 1f, 0f);
        }

        void EnsureSource()
        {
            if (source != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            source = host.GetComponent<AudioSource>() ?? host.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }

        static string NoteLabel(float hz) => hz < 360f ? "E4" : "G4";

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
