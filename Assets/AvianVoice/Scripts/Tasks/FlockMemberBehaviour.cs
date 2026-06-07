using AvianVoice.Audio;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// A flock bird that orbits the lead parrot, tints by note, and echoes the matched pitch.
    /// </summary>
    public class FlockMemberBehaviour : MonoBehaviour
    {
        [SerializeField]
        float orbitRadius = 1.4f;

        [SerializeField]
        float orbitSpeed = 45f;

        [SerializeField]
        float bobAmplitude = 0.12f;

        Transform orbitCenter;
        float orbitAngle;
        float bobPhase;
        float pitchHz;
        AudioSource chirpSource;
        Renderer bodyRenderer;
        MaterialPropertyBlock colorBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public void Initialize(
            Transform center,
            float noteHz,
            BirdCallLibrary library,
            int memberIndex,
            Color noteColor)
        {
            orbitCenter = center;
            pitchHz = noteHz;
            orbitAngle = memberIndex * 72f + Random.Range(-8f, 8f);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);

            bodyRenderer = GetComponentInChildren<Renderer>();
            ApplyColor(noteColor);

            chirpSource = gameObject.AddComponent<AudioSource>();
            chirpSource.playOnAwake = false;
            chirpSource.spatialBlend = 0.75f;
            chirpSource.volume = 0.85f;
            chirpSource.minDistance = 0.5f;
            chirpSource.maxDistance = 18f;

            if (library != null)
                library.PlayNearest(chirpSource, pitchHz, 0.9f, null);
        }

        void Update()
        {
            if (orbitCenter == null)
                return;

            orbitAngle += orbitSpeed * Time.deltaTime;
            var rad = orbitAngle * Mathf.Deg2Rad;
            var bob = Mathf.Sin(Time.time * 2.2f + bobPhase) * bobAmplitude;
            var offset = new Vector3(Mathf.Cos(rad) * orbitRadius, 0.35f + bob, Mathf.Sin(rad) * orbitRadius);
            transform.position = orbitCenter.position + orbitCenter.TransformDirection(offset);
            transform.LookAt(orbitCenter.position + Vector3.up * 0.4f);
        }

        public void PlayAnswerCall(BirdCallLibrary library, float delaySeconds)
        {
            if (library == null || chirpSource == null)
                return;
            Invoke(nameof(PlayEcho), delaySeconds);
        }

        void PlayEcho()
        {
            if (chirpSource != null && pitchHz > 0f)
            {
                var lib = FindAnyObjectByType<BirdCallLibrary>();
                lib?.PlayNearest(chirpSource, pitchHz, 0.75f, null);
            }
        }

        void ApplyColor(Color c)
        {
            if (bodyRenderer == null)
                return;

            colorBlock ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(colorBlock);
            if (bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
                colorBlock.SetColor(BaseColorId, c);
            if (bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(EmissionColorId))
                colorBlock.SetColor(EmissionColorId, c * 1.6f);
            bodyRenderer.SetPropertyBlock(colorBlock);
        }

        public static Color ColorForNoteIndex(int index)
        {
            var palette = new[]
            {
                new Color(0.45f, 0.85f, 1f),
                new Color(0.55f, 1f, 0.65f),
                new Color(1f, 0.85f, 0.45f),
                new Color(0.95f, 0.55f, 1f),
                new Color(1f, 0.55f, 0.55f),
                new Color(0.7f, 0.75f, 1f)
            };
            return palette[Mathf.Clamp(index, 0, palette.Length - 1)];
        }
    }
}
