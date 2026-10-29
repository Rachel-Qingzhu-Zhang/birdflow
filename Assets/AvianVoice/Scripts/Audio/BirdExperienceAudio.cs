using System.Collections;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Reference notes/melody and bird-sound rewards loaded from Resources/BirdExperience.
    /// Scene 6 does not call this.
    /// </summary>
    public class BirdExperienceAudio : MonoBehaviour
    {
        const string Folder = "BirdExperience";

        AudioSource referenceSource;
        AudioSource feedbackSource;
        AudioSource cueSource;
        AudioClip pitchClip;
        AudioClip melodyClip;
        AudioClip breathRewardClip;
        AudioClip forestRewardClip;
        Coroutine sliceRoutine;
        float nextForestTime;
        bool replayMelodyAfterReset;
        Transform lastMelodyBird;

        public bool HasPitchClip => pitchClip != null;
        public bool HasMelodyClip => melodyClip != null;
        public bool IsReferencePlaying => referenceSource != null && referenceSource.isPlaying;

        public float ReferenceSecondsRemaining
        {
            get
            {
                if (referenceSource == null || !referenceSource.isPlaying || referenceSource.clip == null)
                    return 0f;
                var pitch = Mathf.Max(0.01f, referenceSource.pitch);
                return Mathf.Max(0f, (referenceSource.clip.length - referenceSource.time) / pitch);
            }
        }

        public static BirdExperienceAudio Ensure()
        {
            var existing = FindAnyObjectByType<BirdExperienceAudio>();
            if (existing != null)
                return existing;

            var host = GameObject.Find("TaskRoot") ?? new GameObject("TaskRoot");
            return host.AddComponent<BirdExperienceAudio>();
        }

        void Awake()
        {
            LoadClips();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        void LoadClips()
        {
            pitchClip = Resources.Load<AudioClip>(Folder + "/bird_pitch");
            melodyClip = Resources.Load<AudioClip>(Folder + "/bird_melody");
            breathRewardClip = Resources.Load<AudioClip>(Folder + "/birds_reward");
            forestRewardClip = Resources.Load<AudioClip>(Folder + "/birds_forest");
            pitchClip?.LoadAudioData();
            melodyClip?.LoadAudioData();
            breathRewardClip?.LoadAudioData();
            forestRewardClip?.LoadAudioData();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!replayMelodyAfterReset || !isActiveAndEnabled)
                return;
            PlayMelodyNear(lastMelodyBird, true);
        }

        void Start()
        {
            EnsureSources();
        }

        void EnsureSources()
        {
            referenceSource = EnsureOne(referenceSource, "BirdReference");
            feedbackSource = EnsureOne(feedbackSource, "BirdFeedback");
            cueSource = EnsureOne(cueSource, "BirdCue");
        }

        AudioSource EnsureOne(AudioSource current, string childName)
        {
            if (current != null)
                return current;

            var created = CreateSource(childName);
            Configure(created);
            return created;
        }

        void PlaceReferenceNear(Transform bird)
        {
            if (referenceSource == null)
                return;

            if (referenceSource.transform.parent != transform)
                referenceSource.transform.SetParent(transform, true);

            if (bird != null)
                referenceSource.transform.position = bird.position + Vector3.up * 0.25f;

            referenceSource.spatialBlend = 0f;
            referenceSource.spatialize = false;
            referenceSource.dopplerLevel = 0f;
            referenceSource.volume = 1f;
        }

        public AudioSource CueSource
        {
            get
            {
                EnsureSources();
                if (cueSource != null)
                {
                    cueSource.spatialBlend = 0f;
                    cueSource.spatialize = false;
                    cueSource.volume = 1f;
                }

                return cueSource;
            }
        }

        public void PlayPitchStep(int step, int stepCount, float pitchRatio)
        {
            PlayPitchStepNear(null, step, stepCount, pitchRatio);
        }

        /// <summary>
        /// Plays one phrase of the pitch clip from the singing bird so it is heard beside the listener.
        /// </summary>
        public void PlayPitchStepNear(Transform bird, int step, int stepCount, float pitchRatio)
        {
            EnsureSources();
            if (pitchClip == null || referenceSource == null)
                return;

            PlaceReferenceNear(bird);
            if (pitchClip.length > 2.8f && stepCount > 1)
                PlaySlice(pitchClip, step, stepCount);
            else
                PlayOneShot(referenceSource, pitchClip, 1f, Mathf.Clamp(pitchRatio, 0.5f, 1.8f));
        }

        public void PlayMelody(bool restart)
        {
            PlayMelodyNear(null, restart);
        }

        public void PlayMelodyNear(Transform bird, bool restart)
        {
            EnsureSources();
            if (melodyClip == null)
                LoadClips();
            if (melodyClip == null || referenceSource == null)
                return;

            lastMelodyBird = bird;
            PlaceReferenceNear(bird);

            if (!restart && referenceSource.isPlaying && referenceSource.clip == melodyClip)
            {
                replayMelodyAfterReset = true;
                return;
            }

            if (sliceRoutine != null)
            {
                StopCoroutine(sliceRoutine);
                sliceRoutine = null;
            }

            referenceSource.clip = melodyClip;
            referenceSource.pitch = 1f;
            referenceSource.volume = 1f;
            referenceSource.mute = false;
            referenceSource.loop = false;
            referenceSource.ignoreListenerPause = true;
            referenceSource.time = 0f;
            referenceSource.Play();
            replayMelodyAfterReset = true;
        }

        public void PlayMelodyStepNear(Transform bird, int step, int stepCount)
        {
            EnsureSources();
            if (melodyClip == null || referenceSource == null)
                return;

            PlaceReferenceNear(bird);
            if (melodyClip.length > 2.8f && stepCount > 1)
                PlaySlice(melodyClip, step, stepCount);
            else
                PlayMelodyNear(bird, true);
        }

        /// <summary>Longer tree-birds bed while a breath pattern is actually matched.</summary>
        public void PlayBreathReward()
        {
            EnsureSources();
            if (breathRewardClip == null || feedbackSource == null)
                return;

            if (feedbackSource.isPlaying && feedbackSource.clip == breathRewardClip)
                return;

            feedbackSource.clip = breathRewardClip;
            feedbackSource.pitch = 1f;
            feedbackSource.volume = 0.42f;
            feedbackSource.loop = false;
            feedbackSource.Play();
        }

        /// <summary>Short forest sting for a completed step. Does not replace scene 6's chirp burst.</summary>
        public bool PlayForestReward()
        {
            EnsureSources();
            if (forestRewardClip == null || feedbackSource == null)
                return false;

            if (Time.time < nextForestTime)
                return true;

            nextForestTime = Time.time + 0.65f;
            feedbackSource.PlayOneShot(forestRewardClip, 0.8f);
            return true;
        }

        public void StopAll()
        {
            if (sliceRoutine != null)
            {
                StopCoroutine(sliceRoutine);
                sliceRoutine = null;
            }

            replayMelodyAfterReset = false;
            if (referenceSource != null)
                referenceSource.Stop();
            if (feedbackSource != null)
                feedbackSource.Stop();
        }

        void PlaySlice(AudioClip clip, int step, int stepCount)
        {
            if (referenceSource == null || clip == null || stepCount < 1)
                return;

            if (sliceRoutine != null)
                StopCoroutine(sliceRoutine);

            var slice = clip.length / stepCount;
            var start = Mathf.Clamp(step, 0, stepCount - 1) * slice;
            referenceSource.clip = clip;
            referenceSource.pitch = 1f;
            referenceSource.volume = 1f;
            referenceSource.loop = false;
            referenceSource.time = Mathf.Clamp(start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            referenceSource.Play();
            sliceRoutine = StartCoroutine(StopReferenceAfter(slice * 0.92f));
        }

        IEnumerator StopReferenceAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.15f, seconds));
            if (referenceSource != null)
                referenceSource.Stop();
            sliceRoutine = null;
        }

        static void PlayOneShot(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (source == null || clip == null)
                return;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        AudioSource CreateSource(string childName)
        {
            var child = transform.Find(childName);
            GameObject go;
            if (child != null)
                go = child.gameObject;
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(transform, false);
            }

            var source = go.GetComponent<AudioSource>();
            if (source == null)
                source = go.AddComponent<AudioSource>();
            return source;
        }

        static void Configure(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.volume = 1f;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.mute = false;
            source.ignoreListenerPause = true;
        }
    }
}
