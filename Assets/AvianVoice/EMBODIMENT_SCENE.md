# Avian_Lab_Embodiment — 体トラッキングで動くアバターとアバター切替

`Assets/AvianVoice/Scenes/Avian_Lab_Embodiment.unity` は `Avian_Lab.unity` のコピーに、次の点を足した実験用シーンです。

1. **Quest の体トラッキング（IOBT / Full Body）** で、ユーザーの体の向き・腕・（対応機種では）脚がアバターに反映される。
2. **視点がアバターの目に一致する**（アバターの目が XR カメラの位置に来るように毎フレーム合わせる）。
3. **アバター切替モード**: 鳥（`PlayerParrot`）⇄ 人間。人間は **男性 / 女性 / 中性** の 3 体から選べる
   （鳥との embodiment の差を、人間アバターの性別の影響と分けて調べるため）。
4. **4 場面の実験フロー**: 鳥に会う → 呼吸 → 歌う → 休む（元の 6 場面のうち「飛ぶ」「仲間を呼ぶ」は使わない）。
5. **歌う場面は試行形式**: 実験者がボタンを押す → 教師の鳥が 1 音歌う → 被験者が歌い返す → 実験者が次を押す。
   1 試行 = ボタンを押してから次に押すまで（待ち時間なし）。3 ピッチ × 20 回 = 60 試行。
   60 回終わると自動で「休む」に移って終了。
6. **記録**: 全イベントのタイムスタンプ、試行ごとの CSV、被験者マイク音声と提示音声の WAV をセッションごとに保存。

シーン内の `Avian Embodiment` オブジェクトに `AvatarEmbodimentController`（体トラッキング・アバター）と
`StudyFlowController`（4 場面フロー・試行タスク・記録）が付いています。元の `Avian_Lab` は 6 場面のまま変わりません。

> **注意**: 体トラッキングとアバター切替は **Avian_Lab_Embodiment** を開いて Play したときだけ動きます。
> 元の `Avian_Lab` を Play しても鳥は止まり木に乗ったままで、ユーザーの体には追従しません。
> メニュー **Avian Voice → Embodiment → Open Avian_Lab_Embodiment** で開けます。

## 操作（実験者は PC、被験者はヘッドセット）

被験者のコントローラ入力は **既定で無効**です（`Avian Embodiment` の `Participant Controllers Enabled`）。
スティック旋回、メニューボタン、X / B ボタンはすべて無視され、被験者は頭・手・体・声だけで参加します。
XR リグの手メッシュ・関節・レイ表示も既定で非表示です（`Participant Hands Visible`、トラッキング自体は動き続け、
翼や人間アバターの腕がその代わりに見えます）。

実験者は PC 側で操作します。メニュー **Avian Voice → Experimenter Panel (実験者パネル)** を開くと、Play 中に

- 場面 1〜4 のボタン / 前の場面・次の場面
- 歌う場面の試行: 「試行 n を開始」ボタン（被験者の Space / R と同じ）、進捗バー、やり直し、リセット
- アバター: 鳥 / 人間
- 被験者コントローラの有効/無効、手の表示、視界下の状態カードの表示
- 現在の場面・アバター・体トラッキング状態の表示、**音声出力メーター**、記録の状態、詳細ログ出力

Play の前に **被験者 ID** をパネルで入力しておくと、記録フォルダ名に入ります。

### 歌う場面の試行（3 ピッチ × 20 回）

1. 場面 3「歌う」に切り替える。被験者には「次の音を待ってください」と出ます。
2. パネルの **「▶ 試行 n を開始 — 教師の鳥が E4 を歌う」** を押す（または Game ビューで Space）。
   教師の鳥が目標の音を約 0.9 秒歌い、0.3 秒おいて「今度は E4 を歌ってください」に変わります。
3. 被験者が歌う。声の始まり（60 ms 以上有声）と終わり（250 ms 以上無声）を検出して記録します
   （複数回発声してもすべて残ります）。応答中は自分の声が鳥の声として聞こえます。
4. 実験者が **次のボタン（「▶ 次の試行 n+1」/ Space）を押した瞬間に試行が終わり、次の試行が始まります**。
   待ち時間はなく、教師の鳥が歌っている最中でも押せます（その場合は試行が短く記録されるだけです）。
   つまり `trials.csv` の 1 行は「ボタンを押してから次に押すまで」の区間です（`t_start_*` 〜 `t_end_*`、
   `end_reason = experimenter_next`）。「終了 (待機)」で試行を閉じて待機に戻すこともできます（`experimenter_end`）。
5. 60 回目の試行の後にもう一度押すと（ボタンは「■ 最後の試行を終了 → 休む場面へ」になります）
   「お疲れさまでした」と出て、3 秒後に自動で「休む」場面へ移ります。

`SingingTrialTask` の **Trial Until Next Press** を外すと以前の動作（応答は最長 5 秒、声が止まって 1 秒で自動終了、
教師の鳥が歌っている間はボタン無効）に戻ります。

ピッチ（既定 E4 330 / G4 392 / B4 494 Hz）、各ピッチの回数、順序（ブロック / 交互 / ランダム）は最初の試行を始める前なら
パネルか `TaskRoot` の `SingingTrialTask` で変えられます。教師の音は鳥の音域に移調されますが、被験者はどのオクターブで歌っても
同じ音名なら一致と判定します（`pitch_match`）。「直前の試行をやり直す」で同じ目標をもう一度行えます（`attempt` が増え、両方とも記録に残ります）。

### 記録データ

`StudyRecorder` が Play 開始から終了までを `persistentDataPath/AvianStudy/<日時>_<被験者ID>/` に書きます
（Windows では `%USERPROFILE%\AppData\LocalLow\<会社名>\<製品名>\AvianStudy\…`。パネルの「フォルダを開く」で開けます）。

| ファイル | 内容 |
|----------|------|
| `session.json` | 被験者 ID、開始時刻、Unity / 機器 / マイク一覧、サンプリング周波数、時刻基準の説明 |
| `events.jsonl` | 1 行 1 イベント: 場面切替、アバター切替、`trial_start` / `guide_start` / `guide_end` / `response_start` / `voice_onset` / `voice_offset` / `trial_end`、`protocol_complete` など |
| `trials.csv` | 試行ごとに 1 行: 目標周波数・音名、各フェーズのタイムスタンプ、発声開始までの遅延、発声時間、平均 / 中央値ピッチ、一致度、最大音量、終了理由、切り出し音声ファイル名 |
| `mic.wav` | 被験者マイク（セッション全体、モノラル 16 bit） |
| `output.wav` | 被験者が聞いた音（AudioListener のミックス、モノラル 16 bit）。教師の音がここに入ります |
| `segments/trial_NNN_aN_<音名>.wav` | 試行開始〜終了のマイク音声の切り出し |

すべてのタイムスタンプは、壁時計（UTC / ローカル、ISO 8601）、`Time.realtimeSinceStartup`、`AudioSettings.dspTime`、
`mic.wav` のサンプル番号（`mic_sample`）、`output.wav` のフレーム番号（`out_frame`）を同時に持つので、音声ファイルを
イベントに合わせて正確に切り出せます。マイクが途中で再起動した場合は `mic_restart` イベントが入ります。

### 音が聞こえないとき

パネルの「音声出力 (PC 側)」メーターが振れていれば Unity は音を出しています。聞こえないのは出力先の問題です。
Unity の音は **Windows の既定の再生デバイス**に出ます（ログの `XR: Error setting active audio output driver. Falling back to default.`
はヘッドセットへの自動切替が失敗して既定デバイスに落ちた印）。Link でヘッドセット側に聞かせるには、Windows の
サウンド設定で既定の出力を「Headphones (Oculus Virtual Audio Device)」にするか、Meta Quest Link アプリの
設定でリンク時にオーディオを切り替えるようにしてください。PC スピーカーで聞く場合はその逆です。

### 人間アバター（男性 / 女性 / 中性）

| Id | モデル | 出典・実績 | 置き場所 |
|----|--------|------------|----------|
| `male` | Microsoft Rocketbox `Male_Adult_01` | Rocketbox ライブラリ（MIT）。Gonzalez-Franco et al. 2020, *Frontiers in VR*。Maselli & Slater 2013 以来、VR の身体所有感・embodiment 研究（IEEE VR / CHI / UIST 系）の標準的な刺激 | `Resources/Avatars/Rocketbox/Male_Adult_01` |
| `female` | Microsoft Rocketbox `Female_Adult_01` | 同上（同じ骨格・同じ描画設定なので、性別以外の条件を揃えやすい） | `Resources/Avatars/Rocketbox/Female_Adult_01` |
| `neutral` | MakeHuman（MPFB2）で生成する中性体 | MakeHuman（CC0）は CHI / IEEE VR の embodiment 研究で広く使われる無料のヒューマンジェネレータ。性別スライダ 0.5、胸なし、短髪、化粧なしの肌、無地の T シャツとジーンズという Nag & Yalçın (2020) の中性エージェント指針に沿った設定。Rocketbox にも VALID にも中性モデルは無い | `Resources/Avatars/MakeHuman/Neutral_Adult_01`（下記の手順で生成） |

実験者パネルの「人間 (男性 / 女性 / 中性)」ボタンで切り替えます（V キーは鳥 ⇄ 選択中の人間）。`Avian Embodiment` の
`AvatarEmbodimentController` → `Human Avatars` に 3 体の id / 表示名 / Resources パスがあり、モデルが無い id はパネルに
「未配置」と出ます。記録の `events.jsonl` / `trials.csv` には `Human:female` のように id 付きで残ります。

**中性アバターの生成**（生成済みのものがリポジトリに入っています。作り直すときだけ。Blender 4.2 以上 + MPFB2 拡張 +
MakeHuman system assets が必要。2026-11-04 に Blender 5.1.2 + MPFB2 2.0.18 で生成）

1. Blender に MPFB2 を入れる: Edit → Preferences → Get Extensions →「MPFB」→ Install。
2. https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip をダウンロード。
3. リポジトリのルートで次を実行（Blender のパスは環境に合わせる）:

```
"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b --python Tools\MakeHuman\generate_neutral_avatar.py -- --out Assets\AvianVoice\Resources\Avatars\MakeHuman\Neutral_Adult_01 --asset-zip <zip のパス>
```

`Neutral_Adult_01.fbx` とテクスチャが書き出され、Unity が `MakeHumanAvatarImport.cs` で Humanoid として取り込みます。
T シャツのテクスチャにある MakeHuman のロゴはスクリプトが塗りつぶします（`--keep-logo` で残せます）。
体型・肌・髪・服はスクリプトの引数（`--gender 0.5 --cupsize 0 --skin young_asian_male.mhmat --hair short04.mhclo
--clothes male_casualsuit06.mhclo` など）で変えられ、設定は出力先の `GENERATED.md` に残ります。

人間アバターは専用レイヤー **HumanAvatar (12)** に置かれます。鏡と池の反射用の補助ライト（鳥向けに強め）はこのレイヤーに
当たらないため、白飛びしません。髪（`m002_opacity`）はアルファカットアウト、肌は smoothness 0.3 で再インポートされます
（`Avian Voice → Embodiment → Reimport Human Avatar`）。

ができます。キーボードでも同じ操作ができます（Game ビューにフォーカスがある時）。

| したいこと | PC キーボード | 実験者パネル |
|------------|---------------|--------------|
| 場面を変える | **1〜4**、**N**（次）、←（前） | 場面ボタン |
| 歌う場面で次の試行を始める | **Space** / **R** | 試行を開始ボタン |
| アバターを切り替える | **V** | 鳥 / 人間ボタン |
| コントローラを一時的に有効化 | — | トグル |

視界の下に「アバター: 鳥 / 人間 … 体トラッキング: 全身 / 上半身 / なし」のカードが出ます（パネルまたは `showSwitchHint` で非表示可）。

## 初回セットアップ

1. プロジェクトを開くと Package Manager が **Meta XR Core SDK (com.meta.xr.sdk.core 207.0.1)** を
   `https://npm.developer.oculus.com` から取得します（`Packages/manifest.json` に scoped registry を追加済み）。
2. メニュー **Avian Voice → Embodiment → Configure Everything** を実行する。これで
   - Rocketbox FBX を Humanoid として再インポート（`RocketboxAvatarImport.cs`）
   - OpenXR の **Meta XR Feature** を Android / Standalone で有効化
   - `OVRProjectConfig.bodyTrackingSupport = Supported`（ビルド時に `com.oculus.permission.BODY_TRACKING` と
     `com.oculus.software.body_tracking` が AndroidManifest に入る）
   - `OVRRuntimeSettings`: Joint Set = **Full Body**, Fidelity = **High**
   - シーンに `AvatarEmbodimentController` があることを確認、Build Settings に登録
3. Android (Quest) でビルドして実機で起動。初回に体トラッキングの許可ダイアログが出ます。
   Quest 本体側で **設定 → 動作とトラッキング → ボディトラッキング** が有効になっている必要があります。

エディタ再生（Link なし）では体トラッキングは無く、鳥は頭の向きに合わせて体が遅れて回り、人間アバターは腕を下ろした姿勢で
頭だけがカメラに追従します。

## 仕組み（スクリプト）

| ファイル | 役割 |
|----------|------|
| `Scripts/BodyTracking/*` (asmdef `AvianVoice.BodyTracking`) | SDK 非依存の関節データ `BodyPoseFrame`、バックエンド IF、トラッキング空間→ワールドの推定 `TrackingSpaceSolver` |
| `Scripts/MetaBody/MetaBodyTrackingBackend.cs` (asmdef `AvianVoice.MetaBody`, SDK がある時だけコンパイル) | `OVRBody` から関節を取得。人間アバターには `OVRUnityHumanoidSkeletonRetargeter` を付けて Humanoid にリターゲット |
| `Scripts/MetaBody/Editor/MetaBodyTrackingProjectSetup.cs` | 上記の OpenXR / Oculus 設定を一括適用 |
| `Scripts/Embodiment/Avatar/AvatarEmbodimentController.cs` | アバター切替、カメラ・手の位置から体トラッキング空間を合わせる、ヒント表示 |
| `Scripts/Embodiment/Avatar/BirdFirstPersonDriver.cs` | 鳥: 目をカメラに、頭を HMD の向きに、体の向きを肩のラインに、翼を腕の上げ下げに |
| `Scripts/Embodiment/Avatar/HumanFirstPersonDriver.cs` | 人間: リターゲット結果のルートをトラッキング原点に置く。トラッキング無しのフォールバック姿勢 |
| `Scripts/Embodiment/Avatar/FirstPersonHeadHider.cs` | 一人称カメラだけ頭・髪のマテリアルを非表示（鏡・池の反射には映る） |
| `Scripts/Study/StudyFlowController.cs` | 4 場面フロー（`TaskManager.SetFlow`）、`SingingTrialTask` の割り当て、`StudyRecorder` の開始、アバター切替の記録 |
| `Scripts/Study/SingingTrialTask.cs` | 試行形式の歌う場面（教師の 1 音 → 応答、発声検出、60 回で休む場面へ） |
| `Scripts/Study/StudyRecorder.cs` | `events.jsonl` / `trials.csv` / `mic.wav` / `output.wav` / 試行切り出しの書き出し |
| `Scripts/Study/ListenerOutputTap.cs`, `WavWriter.cs` | AudioListener のミックス取得、16 bit WAV ストリーム書き込み |
| `Resources/Avatars/Rocketbox/` | 人間アバター 男性・女性（MIT ライセンス、`README.md` 参照） |
| `Resources/Avatars/MakeHuman/`, `Tools/MakeHuman/generate_neutral_avatar.py` | 中性アバター（MakeHuman / CC0、生成スクリプト） |
| `Editor/MakeHumanAvatarImport.cs` | MakeHuman FBX の Humanoid 取り込みとマテリアル設定 |

鳥の「目」の位置は `BirdFirstPersonDriver.eyeOffsetFromHeadBone`（頭ボーン基準、ルート座標系）で調整できます。
人間アバターを追加するには、Humanoid モデルを Resources 配下に置いて `Human Avatars` に id と Resources パスを足してください。

## 動作確認とトラブルシュート

Play 中にメニュー **Avian Voice → Embodiment → Log Avatar State (Play Mode)** と **Log Body Tracking Diagnostics (Play Mode)**
を実行すると、コンソールに次のような 1 行が出ます。

```
[AvianVoice] Avatar state: avatar=Bird ... bodyValid=True conf=1.00 ... torsoYaw=-30 armElev=78/95 birdYaw=330 ...
[AvianVoice] Body tracking data started: OVRBody=enabled xrApi=OpenXR supported=True trackerEnabled=True inputFocus=True ... joints=84 valid=84
```

- `inputFocus=False` / `state: none` のまま → ヘッドセットを装着していない（近接センサー）か、Link アプリの開発者ランタイム機能
  （Body tracking over Meta Quest Link）が無効。
- `bodyValid=True` なのに動かない → 開いているシーンを確認（上の注意）。
- 視界下のカードにも「体トラッキング: 上半身 / 全身 / なし (理由)」が常時出ます。
- **Toggle Avatar (Play Mode)** メニューで V キーと同じ切替ができます。

Quest Link 経由のエディタ再生で、鳥・人間とも体トラッキングで動くことを確認済みです（2026-11-03、Quest 3、上半身ジョイントセット）。

## 既知の制限

- IOBT の脚トラッキングは Quest 3 / Quest Pro 系で、Quest 2 は上半身のみ（自動判定、ヒントに「上半身」と出ます）。
- 体トラッキングと「ハンドとコントローラの同時使用」は併用不可（`OVRManager.SimultaneousHandsAndControllersEnabled` は off のまま）。
- 鳥アバターは等身大のままなので、ユーザーの目線の高さに浮いて見えます（元シーンの止まり木とは別の体験になります）。
- 飛行場面（4）では元の `ParrotFlightPresentation` が主カメラから鳥を隠す設定を引き継いでいます。

---

# English summary

`Avian_Lab_Embodiment.unity` is a copy of `Avian_Lab.unity` plus: Meta Quest body tracking (IOBT) driving the
avatar, the avatar eyes locked to the XR camera, and an avatar switch (bird `PlayerParrot` ⇄ human). Three human
bodies are selectable from the experimenter panel: Microsoft Rocketbox `Male_Adult_01` and `Female_Adult_01`
(MIT; the standard rigged avatars of VR embodiment research) and a gender-neutral MakeHuman body (CC0, gender
slider 0.5, no breasts, short hair, plain T-shirt and jeans, following Nag & Yalcin 2020) generated with
`Tools/MakeHuman/generate_neutral_avatar.py`. Toggle bird / human with **V** (keyboard) or the **left X** button.

The study flow is four scenes (meet, breath, singing, rest). The singing scene is experimenter-paced: each
press of the panel button (or Space) makes the teacher bird sing one of three target pitches and the participant
sings back; the participant's voice onset / offset are detected. A trial lasts from one press to the next (no
wait, the button is always available), so each row of `trials.csv` is the interval between two presses; 3 pitches
x 20 = 60 trials, and the press after the last one moves the session to the rest scene automatically. `StudyRecorder` writes `events.jsonl`, `trials.csv`,
`mic.wav`, `output.wav` and per-trial microphone segments under `persistentDataPath/AvianStudy/<date>_<id>/`,
every timestamp carrying wall clock, Unity time, DSP time and the microphone sample index.

Setup: open the project (Meta XR Core SDK 207.0.1 is pulled from the Meta scoped registry), then run
**Avian Voice → Embodiment → Configure Everything**, build for Android and allow body tracking on the headset.
Without a headset the avatars follow the head only. See the table above for the scripts involved.
