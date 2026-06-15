using AvianVoice.Audio;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Makes the friend bird visibly and audibly teach the breathing rhythm.
    /// </summary>
    [DefaultExecutionOrder(31999)]
    public class FriendTeachingCue : MonoBehaviour
    {
        [SerializeField]
        float chestIndicatorMinDiameter = 0.18f;

        [SerializeField]
        float chestIndicatorMaxDiameter = 0.55f;

        [SerializeField]
        float inhaleChirpHz = 520f;

        [SerializeField]
        float exhaleChirpHz = 380f;

        BirdFriendDemonstrator demonstrator;
        AudioSource source;
        BirdCallLibrary library;
        GameObject chestIndicator;
        Transform chestAnchor;
        bool subscribed;

        void Awake()
        {
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            source = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            CacheChestAnchor();
            EnsureChestIndicator();
            ResolveLibrary();
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

            if (chestIndicator != null)
                chestIndicator.SetActive(false);
        }

        void OnDestroy()
        {
            if (chestIndicator != null)
                Destroy(chestIndicator);
        }

        void LateUpdate()
        {
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            ResolveLibrary();
            TrySubscribe();
            UpdateChestIndicator();
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

        void UpdateChestIndicator()
        {
            if (chestIndicator == null || demonstrator == null || chestAnchor == null)
                return;

            chestIndicator.SetActive(true);
            chestIndicator.transform.position = chestAnchor.position + chestAnchor.up * 0.05f;
            var depth = Mathf.Pow(Mathf.Clamp01(demonstrator.CurrentDepth), 0.65f);
            var diameter = Mathf.Lerp(chestIndicatorMinDiameter, chestIndicatorMaxDiameter, depth);
            chestIndicator.transform.localScale = Vector3.one * diameter;
        }

        void CacheChestAnchor()
        {
            if (ParrotEmbodimentWiring.TryResolve(transform, out var refs) && refs.chest != null)
                chestAnchor = refs.chest;
            else
                chestAnchor = transform;
        }

        void EnsureChestIndicator()
        {
            if (chestIndicator != null)
                return;

            chestIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chestIndicator.name = "FriendBreathCue";
            Destroy(chestIndicator.GetComponent<Collider>());
            chestIndicator.transform.SetParent(null, true);

            var renderer = chestIndicator.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            var mat = new Material(shader);
            mat.color = new Color(1f, 0.55f, 0.12f, 0.55f);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            renderer.material = mat;
            chestIndicator.SetActive(false);
        }
    }
}
