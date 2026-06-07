using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    public class BeakVocalFX : MonoBehaviour
    {
        Transform beak;
        ParticleSystem particles;
        float emissionSmooth;
        float emitAccumulator;

        public void Initialize(Transform beakTransform, ParticleSystem vocalParticles)
        {
            beak = beakTransform;
            particles = vocalParticles;
        }

        public void Apply(VoiceFrame voice)
        {
            var target = voice.loudness * voice.voiced;
            emissionSmooth = Mathf.Lerp(emissionSmooth, target, 1f - Mathf.Exp(-12f * Time.deltaTime));

            if (particles == null)
                return;

            if (emissionSmooth > 0.2f)
            {
                if (!particles.isPlaying)
                    particles.Play();

                emitAccumulator += emissionSmooth * 40f * Time.deltaTime;
                var count = Mathf.FloorToInt(emitAccumulator);
                if (count > 0)
                {
                    emitAccumulator -= count;
                    var emitParams = new ParticleSystem.EmitParams();
                    if (beak != null)
                        emitParams.position = beak.position;
                    particles.Emit(emitParams, count);
                }
            }
            else if (emissionSmooth < 0.05f && particles.isPlaying)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
