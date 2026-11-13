using System;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// The last audio filter on the AudioListener: when <see cref="Silence"/> is set it clears the final mix, so
    /// Unity's own output device (the headset over Link, or the Windows default) plays nothing. The filters before
    /// it still get the full mix: <see cref="PcAudioMonitor"/> (the copy sent to the PC device, which is then the
    /// only place the sound comes out) and <see cref="ListenerOutputTap"/> (output.wav).
    /// Filters run in component order, so the silencer is put back at the end whenever another filter is added
    /// (<see cref="KeepLast"/>).
    /// </summary>
    public sealed class ListenerOutputSilencer : MonoBehaviour
    {
        /// <summary>Written on the main thread (PcAudioMonitor), read on the audio thread.</summary>
        public static volatile bool Silence;

        volatile bool retired;

        /// <summary>
        /// Make the silencer the last script on the listener object. <paramref name="createIfMissing"/> false
        /// only moves an existing one.
        /// </summary>
        public static void KeepLast(GameObject listenerObject, bool createIfMissing)
        {
            if (listenerObject == null)
                return;

            var behaviours = listenerObject.GetComponents<MonoBehaviour>();
            ListenerOutputSilencer current = null;
            foreach (var behaviour in behaviours)
            {
                if (behaviour is ListenerOutputSilencer silencer && !silencer.retired)
                    current = silencer;
            }

            if (current == null && !createIfMissing)
                return;
            if (current != null && behaviours[behaviours.Length - 1] == current)
                return;

            if (current != null)
            {
                // Components cannot be reordered at run time: retire this one (it stops clearing at once) and add a new one at the end.
                current.retired = true;
                current.enabled = false;
                Destroy(current);
            }

            listenerObject.AddComponent<ListenerOutputSilencer>();
        }

        /// <summary>True when a silencer is the last script on the listener object (so every other filter gets the full mix).</summary>
        public static bool IsLast(GameObject listenerObject)
        {
            if (listenerObject == null)
                return false;
            var behaviours = listenerObject.GetComponents<MonoBehaviour>();
            return behaviours.Length > 0 && behaviours[behaviours.Length - 1] is ListenerOutputSilencer silencer && !silencer.retired;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (Silence && !retired)
                Array.Clear(data, 0, data.Length);
        }
    }
}
