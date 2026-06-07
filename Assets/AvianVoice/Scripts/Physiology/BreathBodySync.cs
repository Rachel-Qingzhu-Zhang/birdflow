using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using UnityEngine;

namespace AvianVoice.Physiology
{
    [DefaultExecutionOrder(31000)]
    public class BreathBodySync : MonoBehaviour
    {
        AvianSignals signals;
        VoiceAnalyzer analyzer;
        BreathSimulator breath;
        BirdBodyController body;
        BeakVocalFX beakFx;
        WingHandMapper wings;

        public void Initialize(
            AvianSignals avianSignals,
            VoiceAnalyzer voiceAnalyzer,
            BreathSimulator breathSimulator,
            BirdBodyController bodyController,
            BeakVocalFX beakVocalFx,
            WingHandMapper wingMapper)
        {
            signals = avianSignals;
            analyzer = voiceAnalyzer;
            breath = breathSimulator;
            body = bodyController;
            beakFx = beakVocalFx;
            wings = wingMapper;
        }

        void LateUpdate()
        {
            if (analyzer == null || breath == null || body == null)
                return;

            var voice = analyzer.Current;
            var breathFrame = breath.Current;
            body.Apply(voice, breathFrame);
            wings?.ApplyBreath(breathFrame);
            beakFx?.Apply(voice);
        }
    }
}
