using System.Collections;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Plays the same chirp five times in a row for success feedback.
    /// </summary>
    public class SuccessChirpBurst : MonoBehaviour
    {
        [SerializeField]
        BirdCallLibrary library;

        [SerializeField]
        AudioSource source;

        [SerializeField]
        int burstCount = 5;

        [SerializeField]
        float gapSeconds = 0.05f;

        [SerializeField]
        float referencePitchHz = 440f;

        Coroutine routine;

        void Awake()
        {
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
            if (source == null)
            {
                source = GetComponent<AudioSource>();
                if (source == null)
                    source = gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.volume = 1f;
        }

        public void PlaySuccessBurst()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(PlayBurst());
        }

        IEnumerator PlayBurst()
        {
            for (var i = 0; i < burstCount; i++)
            {
                library?.PlayNearest(source, referencePitchHz, 1f, 0f);
                yield return new WaitForSeconds(0.18f + gapSeconds);
            }
            routine = null;
        }
    }
}
