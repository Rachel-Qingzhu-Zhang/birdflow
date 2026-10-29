# Birdflow / Avian Voice — Team Guide

**English · 日本語 · 中文**

This is the current team handbook for the live lab. Older files in this folder (`PROJECT_OVERVIEW_CN.md`, `SCENES_AND_TASKS_CN.md`, `PROJECT_USERFLOW_CN.md`) may still mention Space-key breathing or numbered HUD panels — **this document matches the code as of October 2026**.

| | |
|---|---|
| Open in a browser (language tabs) | [`TEAM_GUIDE.html`](TEAM_GUIDE.html) |
| Live Unity scene | `Assets/AvianVoice/Scenes/Avian_Lab.unity` |
| Script map | [`Scripts/README.md`](Scripts/README.md) |

- [English](#english)
- [日本語](#japanese)
- [中文](#chinese)

---

# English

<a id="english"></a>

## 1. What this project is

**Birdflow** (product / repo name) is a Unity 6 VR research prototype. The playable module is **Avian Voice** under `Assets/AvianVoice/`.

It is built for **CHI 2027**-related work on:

- **Bird body illusion** — first-person parrot body, chest breath, wings, beak, pond reflection, and a baroque world mirror.
- **Voice as the only gameplay input** — the microphone drives bird song, flight climb, and pitch-matching tasks. There is **no Space-key breath input**.
- **Guided embodiment** — a teacher timeline expands the player bird’s chest. A companion parrot appears when the task is about singing together.

**Player bird:** scene object `PlayerParrot`.  
**Companion bird:** scene object `FriendParrot` (hidden except in Singing).  
Do not treat leftover RoleMarker spheres as gameplay.

## 2. How to run

| Item | Value |
|------|--------|
| Engine | Unity **6000.3.4f1** (Unity 6), URP |
| XR | OpenXR + XR Interaction Toolkit + XR Hands |
| Lab scene | `Assets/AvianVoice/Scenes/Avian_Lab.unity` |
| Settings | `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` |
| Platform | `Assets/AvianVoice/Settings/PlatformProfile.asset` |

1. Open `Avian_Lab.unity` and press Play.
2. Allow the microphone. On Windows the capture path starts the mic then resets the audio device so speakers keep working; capture restarts once after that.
3. Use the **I want …** chip (or **M** / XR menu button) to pick a scene. Keyboard **1–6** also switches tasks. **C** is a debug cheat (disable `enableDebugCheat` on the task before a real study).

**Desktop:** `DesktopFlyCamera` — hold right mouse to look, **WASD** to move (WASD is disabled during Flying so the flight task owns locomotion). **E / Q** nudge flight altitude.

**Quest:** Meta OpenXR, larger DSP buffer (~512). Prefer an external USB/OTG mic.

Debug metrics HUD (`TrainingHUD`, task score panel, Transform/Assistive toggle) stays **off** while `showHudPanels` is false. The wish menu and the one-line scene prompt are independent of that flag.

## 3. Input and output

### Input (what the user can do)

| Channel | Used for |
|---------|----------|
| **Microphone** | Primary gameplay. Pitch (YIN) + loudness + voiced flag. |
| Wish menu / **M** / XR **menu** | Choose a scene. Copy is invitational, not numbered. |
| Keys **1–6**, **N** | Jump / next task (desktop debug). |
| **C** | Instant-complete the current task. |
| **E / Q** (or arrows / stick Y) | Flight altitude nudge. |
| **T** | Transform ↔ Assistive voice mode (only meaningful if HUD/mode UI is on). |
| Head / HMD | Look. Flying hides the player mesh from the headset so looking down shows the pond; the reflection camera still draws the bird. |

There is **no** Space / breath-controller gameplay. `BreathSimulator` copies the teacher pattern (`Source.Guided`).

### Output (what the user sees and hears)

| Channel | What happens |
|---------|----------------|
| **Bird song (Transform, default)** | Discrete synthesized chirps that follow the singer’s pitch contour. |
| **Assistive mode** | User’s own voice monitored with pitch shift / optional mixer reverb — no chirp library. |
| **Experience clips** | `Resources/BirdExperience/` — melody, pitch phrase, forest sting, reward chorus. |
| **Player parrot** | Chest scale, wing spread, beak open, slight bob when voiced. Fly clip during Flying. |
| **Companion parrot** | Visible in Singing: chest + float while the melody plays. |
| **World mirror** | Tasks Meet, Breath Sync, Singing, Reflection. |
| **Pond planar reflection** | Tasks Flying and Call Friends. |
| **Logs** | `Application.persistentDataPath/avian_trials.jsonl` (~30 Hz). |

## 4. Six scenes — tasks and user flow

All six live **inside** `Avian_Lab`. `TaskManager` enables one task behaviour at a time (`InScene` mode). Independent `Task_*` scenes still exist as legacy extras.

Wish lines (what the user reads):

| Key | ID | Wish | Prompt |
|-----|-----|------|--------|
| 1 | `meet` | I want to meet my bird self. | This is your bird self in the mirror. Look at it — no need to breathe yet. |
| 2 | `breath_sync` | I want to learn its breath. | Watch the bird's chest and breathe with it. Pattern A first, then the slower Pattern B. |
| 3 | `flying` | I want to fly above the water. | The bird glides on its own. Sing louder or higher to climb, stay quiet to glide down. |
| 4 | `singing` | I want to sing as a bird. | Your bird sings a note. Hold that pitch with your voice until the melody is done. |
| 5 | `call_friends` | I want to call my bird friends. | Sing the melody to call birds in. One bird comes first, then three, then the flock. |
| 6 | `reflection` | I want to rest by the water. | Rest by the water and look over your scores. How did it feel to become a bird? |

### Scene 1 — Meet Your Bird (`MeetBirdTask`)

**Goal:** First look at the player bird in the baroque mirror. No breath drill.

**Flow:** Mirror on. Pond reflection off. Friend parrot hidden. Player parrot visible. After about **6 seconds** of looking (or **C**), a short forest reward plays and the task completes.

**Edit:** `Scripts/Tasks/MeetBirdTask.cs` (`LookSeconds`).

### Scene 2 — Breath Sync (`BreathSyncTask`)

**Goal:** Watch the **player** bird breathe on a teacher timeline (the body is driven; the user does not press a breath key).

**Flow:** Mirror on. Teacher uses Pattern A (4 s in / 4 s out, `patternACycles` default 3), then Pattern B (6 s / 6 s, `patternBCycles` default 2). Score follows cycle progress. Completing a pattern plays the forest reward.

**Edit:** timings on `AvianVoiceSettings` (Teacher Breath). Logic in `BreathSyncTask.cs` + `BirdFriendDemonstrator.cs` + `BreathSimulator.cs`. Visual chest/wings: `ParrotBreathEmbodiment.cs`.

### Scene 3 — Flying (`FlightControlTask` + `ParrotFlightPresentation`)

**Goal:** First-person glide over the pond. Voice controls height.

**Flow:** Mirror off. Pond reflection on. XR Origin moves forward on its own (~1.75 m/s). Louder or higher voice climbs; silence glides down toward the water (clamped above the pond). The pack **fly** animation poses the wings. The player mesh is hidden from game cameras (not from the reflection camera) so looking down shows water and the bird’s reflection.

**Edit:** locomotion `Tasks/FlightControlTask.cs`; FPV follow / hide mesh `Embodiment/Parrot/ParrotFlightPresentation.cs`; start pose `Embodiment/Scene/XrRigTaskStartPoses.cs`.

### Scene 4 — Singing Branch (`SingingBranchTask`)

**Goal:** Sing with the companion. Hold each target pitch.

**Flow:** Friend parrot **shown**. Companion plays **`bird_melody.wav`** (full clip, 2D). Default targets: 330, 392, 440, 494 Hz (E4–B4). Hold within ~40 Hz for ~0.45 s to advance. Companion chest expands and floats while the reference plays. Player voice still maps to bird chirps through the global synth.

**Edit:** `Tasks/SingingBranchTask.cs`, `Audio/BirdExperienceAudio.cs`. Replace the song by swapping `Resources/BirdExperience/bird_melody.wav`.

### Scene 5 — Call Friends (`CallFriendsTask`)

**Goal:** Call a flock by singing the melody back.

**Flow:** Pond reflection on. Companions wait ahead, then fly in as notes match (default two notes, 330 then 392 Hz). Stages: listen to melody → sing back → one / three / flock. `bird_melody` is the reference.

**Edit:** `Tasks/CallFriendsTask.cs`, `Embodiment/Flock/`.

### Scene 6 — Reflection (`ReflectionTask`)

**Goal:** Rest, see session scores, close the loop.

**Flow:** Mirror on. Pond reflection off. Player bird visible by the water. Scores from earlier voice / breath / sync caches. Short farewell.

**Edit:** `Tasks/ReflectionTask.cs`, `Data/SessionScoreCache.cs`.

**Typical session:** Meet → Breath Sync → Flying → Singing → Call Friends → Reflection. Users may jump with the wish card.

## 5. How human voice becomes bird song

### Pipeline

```
Microphone
  → MicrophoneCapture (ring buffer; restart once after AudioSettings.Reset)
  → VoiceAnalyzer (RMS loudness + YIN pitch)
  → BirdVoiceMapper
       Transform + useSynthTransform (default)
         → onset / dip-then-rise
         → BirdSongSynth.PlayChirp(humanHz, loudness)
       Transform, synth off
         → BirdCallLibrary.PlayNearest (sample chirps)
       Assistive
         → AssistiveMonitor pitch + optional Audio Mixer (MicPitch, MicReverb)
```

### Pitch (keep the melody contour)

`BirdSongSynth.MapHumanToBirdHz`:

```
birdHz = clamp(humanHz * synthPitchRatio, synthBirdMinHz, synthBirdMaxHz)
```

Default **ratio = 8** (three octaves up). A 220 Hz hum becomes 1760 Hz; 440 Hz becomes 3520 Hz, still the same interval. Human Hz is clamped to 90–900 before multiply.

### Rhythm (one chirp per attack)

Chirps are **not** a metronome. `BirdVoiceMapper.UpdateSynth` fires when:

1. Voice starts after silence, or
2. Loudness dips then rises again (a second note while still “voiced”).

Only a ~45 ms debounce prevents double-triggers on one attack. `synthChirpSeconds` (~0.08 s) is the **length of one chirp**, not the gap between notes.

Each chirp is a short whistle: fast attack, light trill, 2nd harmonic, a little breath noise (`OnAudioFilterRead`).

### Experience audio (not the player’s voice)

Loaded from `Resources/BirdExperience/` by `BirdExperienceAudio`:

| File | Role |
|------|------|
| `bird_melody.wav` | Companion / flock **song** (Singing, Call Friends) |
| `bird_pitch.wav` | Fallback pitch phrases |
| `birds_forest.wav` | Short success sting |
| `birds_reward.wav` | Longer breath-reward bed |

Playback is 2D (`spatialBlend = 0`) so desktop speakers and the HMD both hear it. After Windows speaker restore, melody is replayed if it was supposed to be playing.

### Knobs (`AvianVoiceSettings`)

| Field | Meaning |
|-------|---------|
| `useSynthTransform` | Synthesized chirps vs sample library |
| `synthPitchRatio` | Human→bird Hz multiplier |
| `synthBirdMinHz` / `Max` | Bird range clamp |
| `synthChirpSeconds` | Chirp duration |
| `voicedThreshold` | RMS to count as voiced (default 0.012) |
| `yinThreshold` / `pitchMinConfidence` | Pitch tracker strictness |
| `loudnessSmooth` | Envelope lag (higher = slower) |

## 6. Code structure and how to change things

Namespaces stay `AvianVoice.Audio`, `AvianVoice.Embodiment`, `AvianVoice.Tasks`, … Folders were grouped for humans; **move `.cs` and `.cs.meta` together**.

```
Assets/AvianVoice/
  Scenes/Avian_Lab.unity
  Settings/AvianVoiceSettings.asset
  Resources/BirdExperience/          ← wav clips
  Scripts/
    Audio/       mic, YIN, synth, melody player, chirp library
    Core/        bootstrap, settings, input, desktop camera
    Tasks/       the six lab tasks + TaskManager
    UI/          wish menu, scene prompt, HUD
    Physiology/  BreathSimulator (guided teacher copy)
    Data/        session / trial logs
    Embodiment/
      Parrot/    player/friend body, wings, flight, beak
      Mirror/    baroque glass, planar mirror camera
      Pond/      water, fish, reflection
      Flock/     Call Friends companions
      Scene/     roles, XR poses, lighting
      Pigeon/    legacy FPV (not the lab parrot)
  Editor/        Avian Voice menu, health check, pond merge
```

### “I want to change…”

| Change | Open |
|--------|------|
| Mic / pitch / chirp feel | `Audio/MicrophoneCapture.cs`, `VoiceAnalyzer.cs`, `BirdVoiceMapper.cs`, `BirdSongSynth.cs`, settings asset |
| Companion song | Replace `Resources/BirdExperience/bird_melody.wav` or `BirdExperienceAudio.PlayMelodyNear` |
| Scene list / wish copy | `UI/SceneWishMenu.cs`, instructions in `Tasks/TaskManager.cs` |
| One task’s rules | Matching `Tasks/*Task.cs` |
| Teacher breath speed | `AvianVoiceSettings` Teacher Breath + `BirdFriendDemonstrator.cs` |
| Player chest / wings | `Embodiment/Parrot/ParrotBreathEmbodiment.cs`, `ParrotBreathWingPose.cs` |
| Flight camera / hide bird | `ParrotFlightPresentation.cs` |
| Flight speed / climb | `FlightControlTask.cs` |
| Mirror | `Embodiment/Mirror/` |
| Pond reflection | `Embodiment/Pond/PondWaterReflection.cs` |
| Flock | `Embodiment/Flock/` |
| Who is player vs friend | `Embodiment/Scene/BirdSceneRoles.cs` (`PlayerParrot` / `FriendParrot`) |
| Keyboard | `Core/AvianInput.cs`, `UI/TaskModeKeyboardSwitcher.cs` |
| Editor setup menus | `Editor/AvianVoiceEditorMenu.cs` |

**Runtime boot:** `AvianAppBootstrap` (mic, analyzer, mapper, breath, body) → `AvianInSceneTaskBootstrap` (six tasks on `TaskRoot`, flight on XR Origin) → `BirdSceneRoles` (find parrots) → `SceneWishMenu` / `SceneGuide`.

**Do:** put new scripts in the feature folder; keep `.meta`; comments in **English**; don’t mix pond + mirror + parrot in one file.  
**Don’t:** delete types still wired to editor menus (`Pigeon*`, old `Task_*` helpers) unless you grep first.

## 7. Layers and cameras

| Layer | ID | Use |
|-------|----|-----|
| `BirdMirror` | 9 | Player bird, drawn by the pond reflection camera |
| UI | 11 | Wish menu, scene prompt |

During Flying, game cameras drop layer 9 from the culling mask and keep layer 11. Reflection cameras (they render to a texture) still see the bird.

## 8. Repos

GitHub remote is typically `origin`. Unity Version Control (Plastic) is used in parallel for Unity-side checkin. Do not commit secrets. Ask before pushing.

---

# 日本語

<a id="japanese"></a>

## 1. このプロジェクトは何か

**Birdflow** は Unity 6 の VR 研究プロトタイプです。プレイ可能な本体は `Assets/AvianVoice/` の **Avian Voice** です。

**CHI 2027** 向けに、次を探っています。

- **鳥の身体錯覚** — 一人称のオウム、胸の呼吸、翼、嘴、池の映り込み、バロック調の鏡。
- **ゲーム入力は声だけ** — マイクが鳥の声・飛行の上昇・音程合わせを動かします。**Space による呼吸入力はありません。**
- **ガイドされる身体** — 教師タイムラインがプレイヤー鳥の胸を膨らませます。一緒に歌う場面だけ相棒オウムが出ます。

プレイヤー鳥はシーン上の `PlayerParrot`。相棒は `FriendParrot`（歌うシーン以外は非表示）。RoleMarker の球はゲームプレイではありません。

## 2. 起動方法

| 項目 | 内容 |
|------|------|
| エンジン | Unity **6000.3.4f1**（Unity 6）、URP |
| XR | OpenXR + XR Interaction Toolkit + XR Hands |
| 実験シーン | `Assets/AvianVoice/Scenes/Avian_Lab.unity` |
| 設定 | `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` |

1. `Avian_Lab.unity` を開いて Play。
2. マイクを許可。Windows ではマイク開始後にオーディオデバイスをリセットしてスピーカーを戻し、キャプチャを一度やり直します。
3. **I want …**（または **M** / XR のメニューボタン）で場面を選ぶ。キーボード **1–6** でも切替。**C** はデバッグ用チート（本番前に `enableDebugCheat` を切る）。

**デスクトップ:** 右クリックで視点、**WASD** 移動（飛行中は WASD 無効。飛行タスクが移動を担当）。高度は **E / Q**。  
デバッグ HUD は `showHudPanels = false` のとき出ません。願いメニューと一行プロンプトは独立しています。

## 3. 入力と出力

**入力:** マイク（主）、願いメニュー / M / XR メニュー、1–6、C、飛行の E/Q、任意の T（Transform / Assistive）。呼吸キーはありません。`BreathSimulator` は教師パターンをコピーします。

**出力:** Transform 時は音程の輪郭を保った短い合成chirp。Assistive は自分の声のモニタ。体験音は `Resources/BirdExperience/`。見た目は胸・翼・嘴・浮き、鏡、池の平面反射。ログは `avian_trials.jsonl`。

## 4. 6 つの場面（ユーザーフロー）

すべて `Avian_Lab` 内。`TaskManager` が 1 本だけ有効にします。

| キー | ID | 願い（表示） | やること |
|------|-----|----------------|----------|
| 1 | `meet` | I want to meet my bird self. | 鏡の自分の鳥を見る。呼吸課題はまだない。約 6 秒で完了。 |
| 2 | `breath_sync` | I want to learn its breath. | 教師タイムラインでプレイヤー鳥の胸が動く。Pattern A（4/4、既定 3 周期）のあと Pattern B（6/6、既定 2 周期）を見る。 |
| 3 | `flying` | I want to fly above the water. | 自動で滑空。声が大きい／高いと上昇、黙ると降下。池に姿が映る。本体はヘッドセットから隠す。 |
| 4 | `singing` | I want to sing as a bird. | 相棒が **`bird_melody.wav`** を歌う。330→392→440→494 Hz を声で保持。 |
| 5 | `call_friends` | I want to call my bird friends. | メロディを聴いて歌い返す。鳥が 1 羽→3 羽→群れ。 |
| 6 | `reflection` | I want to rest by the water. | 水辺で休み、セッションのスコアを見る。 |

推奨順は 1→2→3→4→5→6。願いカードで飛ばしてもよい。

**鏡が付く:** Meet / Breath Sync / Singing / Reflection。  
**池の反射が付く:** Flying / Call Friends。  
**相棒が見える:** Singing のみ。

## 5. 人の声 → 鳥の声

```
マイク → MicrophoneCapture → VoiceAnalyzer（YIN）
  → BirdVoiceMapper
      合成ON: 起音で BirdSongSynth.PlayChirp
      合成OFF: BirdCallLibrary のサンプル
      Assistive: 自分の声のピッチ／リバーブ
```

**音程:** `birdHz = clamp(humanHz × 8, 1600, 5500)`。音程の間隔（メロディの形）はそのまま、鳥の帯域へ上げる。

**リズム:** 固定拍子ではない。無音のあとの起音、または音量の谷→山で次の一声。デバウンスは約 45 ms。`synthChirpSeconds` は **一声の長さ**（約 0.08 秒）。

**体験音:** `bird_melody.wav`（相棒の歌）、`bird_pitch.wav`、`birds_forest.wav`（成功）、`birds_reward.wav`。すべて 2D 再生。

調整は `AvianVoiceSettings` の `synthPitchRatio`、`voicedThreshold`、`yinThreshold` など。

## 6. コードの場所と直し方

名前空間は `AvianVoice.*` のまま。フォルダだけ機能別に分けています。**.cs と .cs.meta はセットで動かす。**

| 変えたいこと | 開くファイル |
|--------------|----------------|
| マイク・音程・chirp | `Audio/MicrophoneCapture.cs`, `VoiceAnalyzer.cs`, `BirdVoiceMapper.cs`, `BirdSongSynth.cs` |
| 相棒の歌 | `Resources/BirdExperience/bird_melody.wav` または `BirdExperienceAudio.cs` |
| 場面の文言 | `UI/SceneWishMenu.cs`, `Tasks/TaskManager.cs` |
| タスク規則 | `Tasks/*Task.cs` |
| 教師の呼吸 | Settings の Teacher Breath、`BirdFriendDemonstrator.cs` |
| 胸・翼 | `Embodiment/Parrot/ParrotBreathEmbodiment.cs` |
| 飛行カメラ | `ParrotFlightPresentation.cs` |
| 飛行速度 | `FlightControlTask.cs` |
| 鏡 / 池 / 群れ | `Embodiment/Mirror/`, `Pond/`, `Flock/` |

コメントは **英語**。新しいスクリプトは機能フォルダへ。エディタメニューが参照している旧 `Pigeon*` / `Task_*` は、grep せずに消さない。

## 7. レイヤー

プレイヤー鳥はレイヤー 9（`BirdMirror`）。飛行中、ゲームカメラはこのレイヤーを隠し、UI（11）は残す。反射カメラはテクスチャへ描くので鳥が見える。

---

# 中文

<a id="chinese"></a>

## 1. 项目是什么

**Birdflow** 是 Unity 6 上的 VR 研究原型，可玩模块是 `Assets/AvianVoice/` 里的 **Avian Voice**。

面向 **CHI 2027** 相关工作，探索：

- **鸟身幻觉**：第一人称鹦鹉身体、胸腔呼吸、翅膀、喙、池塘倒影、巴洛克镜子。
- **嗓音是唯一玩法输入**：麦克风驱动鸟鸣、飞行升高、音高匹配。**没有 Space 呼吸键。**
- **被引导的身体**：教师时间轴驱动玩家鸟的胸部。只有「一起唱」时才显示伙伴鹦鹉。

玩家鸟：场景物体 `PlayerParrot`。伙伴：`FriendParrot`（除唱歌场景外隐藏）。不要把 RoleMarker 小球当玩法。

## 2. 怎么运行

| 项 | 值 |
|----|-----|
| 引擎 | Unity **6000.3.4f1**（Unity 6），URP |
| XR | OpenXR + XR Interaction Toolkit + XR Hands |
| 实验室场景 | `Assets/AvianVoice/Scenes/Avian_Lab.unity` |
| 设置 | `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` |

1. 打开 `Avian_Lab.unity`，按 Play。
2. 允许麦克风。Windows 上会先 `Microphone.Start` 再 `AudioSettings.Reset` 以恢复扬声器，然后重启一次采集。
3. 点 **I want …**（或 **M** / 手柄菜单键）选场景。键盘 **1–6** 也可切换。**C** 是调试作弊（正式实验前关掉任务上的 `enableDebugCheat`）。

**桌面：** 右键环顾，**WASD** 移动（飞行场景禁用 WASD，由飞行任务接管）。高度 **E / Q**。  
`showHudPanels = false` 时不显示调试 HUD；许愿菜单和一行场景提示不受该开关影响。

## 3. 输入与输出

**输入：** 麦克风（主输入）、许愿菜单 / M / XR 菜单、1–6、C、飞行 E/Q、可选 T（Transform / Assistive）。没有呼吸按键。`BreathSimulator` 只复制教师节奏（`Source.Guided`）。

**输出：**

- Transform（默认）：跟你音高轮廓走的、一下一下的合成鸟鸣。
- Assistive：监听自己的人声并变调 / 混响。
- 体验音频：`Resources/BirdExperience/`。
- 身体：胸、翅、喙、发声时轻晃；飞行播 fly 动画。
- 镜子：场景 1、2、4、6。池塘反射：场景 3、5。
- 日志：`Application.persistentDataPath/avian_trials.jsonl`。

## 4. 六个场景：任务与 user flow

六个任务都在 **同一个** `Avian_Lab` 里，由 `TaskManager` 只启用其中一个。

| 键 | ID | 许愿文案 | 玩家要做什么 |
|----|-----|----------|----------------|
| 1 | `meet` | I want to meet my bird self. | 看镜子里的自己。不必呼吸。约 6 秒完成。 |
| 2 | `breath_sync` | I want to learn its breath. | 看玩家鸟按教师节奏呼吸。先 Pattern A（4 秒吸 / 4 秒呼，默认 3 轮），再 Pattern B（6/6，默认 2 轮）。 |
| 3 | `flying` | I want to fly above the water. | 自动向前滑翔。大声或更高的声音上升，安静则下降。低头看水面倒影；本体对头显隐藏。 |
| 4 | `singing` | I want to sing as a bird. | 伙伴播放 **`bird_melody.wav`**。用嗓音保持 330→392→440→494 Hz。 |
| 5 | `call_friends` | I want to call my bird friends. | 听旋律再唱回去，召来 1 只 → 3 只 → 鸟群。 |
| 6 | `reflection` | I want to rest by the water. | 在水边休息，看本局分数。 |

推荐顺序 1→6，也可用许愿卡跳转。

脚本：`MeetBirdTask`、`BreathSyncTask`、`FlightControlTask`、`SingingBranchTask`、`CallFriendsTask`、`ReflectionTask`。

## 5. 人声如何变成鸟叫

```
麦克风 → MicrophoneCapture（Reset 扬声器后重启一次）
      → VoiceAnalyzer（响度 RMS + YIN 音高）
      → BirdVoiceMapper
           合成开：起音 → BirdSongSynth.PlayChirp
           合成关：BirdCallLibrary 采样 chirp
           Assistive：人声监听 + Mixer
```

**音调一致（旋律轮廓）：**

```
鸟鸣 Hz = clamp(人声 Hz × synthPitchRatio, 1600, 5500)
```

默认 **×8**（高三个八度）。220 Hz → 1760 Hz，440 Hz → 3520 Hz，音程关系不变。

**节奏（一下一下，不跟节拍器）：**  
在「从静音起音」或「响度先落再升」（连唱两下）时立刻叫。约 45 ms 防抖，避免同一声打两次。`synthChirpSeconds`（约 0.08 秒）是 **一声的时长**，不是两声之间的间隔。

**场景配乐（不是玩家嗓音）：**

| 文件 | 用途 |
|------|------|
| `bird_melody.wav` | 伙伴 / 召群的**歌曲**（唱歌、召朋友） |
| `bird_pitch.wav` | 音高短语备用 |
| `birds_forest.wav` | 短成功提示 |
| `birds_reward.wav` | 较长奖励铺底 |

均为 2D 播放。Windows 恢复扬声器后若旋律应在播，会自动再播一次。

调参：`AvianVoiceSettings.asset` 里 `synthPitchRatio`、`voicedThreshold`（默认 0.012）、`yinThreshold`、`loudnessSmooth`。

## 6. 代码结构与怎么改

命名空间仍是 `AvianVoice.*`，只是按功能分了文件夹。**移动脚本必须带着 `.meta`。**

```
Assets/AvianVoice/Scripts/
  Audio/         麦、音高、合成、旋律播放
  Core/          启动、设置、输入、桌面相机
  Tasks/         六个任务 + TaskManager
  UI/            许愿菜单、场景提示、HUD
  Physiology/    呼吸（只跟教师）
  Data/          试验日志
  Embodiment/Parrot/   玩家/伙伴身体、翅膀、飞行
  Embodiment/Mirror/   镜子
  Embodiment/Pond/     池塘、鱼、水面反射
  Embodiment/Flock/    召朋友的鸟群
  Embodiment/Scene/    角色、XR 位姿
  Embodiment/Pigeon/   旧鸽子 FPV（不是实验室鹦鹉）
```

| 想改什么 | 打开 |
|----------|------|
| 麦 / 鸟鸣手感 | `Audio/MicrophoneCapture.cs`、`VoiceAnalyzer.cs`、`BirdVoiceMapper.cs`、`BirdSongSynth.cs` |
| 伙伴的歌 | 替换 `bird_melody.wav` 或改 `BirdExperienceAudio.cs` |
| 场景文案 | `UI/SceneWishMenu.cs`、`Tasks/TaskManager.cs` |
| 某一关规则 | 对应 `Tasks/*Task.cs` |
| 教师呼吸快慢 | Settings 的 Teacher Breath、`BirdFriendDemonstrator.cs` |
| 胸和翅膀 | `Embodiment/Parrot/ParrotBreathEmbodiment.cs` |
| 飞行镜头 / 藏鸟 | `ParrotFlightPresentation.cs` |
| 飞行速度 | `FlightControlTask.cs` |
| 镜 / 池 / 群 | `Embodiment/Mirror/`、`Pond/`、`Flock/` |

启动链：`AvianAppBootstrap` → `AvianInSceneTaskBootstrap` → `BirdSceneRoles` → 许愿菜单。  
代码注释用 **英文**。新文件放到对应功能目录。不要混写池塘、镜子、鹦鹉逻辑。不要在未 grep 的情况下删除仍被编辑器菜单引用的旧类型。

## 7. 图层

玩家鸟在第 9 层 `BirdMirror`。飞行时主摄像机去掉第 9 层、保留 UI 第 11 层。反射相机画到 RenderTexture，仍能画出鸟，所以水面看得到倒影。

## 8. 协作约定

- 主场景只维护 `Avian_Lab.unity`。
- 提交前不要把 `_Recovery`、本机 XR 缓存等塞进版本库。
- GitHub 与 Unity Version Control（Plastic）可并行；推送前先问。
- 更短的「改哪」表见 [`Scripts/README.md`](Scripts/README.md)。
