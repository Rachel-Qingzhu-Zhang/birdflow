# Birdflow

**English · 日本語 · 中文**

Open [TEAM_GUIDE.html](TEAM_GUIDE.html) in a browser. The scene to press Play on is **Avian Lab**.

- [English](#english)
- [日本語](#japanese)
- [中文](#chinese)

---

# English

<a id="english"></a>

## 1. What Birdflow is

Birdflow is an immersive piece in which you become a parrot in a forest. The camera is the bird’s eyes. A mirror shows your bird body. The pond carries your reflection while you fly. When you speak or hum, you hear birdsong that follows the shape of your melody, as if the bird were calling with your voice.

The visit starts by seeing yourself, then breathing with the bird, gliding low over the water, singing a melody beside a companion, calling other birds in, and resting at the water.

The experience lives in `Assets/AvianVoice/`. The parrot model is under `Assets/Parrot/`. The forest and the pond are already placed in the scene.

## 2. Avian Lab

Open `Assets/AvianVoice/Scenes/Avian_Lab.unity` and press Play. The six scenes all run here. Press 1–6, or open **I want …**. A first visit goes from 1 to 6.

## 3. The six scenes

[1 Meet](#en-s1) · [2 Breath](#en-s2) · [3 Fly](#en-s3) · [4 Sing](#en-s4) · [5 Call](#en-s5) · [6 Rest](#en-s6)

### Scene 1 — Meet yourself

<a id="en-s1"></a>

**Input**　Where you look, and the microphone. At a desk, hold the right mouse button toward the mirror. In a headset, turn your head. Speak or hum, and one chirp follows your pitch.

**Task**　See your bird body in the mirror and stay for about 6 seconds.

Menu line: “I want to meet my bird self.” You stand at the mirror. It shows your parrot, `PlayerParrot`. The prompt says: “This is your bird self in the mirror. Look at it — no need to breathe yet.”

| When | Feedback in view | Audio |
|------|------------------|-------|
| On enter | Prompt: “This is your bird self in the mirror. Look at it — no need to breathe yet.” | None |
| You start a sound | One short chirp at your pitch | Live synth, not a file |
| About 6 seconds | The scene ends. You can open scene 2. | `birds_forest.wav` |

The debug panel then reads “You met your bird self.” It is hidden by default.

### Scene 2 — Learn the breath

<a id="en-s2"></a>

**Input**　Your own breathing. When the chest rises, breathe in. When it falls, breathe out.

**Task**　Follow a faster rhythm, then a slower one.

Menu line: “I want to learn its breath.” You are still at the mirror. The parrot’s chest and wings move on their own, with a slight cyan tint.

| Phase | Rhythm | One cycle | Cycles |
|-------|--------|-----------|--------|
| Pattern A | About 4 s in, 4 s out | About 8 s | 3 (about 24 s) |
| Pattern B | About 6 s in, 6 s out | About 12 s | 2 (about 24 s) |

| When | Feedback in view | Audio |
|------|------------------|-------|
| On enter | Prompt stays on the breath sentence. The chest starts pattern A. Feathers tint slightly cyan as the chest rises. | None |
| After A’s 3 cycles | A cyan flash, then the slower pattern B starts at once | `birds_forest.wav` |
| After B’s 2 cycles | Another cyan flash. The scene ends. | `birds_forest.wav` |

Timings are Teacher Breath on `AvianVoiceSettings`. The debug panel counts inhale / exhale and “cycle 1/3”, then “Breath sync complete!” The prompt card does not change.

### Scene 3 — Fly over the water

<a id="en-s3"></a>

**Input**　The microphone. Louder or higher climbs; silence descends. At a desk, E climbs and Q descends. In a headset, stick up/down nudges height and left/right turns. Forward flight is automatic. WASD does not move you here.

**Task**　Glide above the pond, set your height with your voice, and look down at the reflection.

Menu line: “I want to fly above the water.” You are placed above the pond, looking slightly down. Mirror off, pond reflection on. Your own view does not draw the bird’s body. The reflection shows the open wings. Forward speed is about 1.75 m/s. Height stays about 0.6–2.6 m over the water. This scene does not end by itself. Open **I want …** for the next one.

| When | Feedback in view | Audio |
|------|------------------|-------|
| On enter | Prompt stays on the flight sentence. The fly animation starts. | None |
| Louder or higher singing | You climb at once. The reflection climbs with you. | Live chirp |
| Silence | You sink at once, and stop above the water | None |
| E / Q, or the stick | Height changes at once. Stick left/right changes heading. | None |

### Scene 4 — Sing together

<a id="en-s4"></a>

**Input**　Microphone pitch. Hold the current note steady.

**Task**　Listen to the companion, then hold E4, G4, A4, and B4 one at a time.

Menu line: “I want to sing as a bird.” Mirror on, pond reflection off. `FriendParrot` stands beside you. A note passes when you stay within about 40 Hz for about 0.45 seconds.

| Order | Note | Hz |
|-------|------|----|
| 1 | E4 | 330 |
| 2 | G4 | 392 |
| 3 | A4 | 440 |
| 4 | B4 | 494 |

| When | Feedback in view | Audio |
|------|------------------|-------|
| On enter | The singing prompt stays. The companion’s chest opens and its body lifts. | `bird_melody.wav` from the start. If it is silent after 0.35 s, it plays again. |
| You are singing, not yet on the note | The companion’s feathers shift toward cyan as you get closer. Your own chest opens a little. | Live chirps, together with the recording |
| The note holds for about 0.45 s | A cyan flash, then the next note. The companion sings again. | `bird_melody.wav` from the start again |
| All four notes are held | The scene ends | `birds_forest.wav` |

The debug panel reads “Hold E4 — pitch your Hz/target Hz”, then “Melody complete!” The prompt card does not change.

### Scene 5 — Call your friends

<a id="en-s5"></a>

**Input**　Listen, then sing the current note back into the microphone.

**Task**　Each note you hold brings one distant bird in, until the group is with you.

Menu line: “I want to call my bird friends.” You are above the pond, and the water shows a reflection. Seven parrots wait about 30 m ahead, spread about 4 m across, between about 1.8 and 4.6 m high. Their notes are 330, 392, 440, 494, 523, 587, and 659 Hz. A note counts when you stay within about 70 Hz for about 0.32 seconds. The prompt changes during this scene.

| When | Feedback in view | Audio |
|------|------------------|-------|
| On enter | Prompt changes to “Listen to the melody.” | `bird_melody.wav`, through that play |
| That play ends | Prompt changes to “Your turn. Sing it back.” | None |
| You hold the current note | The nearest bird flies in. Your bird flashes cyan. The prompt becomes “Listen.” for about 0.7 s, then “Your turn. Sing it back.” | `birds_forest.wav`. The next note is demonstrated with a short chirp. |
| All 7 notes, then about 0.45 s | Prompt changes to “Your friends are here.” Each arrived bird answers. | `bird_melody.wav` again, plus a live answer from each bird |

### Scene 6 — Rest by the water

<a id="en-s6"></a>

**Input**　Looking at the mirror and the water. This scene does not score pitch or breath.

**Task**　Return to the mirror and read the three feelings from the visit.

Menu line: “I want to rest by the water.” You return to a place where the mirror is visible. Mirror on, pond reflection off. The prompt stays on the rest sentence.

| When | Feedback in view | Audio |
|------|------------------|-------|
| About 0.5 s after enter | Five chirps, about 0.23 s apart, near 440 Hz | Generated by the program |
| Debug panel on | Three percentages keep updating: Breathing Control, Voice Control, Synchronization | None |

## 4. Desktop: how to run it and how to play

**Run**

1. Open the repo in Unity **6000.3.4f1**.
2. Open `Assets/AvianVoice/Scenes/Avian_Lab.unity`. The scene name is **Avian Lab**.
3. Confirm `XR Origin Hands (XR Rig)` and `PlayerParrot` are in the Hierarchy.
4. Press Play.
5. Allow the microphone. On Windows the app starts the mic, resets the audio device so the speakers still work, then listens again. The first sound can be late by a beat while that reset finishes.
6. You are now in the desktop preview of Avian Lab. `DesktopFlyCamera` drives the main camera with the mouse and keyboard.

**Play**

| You want to | Do this |
|-------------|---------|
| Look around | Hold the right mouse button and move the mouse. Release it to click the UI. |
| Walk the forest (not during flight) | W A S D. Shift is faster. Q lowers the view, E raises it. |
| Change height in flight | Sing: louder or higher climbs, silence descends. E and Q also nudge height. WASD does not move you during flight. |
| Change scene | Click **I want …**, or press **M**. Pick a scene. Press M again, or choose “Stay here,” to close the list. |
| Jump with numbers | **1** meet, **2** breath, **3** fly, **4** sing, **5** call friends, **6** rest. **N** is next. |
| Skip ahead while testing | **C**. Turn off `enableDebugCheat` on the task before a visitor session. |

During flight, look down with the right mouse button to see the reflection. The prompt stays toward the top of the view. **I want …** stays low enough that it is still on screen when you look at the water.

## 5. Headset: how to run it and how to play

Same project, same scene: **Avian Lab**.

**Run**

1. Open `Avian_Lab.unity` in Unity 6.
2. XR is OpenXR. Quest builds use Meta OpenXR. Assign `Assets/AvianVoice/Settings/PlatformProfile.asset` on the bootstrap.
3. Press Play with the headset connected, or install a device build. You still enter Avian Lab.
4. Use the headset mic or a USB / OTG mic. On Quest the audio buffer is larger (about 512), so sound is a little later and more stable than at a desk.
5. `DesktopFlyCamera` does not take the view during a real headset session.

**Play**

| You want to | Do this |
|-------------|---------|
| Look | Turn your head. In flight, look down at the water. |
| Open the six scenes | Press the controller menu button, or aim at **I want …** and select it. |
| Turn while flying | Stick left / right changes the heading. The bird glides that way. |
| Climb or sink | Sing into the mic. Stick up / down can nudge height. |
| Sing, call friends, turn your voice into birdsong | Speak or hum. No button has to be held to “start” the voice. |

Keys 1–6, M, and C are for someone at the computer beside the visitor.

## 6. The two cards in view

1. **The prompt.** One sentence for the current moment. The strings live in `TaskManager`.
2. **I want ….** Six lines, one per scene in section 3. After you close it, the small card remains, including while you fly.

Score bars, pitch meters, and the Transform / Assistive switch stay hidden while `showHudPanels` is off. Turn that on only when you need the numbers. Do not stack a second copy of the prompt on top of the first.

## 7. Sound: your voice, the companion’s song, the calls that mark an ending

**Birdsong from your voice.**  
Each time you start a note, one short chirp plays at a pitch that follows you. Two quick notes should be two chirps, with your timing between them. A long tone does not become one endless whistle. This is the default, called Transform in code.

**The companion’s song.**  
Played in singing and in calling friends, from `bird_melody.wav`. It is a recording, not your microphone played back.

**A short call when a moment completes.**  
`bird_melody.wav` and `birds_forest.wav` are in `Assets/AvianVoice/Resources/BirdExperience/`.

### How pitch is kept

Human frequency times **8** (three octaves up), then held inside about 1600–5500 Hz. A hum at 220 Hz becomes about 1760 Hz; 440 Hz becomes about 3520 Hz. The interval between notes stays, so the melody shape is still there. The multiplier is `synthPitchRatio`.

### Why the chirps are separate

The synth waits for a new attack, or for loudness to fall and rise again. About 45 milliseconds of debounce stops one syllable from counting twice. Each chirp lasts about 0.08 seconds. Timing follows the mouth, not a metronome.

Turn `useSynthTransform` off to play samples from `BirdCallLibrary` instead.

Assistive is the other listening mode: you hear your own voice, with optional pitch shift and reverb. The visit does not start there. **T** toggles it when the debug UI is visible.

## 8. Where to edit

Namespaces stay `AvianVoice.*`. Move a script together with its `.cs.meta`. Comments are in English.

```
Assets/AvianVoice/
  Scenes/Avian_Lab.unity
  Settings/AvianVoiceSettings.asset
  Resources/BirdExperience/
  Scripts/Audio/  Core/  Tasks/  UI/  Physiology/
  Scripts/Embodiment/Parrot/  Mirror/  Pond/  Flock/  Scene/
```

| You want to change | Open |
|--------------------|------|
| Prompt and scene lines | `TaskManager.cs`, `SceneWishMenu.cs` |
| How long “meet” lasts | `MeetBirdTask.cs` (`LookSeconds`) |
| Breath timings | `AvianVoiceSettings` Teacher Breath, `BreathSyncTask.cs`, `BirdFriendDemonstrator.cs` |
| Flight speed and climb | `FlightControlTask.cs` |
| Flight camera, reflection, wing clip | `ParrotFlightPresentation.cs` |
| The four sung notes | `SingingBranchTask.cs` |
| The companion recording | `bird_melody.wav`, `BirdExperienceAudio.cs` |
| How many friends, how far | `CallFriendsTask.cs` |
| Chirp length and pitch ratio | `AvianVoiceSettings`, `BirdVoiceMapper.cs`, `BirdSongSynth.cs` |
| A silent microphone | `MicrophoneCapture.cs`, `VoiceAnalyzer.cs`, `voicedThreshold` |
| Mouse and WASD | `DesktopFlyCamera.cs` |
| Keys 1–6, M, C, E, Q | `AvianInput.cs` |

Boot order: `AvianAppBootstrap` (mic and analysis), `AvianInSceneTaskBootstrap` (the six moments inside Avian Lab), `BirdSceneRoles` (`PlayerParrot` and `FriendParrot`), then the **I want …** card and the prompt.

---
# 日本語

<a id="japanese"></a>

## 1. Birdflow とは

Birdflow は、森の中でオウムになる没入体験です。カメラは鳥の目線です。鏡には自分の鳥の身体が映り、飛んでいるときは池の水面に姿が映ります。話したりハミングしたりすると、自分のメロディの形を保った鳥の声が聞こえます。

流れは、自分を見るところから始まり、鳥と一緒に呼吸し、水面のすぐ上を滑空し、隣の鳥と歌い、遠くの仲間を呼び、水辺で休みます。

体験の中身は `Assets/AvianVoice/` にあります。オウムのモデルは `Assets/Parrot/`、森と池はシーンにすでに置いてあります。

## 2. Avian Lab

`Assets/AvianVoice/Scenes/Avian_Lab.unity` を開いて Play します。六つの場面はすべてこの中です。キーボードの 1–6、または **I want …** で入ります。最初は 1 から 6 です。

## 3. 六つの場面

[1 出会う](#ja-s1) · [2 呼吸](#ja-s2) · [3 飛行](#ja-s3) · [4 歌う](#ja-s4) · [5 呼ぶ](#ja-s5) · [6 休む](#ja-s6)

### 場面 1　自分に会う

<a id="ja-s1"></a>

**入力**　視線とマイク。机では右ボタンを押して鏡を見る。ヘッドセットでは頭を向ける。話すかハミングすると、音高に沿った一声が鳴る。

**課題**　鏡の中の自分の鳥を見て、およそ 6 秒とどまる。

メニューは “I want to meet my bird self.” 鏡の前に立ち、映るのは自分のオウム `PlayerParrot` です。案内は “This is your bird self in the mirror. Look at it — no need to breathe yet.” です。

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入ったとき | 案内：“This is your bird self in the mirror. Look at it — no need to breathe yet.” | なし |
| 声を出したとき | 音高に沿った短い一声 | その場で合成 |
| およそ 6 秒 | この場面が終わる。場面 2 へ進める。 | `birds_forest.wav` |

デバッグパネルは “You met your bird self.” になります。既定では見えません。

### 場面 2　呼吸を学ぶ

<a id="ja-s2"></a>

**入力**　自分の呼吸。胸が膨らむときに吸い、落ちるときに吐く。

**課題**　速いリズムに合わせ、そのあと遅いリズムに合わせる。

メニューは “I want to learn its breath.” まだ鏡の前です。オウムの胸と翼が自分で動き、少し青くなります。

| 段階 | リズム | 一周 | 回数 |
|------|--------|------|------|
| Pattern A | 吸う約 4 秒、吐く約 4 秒 | 約 8 秒 | 3（およそ 24 秒） |
| Pattern B | 吸う約 6 秒、吐く約 6 秒 | 約 12 秒 | 2（およそ 24 秒） |

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入ったとき | 案内はそのまま。胸が A で動き、膨らむほど羽が少し青くなる。 | なし |
| A が 3 周終わったとき | 青く光って、すぐ遅い B に変わる | `birds_forest.wav` |
| B が 2 周終わったとき | もう一度光って、この場面が終わる | `birds_forest.wav` |

秒数は `AvianVoiceSettings` の Teacher Breath。デバッグパネルは inhale / exhale と “cycle 1/3”、最後に “Breath sync complete!” を出します。案内カードは変わりません。

### 場面 3　水面を飛ぶ

<a id="ja-s3"></a>

**入力**　マイク。大きい声、または高い声で上昇。黙ると下降。机では E が上昇、Q が下降。ヘッドセットではスティックの上下が高さ、左右が向き。前進は自動です。この場面で WASD は動きません。

**課題**　池の上を滑空し、声で高さを決め、水面の映り込みを見る。

メニューは “I want to fly above the water.” 池の上から始まり、視線は少し下です。鏡はオフ、映り込みはオン。自分の眼前には体を描きません。映り込みは翼を広げた姿です。速さはおよそ毎秒 1.75 メートル、高さは水面の上 約 0.6〜2.6 メートル。この場面は自分では終わりません。**I want …** で次へ進みます。

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入ったとき | 案内はそのまま。飛行の動きが始まる。 | なし |
| 大きい声、または高い声 | すぐ上昇。映り込みも上がる。 | その場の鳥の声 |
| 黙ったとき | すぐ降り、水面の上で止まる | なし |
| E / Q、またはスティック | すぐ高さが変わる。左右は進行方向。 | なし |

### 場面 4　一緒に歌う

<a id="ja-s4"></a>

**入力**　マイクの音高。今の音を安定させる。

**課題**　隣の鳥を聴いてから、E4、G4、A4、B4 を一つずつ保つ。

メニューは “I want to sing as a bird.” 鏡はオン、映り込みはオフ。`FriendParrot` が隣に立ちます。今の音からおよそ 40 Hz 以内を約 0.45 秒保てると、その音は終わりです。

| 順 | 音 | Hz |
|----|----|----|
| 1 | E4 | 330 |
| 2 | G4 | 392 |
| 3 | A4 | 440 |
| 4 | B4 | 494 |

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入ったとき | 歌う案内はそのまま。隣の鳥は胸を開き、少し浮かぶ。 | `bird_melody.wav` を最初から。0.35 秒後に無音ならもう一度。 |
| 歌っているが、まだ合っていない | 近いほど隣の羽が青くなる。自分の胸も少し開く。 | その場の鳥の声。録音と同時。 |
| 約 0.45 秒保てたとき | 青く光り、次の音へ。隣の鳥がもう一度歌う。 | `bird_melody.wav` を最初からもう一度 |
| 四音とも終わったとき | この場面が終わる | `birds_forest.wav` |

デバッグパネルは “Hold E4 — pitch 自分の Hz/目標 Hz”、最後に “Melody complete!” です。案内カードは変わりません。

### 場面 5　仲間を呼ぶ

<a id="ja-s5"></a>

**入力**　まず聴き、それから今の音をマイクで歌い返す。

**課題**　音を一つ保つごとに遠くの鳥を一羽呼び、群れが揃うまで続ける。

メニューは “I want to call my bird friends.” 池の上にいて、水面に映り込みがあります。前方およそ 30 メートル、左右およそ 4 メートル、高さおよそ 1.8〜4.6 メートルに 7 羽います。音は 330、392、440、494、523、587、659 Hz。およそ 70 Hz 以内を約 0.32 秒保てると、その音は合ったことになります。この場面では案内の文が変わります。

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入ったとき | 案内が “Listen to the melody.” に変わる | `bird_melody.wav` をその再生が終わるまで |
| その再生が終わったとき | 案内が “Your turn. Sing it back.” に変わる | なし |
| 今の音を保てたとき | 一番近い鳥が飛んでくる。自分の鳥が青く光る。案内は “Listen.” になり、およそ 0.7 秒後に “Your turn. Sing it back.” に戻る。 | `birds_forest.wav`。次の音は短い一声で示します。 |
| 7 音のあと、およそ 0.45 秒 | 案内が “Your friends are here.” に変わる。来た鳥がそれぞれ応える。 | `bird_melody.wav` をもう一度。各鳥の応答は合成音。 |

### 場面 6　水辺で休む

<a id="ja-s6"></a>

**入力**　鏡と水辺を見る。この場面は音高も呼吸も判定しません。

**課題**　鏡の前に戻り、ここまでの三つの感覚を見る。

メニューは “I want to rest by the water.” 鏡の見える場所に戻ります。鏡はオン、映り込みはオフ。案内は休みの文のままです。

| いつ | 目の前の反応 | 音 |
|------|----------------|------|
| 入っておよそ 0.5 秒 | 五声。およそ 0.23 秒間隔、音高は約 440 Hz。 | プログラムが生成 |
| デバッグパネルを開けたとき | 三つの百分率が更新され続ける：Breathing Control、Voice Control、Synchronization | なし |

## 4. デスクトップでの起動と操作

**起動**

1. Unity **6000.3.4f1** でこのリポジトリを開く。
2. `Assets/AvianVoice/Scenes/Avian_Lab.unity` を開く。シーン名は **Avian Lab**。
3. Hierarchy に `XR Origin Hands (XR Rig)` と `PlayerParrot` があることを確認する。
4. Play。
5. マイクを許可する。Windows では、スピーカーが鳴るようにオーディオ装置を一度リセットしてから、聞き直しをします。最初の一声だけ半拍遅れることがあります。
6. 見ているのは Avian Lab のデスクトッププレビューです。主カメラは `DesktopFlyCamera` がマウスとキーボードで動かします。

**操作**

| したいこと | 操作 |
|------------|------|
| 見回す | 右ボタンを押したままマウスを動かす。離すとポインタが戻り、画面を指せます。 |
| 森を歩く（飛行中を除く） | W A S D。Shift で速く。Q で視点を下げ、E で上げる。 |
| 飛行中の高さ | 歌う。大きい、または高いと上昇。黙ると下降。E と Q でも微調整。飛行中は WASD では動きません。 |
| 場面を変える | **I want …** を押すか、**M**。場面を一つ選ぶ。もう一度 M、または “Stay here” で閉じる。 |
| 数字で跳ぶ | **1** 出会う、**2** 呼吸、**3** 飛行、**4** 歌う、**5** 仲間、**6** 水辺。**N** は次。 |
| テストで先へ進める | **C**。来訪者の前ではタスクの `enableDebugCheat` を外す。 |

飛行中は右ボタンで少し下を向き、水面と映り込みを見ます。案内は視野の上、**I want …** は下寄りに残り、水を見ていても場面を変えられます。

## 5. ヘッドセットでの起動と操作

同じプロジェクト、同じシーン **Avian Lab** です。

**起動**

1. Unity 6 で `Avian_Lab.unity` を開く。
2. XR は OpenXR。Quest 向けビルドは Meta OpenXR。`PlatformProfile.asset` を起動コンポーネントに割り当てる。
3. ヘッドセットをつないで Play するか、実機ビルドを入れる。入るのはやはり Avian Lab。
4. ヘッドセットのマイク、または USB / OTG マイクを使う。Quest ではバッファがおよそ 512 で、机の前より少し遅く、安定します。
5. 実際のヘッドセットセッションでは `DesktopFlyCamera` は視点を取りません。

**操作**

| したいこと | 操作 |
|------------|------|
| 見る | 頭を向ける。飛行中は水面を見る。 |
| 六つの場面を開く | コントローラのメニューボタン、または **I want …** を指して選ぶ。 |
| 飛行中に向きを変える | スティックの左右。鳥はその向きへ滑る。 |
| 上昇と下降 | マイクに向かって歌う。スティックの上下でも高さを押せる。 |
| 歌う、仲間を呼ぶ、声を鳥の声にする | 話すかハミングする。発声開始のボタンはない。 |

1–6、M、C は、横にいる人のキーボード用です。

## 6. 目の前の二つのカード

1. **案内。** 今の場面の一文。文面は `TaskManager` にあります。
2. **I want …。** 第 3 節の六場面へ切り替える六つの文。閉じても小さなカードは残り、飛行中も開けます。

点数バーや Transform / Assistive の切替は、`showHudPanels` がオフのあいだ出ません。数値が必要なときだけ開けます。案内の文を二枚重ねないでください。

## 7. 音: 自分の声、隣の鳥の歌、区切りの鳴き声

**自分の声から出る鳥の声。**  
音を出した瞬間に、短い一声が、その音高に合わせて鳴ります。続けて二回ハミングすれば二声です。長い音が一本の笛にはなりません。コード上の名前は Transform です。

**隣の鳥の歌。**  
歌う場面と、仲間を呼ぶ場面で、`bird_melody.wav` を再生します。録音であり、マイクの返しではありません。

**場面が終わったときの短い声。**  
`bird_melody.wav` と `birds_forest.wav` は `Assets/AvianVoice/Resources/BirdExperience/` にあります。

### 音高の揃え方

人の周波数を **8 倍**し（三オクターブ上）、およそ 1600〜5500 Hz に収めます。220 Hz は約 1760 Hz、440 Hz は約 3520 Hz。音程は残るので、メロディの形が分かります。倍率は `synthPitchRatio` です。

### 一声ずつになる理由

新しい出だし、または音量が落ちてからまた上がったときだけ鳴ります。約 45 ミリ秒の間隔で、同じ一音が二回に割れるのを防ぎます。一声の長さは約 0.08 秒です。拍子ではなく、口のタイミングに従います。

`useSynthTransform` を切ると、`BirdCallLibrary` のサンプルを再生します。

Assistive は、鳥の声に置き換えず、自分の声をピッチやリバーブ付きで聴くモードです。体験の初期状態ではありません。デバッグ表示があるとき **T** で切り替えられます。

## 8. どこを直すか

名前空間は `AvianVoice.*` のままです。スクリプトは `.cs.meta` と一緒に動かします。コメントは英語です。

```
Assets/AvianVoice/
  Scenes/Avian_Lab.unity
  Settings/AvianVoiceSettings.asset
  Resources/BirdExperience/
  Scripts/Audio/  Core/  Tasks/  UI/  Physiology/
  Scripts/Embodiment/Parrot/  Mirror/  Pond/  Flock/  Scene/
```

| 変えたいこと | 開くファイル |
|--------------|----------------|
| 案内と場面の文 | `TaskManager.cs`, `SceneWishMenu.cs` |
| 「出会う」の長さ | `MeetBirdTask.cs` の `LookSeconds` |
| 呼吸の秒数 | `AvianVoiceSettings` の Teacher Breath、`BreathSyncTask.cs`、`BirdFriendDemonstrator.cs` |
| 飛行の速さと上昇 | `FlightControlTask.cs` |
| 飛行カメラ、映り込み、翼 | `ParrotFlightPresentation.cs` |
| 四つの音 | `SingingBranchTask.cs` |
| 隣の鳥の録音 | `bird_melody.wav`、`BirdExperienceAudio.cs` |
| 仲間の数と距離 | `CallFriendsTask.cs` |
| 一声の長さと倍率 | `AvianVoiceSettings`、`BirdVoiceMapper.cs`、`BirdSongSynth.cs` |
| マイクが無い | `MicrophoneCapture.cs`、`VoiceAnalyzer.cs`、`voicedThreshold` |
| マウスと WASD | `DesktopFlyCamera.cs` |
| 1–6、M、C、E、Q | `AvianInput.cs` |

起動順は、`AvianAppBootstrap`（マイクと分析）、`AvianInSceneTaskBootstrap`（Avian Lab の六場面）、`BirdSceneRoles`（`PlayerParrot` と `FriendParrot`）、そのあと **I want …** と案内カードです。

---

# 中文

<a id="chinese"></a>

## 1. Birdflow 是什么

Birdflow 是一段在森林里变成鹦鹉的体验。镜头是鸟的眼睛。镜子里是你的鸟身。飞过池塘时，水面有倒影。说话或哼唱时，鸟鸣跟着你的旋律走，像这只鸟在用你的声音叫。

先看见自己，再跟着鸟呼吸，贴着水面滑翔，在伙伴身边唱歌，把别的鸟叫过来，最后在水边休息。

内容在 `Assets/AvianVoice/`。鹦鹉模型在 `Assets/Parrot/`。森林和池塘已经放在场景里。

## 2. Avian Lab

打开 `Assets/AvianVoice/Scenes/Avian_Lab.unity`，按 Play。六个场景都在这里。按 1–6，或打开 **I want …**。第一次从 1 走到 6。

## 3. 六个场景

[1 遇见](#zh-s1) · [2 呼吸](#zh-s2) · [3 飞行](#zh-s3) · [4 唱歌](#zh-s4) · [5 呼唤](#zh-s5) · [6 休息](#zh-s6)

### 场景 1　遇见自己

<a id="zh-s1"></a>

**输入**　看向哪里，以及麦克风。电脑上按住鼠标右键看镜子，头显里转头。说话或哼唱时，按你的音高叫一声。

**任务**　在镜子里看见自己的鸟身，停留大约 6 秒。

菜单句：“I want to meet my bird self.” 你站在镜子前，里面是你的鹦鹉 `PlayerParrot`。提示是：“This is your bird self in the mirror. Look at it — no need to breathe yet.”

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入时 | 提示：“This is your bird self in the mirror. Look at it — no need to breathe yet.” | 无 |
| 你一出声 | 一声短鸟鸣，音高跟着你 | 程序即时合成，不是音频文件 |
| 大约 6 秒 | 这一关结束，可以进入场景 2 | `birds_forest.wav` |

调试面板这时写成 “You met your bird self.” 默认不显示。

### 场景 2　学习呼吸

<a id="zh-s2"></a>

**输入**　自己的呼吸。胸口鼓起就吸气，落下就呼气。

**任务**　先跟上较快的节奏，再跟上较慢的。

菜单句：“I want to learn its breath.” 还在镜子前。鹦鹉的胸口和翅膀自己动，略微发青。

| 阶段 | 节奏 | 一轮 | 次数 |
|------|------|------|------|
| Pattern A | 吸大约 4 秒，呼大约 4 秒 | 大约 8 秒 | 3 次（大约 24 秒） |
| Pattern B | 吸大约 6 秒，呼大约 6 秒 | 大约 12 秒 | 2 次（大约 24 秒） |

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入时 | 提示保持呼吸那一句。胸口按 A 开始动。胸口鼓起时，羽毛略微发青。 | 无 |
| A 的 3 轮结束 | 闪一下青光，马上换成较慢的 B | `birds_forest.wav` |
| B 的 2 轮结束 | 再闪一下青光。这一关结束。 | `birds_forest.wav` |

秒数在 `AvianVoiceSettings` 的 Teacher Breath。调试面板显示吸气 / 呼气和 “cycle 1/3”，结束时是 “Breath sync complete!” 提示卡片不变。

### 场景 3　飞过水面

<a id="zh-s3"></a>

**输入**　麦克风。更大声或更高就上升，不发声就下降。电脑上 E 升高、Q 降低。头显里摇杆上下调高度，左右转向。前进是自动的。这一关 WASD 不移动。

**任务**　在池塘上方滑翔，用声音定高度，低头看水面倒影。

菜单句：“I want to fly above the water.” 你被放到池塘上方，视线略向下。镜子关，水面倒影开。自己的眼前不画鸟的身体。倒影里翅膀是张开的。前进大约每秒 1.75 米。高度保持在水面以上大约 0.6–2.6 米。这一关不会自己结束。打开 **I want …** 进入下一关。

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入时 | 提示保持飞行那一句。飞行动画开始。 | 无 |
| 唱得更大声或更高 | 马上上升。倒影一起上升。 | 即时鸟鸣 |
| 不发声 | 马上下降，停在水面上方 | 无 |
| E / Q，或摇杆 | 高度马上变。摇杆左右改变方向。 | 无 |

### 场景 4　一起唱歌

<a id="zh-s4"></a>

**输入**　麦克风音高。把当前这个音唱稳。

**任务**　先听伙伴唱，再逐个稳住 E4、G4、A4、B4。

菜单句：“I want to sing as a bird.” 镜子开，水面倒影关。`FriendParrot` 站在旁边。和目标音相差大约 40 Hz 以内，稳住大约 0.45 秒，这个音就过了。

| 顺序 | 音 | Hz |
|------|----|----|
| 1 | E4 | 330 |
| 2 | G4 | 392 |
| 3 | A4 | 440 |
| 4 | B4 | 494 |

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入时 | 唱歌的提示保持不变。伙伴胸口张开，身体微微浮起。 | `bird_melody.wav` 从头播。若 0.35 秒后还没有声音，再播一次。 |
| 你在唱，还没对准 | 越接近，伙伴的羽毛越青。你自己的胸口也会微微张开。 | 即时鸟鸣，和录音叠在一起 |
| 这个音稳住大约 0.45 秒 | 闪一下青光，进入下一个音。伙伴再唱一遍。 | `bird_melody.wav` 再从头播 |
| 四个音都稳住 | 这一关结束 | `birds_forest.wav` |

调试面板显示 “Hold E4 — pitch 你的 Hz/目标 Hz”，结束时是 “Melody complete!” 提示卡片不变。

### 场景 5　呼唤朋友

<a id="zh-s5"></a>

**输入**　先听，再用麦克风把当前这个音唱回去。

**任务**　每稳住一个音，就有一只远处的鸟飞过来，直到这群鸟都在身边。

菜单句：“I want to call my bird friends.” 你在池塘上方，水面有倒影。前方大约 30 米有 7 只鹦鹉，左右大约 4 米，高度大约 1.8 到 4.6 米。七个音是 330、392、440、494、523、587、659 Hz。和目标相差大约 70 Hz 以内，稳住大约 0.32 秒，算唱准。这一关的提示会改字。

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入时 | 提示改成 “Listen to the melody.” | `bird_melody.wav`，播完这一遍 |
| 这一遍播完 | 提示改成 “Your turn. Sing it back.” | 无 |
| 稳住当前这个音 | 最近的一只飞过来。你的鸟闪一下青光。提示先变成 “Listen.”，大约 0.7 秒后再变回 “Your turn. Sing it back.” | `birds_forest.wav`。下一个音用一声短鸟鸣做示范。 |
| 7 个音之后，再过大约 0.45 秒 | 提示改成 “Your friends are here.” 到齐的鸟各自应一声。 | 再播一遍 `bird_melody.wav`，每只鸟再各应一声 |

### 场景 6　水边休息

<a id="zh-s6"></a>

**输入**　看镜子和水边。这一关不计音高，也不计呼吸。

**任务**　回到镜子前，看这一路的三项感觉。

菜单句：“I want to rest by the water.” 回到能看见镜子的地方。镜子开，水面倒影关。提示保持休息那一句。

| 什么时候 | 眼前的反馈 | 音频 |
|----------|------------|------|
| 进入大约 0.5 秒 | 连叫五声，大约每 0.23 秒一声，音高大约 440 Hz | 程序生成 |
| 调试面板开着时 | 三项百分比一直更新：Breathing Control、Voice Control、Synchronization | 无 |

## 4. 电脑：怎么运行、怎么操作

**运行**

1. 用 Unity **6000.3.4f1** 打开本仓库。
2. 打开 `Assets/AvianVoice/Scenes/Avian_Lab.unity`。场景名是 **Avian Lab**。
3. 确认 Hierarchy 里有 `XR Origin Hands (XR Rig)` 和 `PlayerParrot`。
4. 按 Play。
5. 允许麦克风。在 Windows 上，程序会先打开麦克风，再重置音频设备，好让扬声器还能出声，然后重新听。这一下重置结束前，第一声可能晚半拍。
6. 现在看到的是 Avian Lab 的桌面预览。主相机由 `DesktopFlyCamera` 用鼠标和键盘驱动。

**操作**

| 你想做什么 | 怎么做 |
|------------|--------|
| 环顾 | 按住鼠标右键并移动。松开才能点界面。 |
| 走动（飞行时除外） | W A S D。Shift 更快。Q 降低视点，E 升高视点。 |
| 飞行时的高度 | 唱：更大声或更高就上升，不唱就下降。E 和 Q 也可以调高度。飞行时 WASD 不移动。 |
| 换场景 | 点 **I want …**，或按 **M**。选一个场景。再按 M，或选 “Stay here”，把列表合上。 |
| 用数字跳 | **1** 遇见，**2** 呼吸，**3** 飞行，**4** 唱歌，**5** 呼唤，**6** 休息。**N** 是下一关。 |
| 测试时跳过 | **C**。给体验者玩之前，关掉任务上的 `enableDebugCheat`。 |

飞行时用右键把视线低下，才能看到倒影。提示在视野上方。**I want …** 的位置偏低，低头看水时仍留在画面里。

## 5. 头显：怎么运行、怎么操作

还是同一个工程、同一个场景：**Avian Lab**。

**运行**

1. 用 Unity 6 打开 `Avian_Lab.unity`。
2. XR 使用 OpenXR。Quest 构建用 Meta OpenXR。把 `Assets/AvianVoice/Settings/PlatformProfile.asset` 赋给启动组件。
3. 头显连上后按 Play，或安装到设备上再打开。进入的仍是 Avian Lab。
4. 用头显麦克风，或 USB / OTG 麦克风。Quest 的音频缓冲大约 512，比电脑前略慢，也更稳。
5. 真正的头显会话里，`DesktopFlyCamera` 不会接管视角。

**操作**

| 你想做什么 | 怎么做 |
|------------|--------|
| 看 | 转头。飞行时低头看水面。 |
| 打开六个场景 | 按手柄菜单键，或看向 **I want …** 并点它。 |
| 飞行时转向 | 摇杆左右改变方向。鸟朝那个方向滑。 |
| 上升或下降 | 对着麦克风唱。摇杆上下也可以调高度。 |
| 唱歌、呼唤、让鸟鸣跟着你 | 说话或哼唱。没有一个键用来“开始发声”。 |

1–6、M、C 是给旁边电脑上的人用的。

## 6. 眼前的两张卡片

1. **提示。** 当前这一关的一句话。句子在 `TaskManager` 里。
2. **I want …。** 六句用来切换场景，对应第 3 节。合上后小卡片还在，飞行时也能再打开。

分数条、音高条，以及 Transform / Assistive 开关，在 `showHudPanels` 关闭时不显示。需要看数值时再打开。不要把同一句提示画成两张。

## 7. 声音：你的声音、伙伴的歌、一段结束时的叫声

**你的声音变成的鸟鸣。**  
你每起一个音，程序就叫一声，音高跟着你。很快的两声就是两声鸟鸣，间隔也是你的。长音不会变成一声不停的哨音。这是默认方式，代码里叫 Transform。

**伙伴的歌。**  
唱歌和呼唤朋友时播放 `bird_melody.wav`。这是录音，不是把麦克风原样放出来。

**一段结束时的短叫声。**  
`bird_melody.wav` 和 `birds_forest.wav` 在 `Assets/AvianVoice/Resources/BirdExperience/`。

### 音高怎么保留

人声频率乘 **8**（升高三个八度），再限制在大约 1600–5500 Hz。220 Hz 大约变成 1760 Hz，440 Hz 大约变成 3520 Hz。音与音之间的高低关系还在，所以旋律的形状还在。这个倍数是 `synthPitchRatio`。

### 为什么是一声一声的

合成器等到新的起音，或音量落下再升起，才叫下一声。大约 45 毫秒的间隔，避免同一下被算成两声。每声大约 0.08 秒。时间跟着嘴，不跟节拍器。

关掉 `useSynthTransform`，就会改播 `BirdCallLibrary` 里的采样。

Assistive 是另一种听法：听到的是自己的声音，可以加音高变化和混响。体验开始时不用这个。调试界面开着时，按 **T** 切换。

## 8. 改哪里

命名空间保持 `AvianVoice.*`。移动脚本时连同 `.cs.meta`。注释用英文。

```
Assets/AvianVoice/
  Scenes/Avian_Lab.unity
  Settings/AvianVoiceSettings.asset
  Resources/BirdExperience/
  Scripts/Audio/  Core/  Tasks/  UI/  Physiology/
  Scripts/Embodiment/Parrot/  Mirror/  Pond/  Flock/  Scene/
```

| 要改什么 | 打开 |
|----------|------|
| 提示和切换场景的句子 | `TaskManager.cs`，`SceneWishMenu.cs` |
| 遇见要看多久 | `MeetBirdTask.cs`（`LookSeconds`） |
| 呼吸秒数 | `AvianVoiceSettings` 的 Teacher Breath，`BreathSyncTask.cs`，`BirdFriendDemonstrator.cs` |
| 飞行速度和升降 | `FlightControlTask.cs` |
| 飞行相机、倒影、翅膀 | `ParrotFlightPresentation.cs` |
| 四个音 | `SingingBranchTask.cs` |
| 伙伴的录音 | `bird_melody.wav`，`BirdExperienceAudio.cs` |
| 叫来几只、停多远 | `CallFriendsTask.cs` |
| 鸟鸣长度和音高倍数 | `AvianVoiceSettings`，`BirdVoiceMapper.cs`，`BirdSongSynth.cs` |
| 麦克风没有声音 | `MicrophoneCapture.cs`，`VoiceAnalyzer.cs`，`voicedThreshold` |
| 鼠标和 WASD | `DesktopFlyCamera.cs` |
| 按键 1–6、M、C、E、Q | `AvianInput.cs` |

启动顺序：`AvianAppBootstrap`（麦克风和分析），`AvianInSceneTaskBootstrap`（Avian Lab 里的六个场景），`BirdSceneRoles`（`PlayerParrot` 和 `FriendParrot`），然后是 **I want …** 和提示卡片。
