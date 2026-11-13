namespace AvianVoice.UI
{
    /// <summary>
    /// Player-facing lines for the prompt card and the scene menu.
    /// </summary>
    public static class AvianUiCopy
    {
        public static string MenuTitle => AvianUiLanguage.Pick("Choose a scene", "場面を選ぶ");

        public static string MenuChip => AvianUiLanguage.Pick("I want …", "場面 …");

        public static string StayHere => AvianUiLanguage.Pick("Stay here", "このまま");

        public static string Replay => AvianUiLanguage.Pick("Replay", "もう一度");

        public static string GlyphProbe()
        {
            return "場面を選ぶこのままもう一度"
                + "練習です。鳥が歌った音を、同じように歌ってください。実験者が説明します。練習する"
                + "鏡の中にいるのが、あなたの鳥です。しばらく見ていてください。まだ呼吸しなくて大丈夫です。"
                + "鳥の胸を見て、一緒に呼吸してください。"
                + "鏡の中の自分を見てください。鏡の中の自分に合わせて呼吸してください。"
                + "先生と同じ音程で歌ってください。少し休みましょう。"
                + "鳥は自分で滑空します。大きく、または高く歌うと上昇し、黙ると下降します。"
                + "鳥が一つの音を歌います。鳴き終わったら、同じ音を歌ってください。次の音も同じです。"
                + "鳥が一つの音を歌います。同じ音を歌うと仲間が飛んできます。音ごとに鳥が増えます。"
                + "水辺で休み、ここまでの感覚を見てください。鳥になるのは、どんな感じでしたか。"
                + "鏡の中の自分に会う鳥の呼吸を覚える水面の上を飛ぶ鳥として歌う仲間の鳥を呼ぶ水辺で休む"
                + "この音を聴いてください。今度はこの音を歌ってください。この音を歌って仲間を呼んでください。"
                + "仲間が向かっています。次の音を聴いてください。メロディができました。仲間が来ました。"
                + "次の音を待ってください。お疲れさまでした。そのまま休んでください。";
        }

        /// <summary>
        /// Title on the guide card. The same in every condition (bird / human avatar, converted / own voice) and
        /// in English only. "" for the scenes outside the study flow, which keep their one-line prompt.
        /// </summary>
        public static string Title(string taskId)
        {
            return taskId switch
            {
                "practice" => "Practice",
                "meet" => "Meet Scene",
                "breath_sync" => "Breathing Scene",
                "singing" => "Singing Scene",
                "reflection" => "Rest Scene",
                _ => ""
            };
        }

        /// <summary>Instruction of a study scene in English. The same in every condition; none in the practice scene.</summary>
        public static string InstructionEnglish(string taskId)
        {
            return taskId switch
            {
                "meet" => "Look at yourself in the mirror.",
                "breath_sync" => "Breathe together with yourself in the mirror.",
                "singing" => "Sing the same pitch as the teacher.",
                "reflection" => "Take a short rest.",
                _ => ""
            };
        }

        /// <summary>Instruction of a study scene in Japanese. The same in every condition; none in the practice scene.</summary>
        public static string InstructionJapanese(string taskId)
        {
            return taskId switch
            {
                "meet" => "鏡の中の自分を見てください。",
                "breath_sync" => "鏡の中の自分に合わせて呼吸してください。",
                "singing" => "先生と同じ音程で歌ってください。",
                "reflection" => "少し休みましょう。",
                _ => ""
            };
        }

        /// <summary>
        /// Text of the guide card (SceneGuide). Study scenes: the title, then the instruction in Japanese and under it
        /// in English, both always shown. While a singing cue is up (<paramref name="phaseEnglish"/> /
        /// <paramref name="phaseJapanese"/>, e.g. "Now sing this note.") it takes the place of the instruction. The practice
        /// scene shows its title only. Other scenes: the cue or their prompt in the UI language, as before.
        /// </summary>
        public static string GuideText(string taskId, string phaseEnglish, string phaseJapanese)
        {
            var title = Title(taskId);
            if (string.IsNullOrEmpty(title))
            {
                var phase = AvianUiLanguage.Pick(phaseEnglish, phaseJapanese);
                return string.IsNullOrEmpty(phase) ? Instruction(taskId) : phase;
            }

            var heading = "<size=120%><b>" + title + "</b></size>";
            if (taskId == "practice")
                return heading;

            var hasPhase = !string.IsNullOrEmpty(phaseEnglish) || !string.IsNullOrEmpty(phaseJapanese);
            var japanese = hasPhase ? (string.IsNullOrEmpty(phaseJapanese) ? phaseEnglish : phaseJapanese) : InstructionJapanese(taskId);
            var english = hasPhase ? (string.IsNullOrEmpty(phaseEnglish) ? phaseJapanese : phaseEnglish) : InstructionEnglish(taskId);
            return japanese == english
                ? heading + "\n" + english
                : heading + "\n" + japanese + "\n" + english;
        }

        /// <summary>
        /// One-line instruction in the UI language (the debug HUD, and the guide card of the scenes outside the study
        /// flow). For the study scenes it is the same sentence as on the guide card.
        /// </summary>
        public static string Instruction(string taskId)
        {
            if (!string.IsNullOrEmpty(Title(taskId)))
                return AvianUiLanguage.Pick(InstructionEnglish(taskId), InstructionJapanese(taskId));

            return taskId switch
            {
                "flying" => AvianUiLanguage.Pick(
                    "The bird glides on its own. Sing louder or higher to climb, stay quiet to glide down.",
                    "鳥は自分で滑空します。大きく、または高く歌うと上昇し、黙ると下降します。"),
                "call_friends" => AvianUiLanguage.Pick(
                    "The bird sings one note. Sing it back and one friend flies in.",
                    "鳥が一つの音を歌います。同じ音を歌うと、仲間が一羽飛んできます。"),
                _ => ""
            };
        }

        public static string SceneLine(string taskId)
        {
            return taskId switch
            {
                "practice" => AvianUiLanguage.Pick("I want to practice.", "練習する"),
                "meet" => AvianUiLanguage.Pick("I want to meet my bird self.", "鏡の中の自分に会う"),
                "breath_sync" => AvianUiLanguage.Pick("I want to learn its breath.", "鳥の呼吸を覚える"),
                "flying" => AvianUiLanguage.Pick("I want to fly above the water.", "水面の上を飛ぶ"),
                "singing" => AvianUiLanguage.Pick("I want to sing as a bird.", "鳥として歌う"),
                "call_friends" => AvianUiLanguage.Pick("I want to call my bird friends.", "仲間の鳥を呼ぶ"),
                "reflection" => AvianUiLanguage.Pick("I want to rest by the water.", "水辺で休む"),
                _ => ""
            };
        }

        /// <summary>
        /// Cue on the guide card while a note is taught or sung. The participant is never shown the name of the note
        /// (B3, D4 ...) or its frequency: the text drew the attention away from the mirror (2026-11-12). The note is
        /// still recorded (trials.csv, events.jsonl) and shown to the experimenter (panel, Study Monitor).
        /// <paramref name="note"/> is accepted for the callers that have it, and not displayed.
        /// </summary>
        public static void ShowSingingCue(string kind, string note)
        {
            switch (kind)
            {
                case "listen":
                    SceneGuide.SetPhase("Listen to this note.", "この音を聴いてください。");
                    break;
                case "sing":
                    SceneGuide.SetPhase("Now sing this note.", "今度はこの音を歌ってください。");
                    break;
                case "call":
                    SceneGuide.SetPhase("Sing this note to call a friend.", "この音を歌って仲間を呼んでください。");
                    break;
                case "coming":
                    SceneGuide.SetPhase("A friend is coming.", "仲間が向かっています。");
                    break;
                case "next":
                    SceneGuide.SetPhase("Listen for the next note.", "次の音を聴いてください。");
                    break;
                case "done":
                    SceneGuide.SetPhase("Melody complete.", "メロディができました。");
                    break;
                case "friends":
                    SceneGuide.SetPhase("Your friends are here.", "仲間が来ました。");
                    break;
                case "wait":
                    SceneGuide.SetPhase("Wait for the next note.", "次の音を待ってください。");
                    break;
                case "finished":
                    SceneGuide.SetPhase("All done. Rest now.", "お疲れさまでした。そのまま休んでください。");
                    break;
                default:
                    SceneGuide.ClearPhase();
                    break;
            }
        }

        public static void SetGuidePhase(string key)
        {
            switch (key)
            {
                case "listen_melody":
                    SceneGuide.SetPhase("Listen to the melody.", "メロディを聴いてください。");
                    break;
                case "your_turn":
                    SceneGuide.SetPhase("Your turn. Sing it back.", "あなたの番です。歌い返してください。");
                    break;
                case "listen":
                    SceneGuide.SetPhase("Listen.", "聴いてください。");
                    break;
                case "friends_here":
                    SceneGuide.SetPhase("Your friends are here.", "仲間が来ました。");
                    break;
                default:
                    SceneGuide.ClearPhase();
                    break;
            }
        }
    }
}
