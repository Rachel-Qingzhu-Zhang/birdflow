using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AvianVoice.UI
{
    /// <summary>
    /// English uses Liberation Sans. Japanese uses the bundled Kosugi Maru face,
    /// because the OS font lookup was rendering kana and kanji as empty boxes.
    /// </summary>
    public static class AvianUiFont
    {
        const string JapaneseResource = "Fonts/KosugiMaru-Regular";

        static TMP_FontAsset latin;
        static TMP_FontAsset japanese;
        static bool japaneseAttempted;

        public static void Apply(TMP_Text text)
        {
            if (text == null)
                return;

            var font = AvianUiLanguage.IsJapanese ? Japanese() : Latin();
            if (font == null)
                return;

            text.font = font;
            if (font.material != null)
                text.fontSharedMaterial = font.material;
        }

        /// <summary>
        /// For a text that shows Japanese and English together whatever the UI language (the guide card): the
        /// Japanese face, which also has the Latin letters. With Liberation Sans the Japanese line is empty boxes.
        /// </summary>
        public static void ApplyBilingual(TMP_Text text)
        {
            if (text == null)
                return;

            var font = Japanese();
            if (font == null)
                return;

            text.font = font;
            if (font.material != null)
                text.fontSharedMaterial = font.material;
        }

        static TMP_FontAsset Latin()
        {
            if (latin != null)
                return latin;

            latin = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return latin;
        }

        static TMP_FontAsset Japanese()
        {
            if (japanese != null || japaneseAttempted)
                return japanese != null ? japanese : Latin();

            japaneseAttempted = true;
            var source = Resources.Load<Font>(JapaneseResource);
            if (source == null)
            {
                Debug.LogWarning("[AvianVoice] Missing Resources/" + JapaneseResource + ". Japanese text cannot render.");
                return Latin();
            }

            japanese = TMP_FontAsset.CreateFontAsset(
                source,
                42,
                6,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (japanese == null)
            {
                Debug.LogWarning("[AvianVoice] Could not build the Japanese font asset.");
                return Latin();
            }

            japanese.name = "AvianUiJapanese";
            if (!japanese.TryAddCharacters(AvianUiCopy.GlyphProbe(), out var missing) && missing != null && missing.Length > 0)
                Debug.LogWarning("[AvianVoice] Japanese font is missing " + missing.Length + " characters.");

            return japanese;
        }
    }
}
