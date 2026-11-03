using System;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// On-screen language. J toggles English and Japanese. Default is English.
    /// </summary>
    public static class AvianUiLanguage
    {
        public enum Id
        {
            English,
            Japanese
        }

        static int handledFrame = -1;

        public static Id Current { get; private set; } = Id.English;

        public static bool IsJapanese => Current == Id.Japanese;

        public static event Action Changed;

        public static string Pick(string english, string japanese)
        {
            return IsJapanese && !string.IsNullOrEmpty(japanese) ? japanese : english ?? "";
        }

        public static void PollToggle()
        {
            if (!AvianInput.WasLanguageTogglePressedThisFrame())
                return;
            if (handledFrame == Time.frameCount)
                return;

            handledFrame = Time.frameCount;
            Current = IsJapanese ? Id.English : Id.Japanese;
            Changed?.Invoke();
        }
    }
}
