using AvianVoice.Audio;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Makes the friend bird audibly teach the breathing rhythm.
    /// </summary>
    [DefaultExecutionOrder(31999)]
    public class FriendTeachingCue : MonoBehaviour
    {
        [SerializeField]
        float inhaleChirpHz = 520f;

        [SerializeField]
        float exhaleChirpHz = 380f;

        BirdFriendDemonstrator demonstrator;
        AudioSource source;
        BirdCallLibrary library;
        bool subscribed;

        void Awake()
        {
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            if (!TryGetComponent(out source))
                source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            ResolveLibrary();
            StripLegacyChestIndicator(transform);
        }

        void ResolveLibrary()
        {
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
        }

        void OnEnable()
        {
            TrySubscribe();
        }

        void OnDisable()
        {
            if (demonstrator != null && subscribed)
            {
                demonstrator.OnPhaseChanged -= HandlePhaseChanged;
                subscribed = false;
            }
        }

        void LateUpdate()
        {
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            ResolveLibrary();
            TrySubscribe();
        }

        void TrySubscribe()
        {
            if (demonstrator == null || subscribed)
                return;

            demonstrator.OnPhaseChanged += HandlePhaseChanged;
            subscribed = true;
        }

        void HandlePhaseChanged(bool inhaling)
        {
            ResolveLibrary();
            if (library == null)
                return;

            var hz = inhaling ? inhaleChirpHz : exhaleChirpHz;
            library.PlayTeachingCue(source, hz);
        }

        static void StripLegacyChestIndicator(Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            foreach (var child in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "FriendBreathCue")
                    Destroy(child.gameObject);
            }
        }
    }
}
