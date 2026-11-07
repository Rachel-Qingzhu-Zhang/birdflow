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
                + "鳥は自分で滑空します。大きく、または高く歌うと上昇し、黙ると下降します。"
                + "鳥が一つの音を歌います。鳴き終わったら、同じ音を歌ってください。次の音も同じです。"
                + "鳥が一つの音を歌います。同じ音を歌うと仲間が飛んできます。音ごとに鳥が増えます。"
                + "水辺で休み、ここまでの感覚を見てください。鳥になるのは、どんな感じでしたか。"
                + "鏡の中の自分に会う鳥の呼吸を覚える水面の上を飛ぶ鳥として歌う仲間の鳥を呼ぶ水辺で休む"
                + "この音を聴いてください。今度はこの音を歌ってください。この音を歌って仲間を呼んでください。"
                + "仲間が向かっています。次の音を聴いてください。メロディができました。仲間が来ました。"
                + "次の音を待ってください。お疲れさまでした。そのまま休んでください。";
        }

        public static string Instruction(string taskId)
        {
            return taskId switch
            {
                "practice" => AvianUiLanguage.Pick(
                    "Practice. The bird sings a note; sing it back. The experimenter will explain.",
                    "練習です。鳥が歌った音を、同じように歌ってください。実験者が説明します。"),
                "meet" => AvianUiLanguage.Pick(
                    "This is your bird self in the mirror. Look at it — no need to breathe yet.",
                    "鏡の中にいるのが、あなたの鳥です。しばらく見ていてください。まだ呼吸しなくて大丈夫です。"),
                "breath_sync" => AvianUiLanguage.Pick(
                    "Watch the bird's chest and breathe with it.",
                    "鳥の胸を見て、一緒に呼吸してください。"),
                "flying" => AvianUiLanguage.Pick(
                    "The bird glides on its own. Sing louder or higher to climb, stay quiet to glide down.",
                    "鳥は自分で滑空します。大きく、または高く歌うと上昇し、黙ると下降します。"),
                "singing" => AvianUiLanguage.Pick(
                    "The bird sings one note. When it stops, sing that same note. Then it sings the next one.",
                    "鳥が一つの音を歌います。鳴き終わったら、同じ音を歌ってください。次の音も同じです。"),
                "call_friends" => AvianUiLanguage.Pick(
                    "The bird sings one note. Sing it back and one friend flies in.",
                    "鳥が一つの音を歌います。同じ音を歌うと、仲間が一羽飛んできます。"),
                "reflection" => AvianUiLanguage.Pick(
                    "Rest by the water and look over your scores. How did it feel to become a bird?",
                    "水辺で休み、ここまでの感覚を見てください。鳥になるのは、どんな感じでしたか。"),
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

        public static void ShowSingingCue(string kind, string note)
        {
            var name = string.IsNullOrEmpty(note) ? "" : note;
            switch (kind)
            {
                case "listen":
                    SceneGuide.SetPhase($"Listen to {name}.", $"{name}を聴いてください。");
                    break;
                case "sing":
                    SceneGuide.SetPhase($"Now sing {name}.", $"今度は{name}を歌ってください。");
                    break;
                case "call":
                    SceneGuide.SetPhase($"Sing {name} to call a friend.", $"{name}を歌って仲間を呼んでください。");
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
