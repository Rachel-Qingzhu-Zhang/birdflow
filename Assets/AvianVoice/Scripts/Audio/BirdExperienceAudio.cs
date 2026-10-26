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
        AudioClip pitchClip;
        AudioClip melodyClip;
        AudioClip breathRewardClip;
        AudioClip forestRewardClip;
        Coroutine sliceRoutine;
        float nextForestTime;

        public bool HasPitchClip => pitchClip != null;
        public bool HasMelodyClip => melodyClip != null;

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
            pitchClip = Resources.Load<AudioClip>(Folder + "/bird_pitch");
            melodyClip = Resources.Load<AudioClip>(Folder + "/bird_melody");
            breathRewardClip = Resources.Load<AudioClip>(Folder + "/birds_reward");
            forestRewardClip = Resources.Load<AudioClip>(Folder + "/birds_forest");
            pitchClip?.LoadAudioData();
            melodyClip?.LoadAudioData();
            breathRewardClip?.LoadAudioData();
            forestRewardClip?.LoadAudioData();
            referenceSource = CreateSource("BirdReference");
            feedbackSource = CreateSource("BirdFeedback");
        }

        public void PlayPitchStep(int step, int stepCount, float pitchRatio)
        {
            if (pitchClip == null)
                return;

            if (pitchClip.length > 2.8f && stepCount > 1)
                PlaySlice(pitchClip, step, stepCount);
            else
                PlayOneShot(referenceSource, pitchClip, 0.9f, Mathf.Clamp(pitchRatio, 0.5f, 1.8f));
        }

        public void PlayMelody(bool restart)
        {
            if (melodyClip == null || referenceSource == null)
                return;

            if (!restart && referenceSource.isPlaying && referenceSource.clip == melodyClip)
                return;

            if (sliceRoutine != null)
            {
                StopCoroutine(sliceRoutine);
                sliceRoutine = null;
            }

            referenceSource.clip = melodyClip;
            referenceSource.pitch = 1f;
            referenceSource.volume = 0.85f;
            referenceSource.loop = false;
            referenceSource.time = 0f;
            referenceSource.Play();
        }

        /// <summary>Longer tree-birds bed while a breath pattern is actually matched.</summary>
        public void PlayBreathReward()
        {
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
            referenceSource.volume = 0.9f;
            referenceSource.loop = false;
            referenceSource.Play();
            referenceSource.time = Mathf.Clamp(start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            sliceRoutine = StartCoroutine(StopReferenceAfter(slice * 0.92f));
        }

        IEnumerator StopReferenceAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.15f, seconds));
            if (referenceSource != null && referenceSource.clip == pitchClip)
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

            var source = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.15f;
            source.loop = false;
            return source;
        }
    }
}
