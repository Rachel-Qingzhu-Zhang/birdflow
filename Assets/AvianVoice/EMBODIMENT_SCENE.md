# Avian_Lab_Embodiment — 体トラッキングで動くアバターとアバター切替

`Assets/AvianVoice/Scenes/Avian_Lab_Embodiment.unity` は `Avian_Lab.unity` のコピーに、次の点を足した実験用シーンです。

1. **Quest の体トラッキング（IOBT / Full Body）** で、ユーザーの体の向き・腕・（対応機種では）脚がアバターに反映される。
2. **視点がアバターの目に一致する**（アバターの目が XR カメラの位置に来るように毎フレーム合わせる）。
3. **アバター切替モード**: 鳥（`PlayerParrot`）⇄ 人間。人間は **中性のマネキン 1 体**（Unity Starter Assets の
   ロボット型アバター。Meta が体トラッキングのサンプルで使っている体で、顔・髪・服・性差がない）。
   **実験条件は アバター（鳥 / 人間）× 声のフィードバック（自分の声がアバターの声に変換されて聞こえる / 自分の声しか聞こえない）の 4 条件**。
4. **4 場面の実験フロー**: 鳥に会う → 呼吸 → 歌う → 休む（元の 6 場面のうち「飛ぶ」「仲間を呼ぶ」は使わない）。
   流れの外に **練習場面**（2026-11-07 追加）があり、Play はそこから始まります。記録は場面 1 に進んだ時点から（下記）。
5. **歌う場面は試行形式**: 実験者がボタンを押す → 教師の鳥が 1 音歌う → 被験者が歌い返す → 実験者が次を押す。
   1 試行 = ボタンを押してから次に押すまで（待ち時間なし）。3 ピッチ × 10 回 = 30 試行。
   30 回終わると自動で「休む」に移って終了。
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

実験者は PC 側で操作します。メニュー **Avian Voice → Experimenter Panel** を開くと、Play 中に
（パネルの表示は英語です。被験者がヘッドセットで見る文言は日英のままです）

- 場面 1〜4 のボタン / 前の場面・次の場面、その上に流れの外の **Practice** ボタン（キー 0）
- Practice 欄: 練習場面で教師の音程を自由に選んで鳴らす（下記「練習場面」）
- 歌う場面の試行: 「Start trial n」ボタン（被験者の Space / R と同じ）、進捗バー、やり直し（Repeat the last trial）、リセット
- 条件: 鳥 + 鳥の声 / 鳥 + 自分の声のみ / 人間 + 人間の声 / 人間 + 自分の声のみ（4 つのボタン）
- 被験者コントローラの有効/無効、手の表示、視界下の状態カードの表示
- 現在の場面・アバター・体トラッキング状態の表示、**音声出力メーター**、記録の状態、詳細ログ出力

Play の前に **Participant ID** をパネルで入力しておくと、記録フォルダ名に入ります。

### 練習場面（実験条件の説明用、2026-11-07）

練習場面は 4 場面の流れ（1〜4）の**外**にある独立した場面で、4 条件を始める前に一度だけ使います。Play すると
まずこの場面になります（`StudyFlowController` の `Start In Practice`）。パネルの Scene flow の **Practice** ボタン
（キー **0**）でいつでも入れ、場面 1〜4 のボタンで出ます。「次の場面」は練習からは場面 1 へ進みます。
歌う場面と同じ場所・同じ進み方（実験者が押す → 教師の鳥が歌う → 被験者が歌い返す）ですが、スケジュールも終わりも
なく、教師の音程はパネルの **Practice** 欄で自由に選べます（3 つの目標音程のボタン、または任意の Hz）。説明しながら
**Condition** 欄でアバターと声のフィードバックを切り替えて 4 条件を体験させ、場面 1（鳥に会う）へ進むと本番です。
**記録（events.jsonl / mic.wav / output.wav / trials.csv）は場面 1 に進んだ時点で始まる**ので、練習は記録されません
（本番が始まったあとに練習へ戻った場合だけ、`task: "practice"`、`practice: true` のイベントとして残り、`trials.csv` と
`segments/` には入りません）。実装は `SingingTrialTask` の 2 つ目のインスタンス（`IsPractice`）と `TaskManager.SetFlow`
の side task で、`Include Practice` をオフにすると練習場面なし（従来どおり Play 直後から記録）に戻ります。

### 4 条件（アバター × 声のフィードバック）

パネルの **Condition** に 4 つのボタンがあります（`StudyFlowController.SetCondition`）。

| ボタン | アバター | 被験者が聞く自分の声 | 記録（`voice_feedback` / `condition`） |
|--------|----------|----------------------|------------------------------------------|
| Bird + bird voice | 鳥 | 鳥の声（`BirdSongSynth`、声が出ている間ずっと音程に追従して鳴る） | `bird` / `Bird+bird` |
| Bird + own voice only | 鳥 | なし（アプリは何も出さない。地声だけ） | `none` / `Bird+own` |
| Human + human voice | 人間（マネキン） | 人間の声（`HumanVoiceSynth`、同じ音程で歌う母音「あ」） | `human` / `Human:neutral+human` |
| Human + own voice only | 人間（マネキン） | なし | `none` / `Human:neutral+own` |

- 声のフィードバックは `BirdVoiceMapper.SetFeedback` で切り替わり、歌う場面の応答中だけでなく、どの場面でも同じ設定です。
  V キーで鳥 ⇄ 人間を切り替えても「変換あり / なし」は保たれます（鳥なら鳥の声、人間なら人間の声になります）。
- 教師の鳥の手本（guide note）は **どの条件でも鳥の声**です（`SingingTrialTask.teacherVoice = Bird`）。
  人間アバターのときに手本も人間の声にしたい場合は `Teacher Voice` を `FollowAvatar` にします（`events.jsonl` の
  `guide_start` に `teacher_voice` が残ります）。
- 鳥の声は 2026-11-07 から人間の声と同じ連続型です（有声の間ゲートが開き、音程・音量に追従。止まると減衰）。
  以前の「声の立ち上がりごとに約 80 ms のさえずりを 1 回鳴らす」離散型はコードに残してあり、
  `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` の `Synth Continuous` をオフにすると戻ります。
  どちらで記録したかは `events.jsonl` の `voice_feedback` イベントの `bird_feedback`（`continuous` / `chirps`）に残ります。
- 時間応答（2026-11-07）: 声のフィードバックは解析器の音量エンベロープだけでゲートします（立ち上がり 2 ms、
  減衰の時定数 12 ms、`AvianVoiceSettings.loudnessAttackSeconds / loudnessReleaseSeconds`）。合成器の減衰の時定数は 15 ms
  （-30 dB まで約 50 ms）、音程の平滑化は 20 ms です。実測（`Debug/Test Voice Response`: 0.5 秒の合成音を解析器に
  流し込み、`output.wav` と `debug_voice_inject` イベントで測定）では、入力停止から -30 dB まで鳥 100 ms / 人 90 ms、
  入力開始から発音まで約 60 ms（変更前は減衰 220〜245 ms）。
- 使用中のマイクはパネルの Status 欄「Microphone in use」に出ます（機器名・サンプリングレート・入力レベル）。
  「Input device」で機器を選ぶとその場で切り替わり、次回以降も記憶されます（Auto = XR 中はヘッドセットのマイク）。
- 人間の声（`HumanVoiceSynth`）の音量は鳥の手本と同じ約 -11 dBFS に合わせてあります（2026-11-05 に `output.wav` で実測。
  変えるときは `HumanVoiceSynth.volume`）。鳥 / 人間の声の条件で聞こえる大きさが揃うようにしています。
- `events.jsonl` の全イベントに `voice_feedback`、`trials.csv` に `voice_feedback` 列、条件を変えたときに
  `voice_feedback` イベント（`condition` 付き）が入ります。

### 歌う場面の試行（3 ピッチ × 10 回）

1. 場面 3「歌う」に切り替える。被験者には「次の音を待ってください」と出ます。
2. パネルの **「▶ Start trial n / 30 — teacher bird sings E4」** を押す（または Game ビューで Space）。
   教師の鳥が目標の音を約 0.9 秒歌い、0.3 秒おいて「今度は E4 を歌ってください」に変わります。
3. 被験者が歌う。声の始まり（60 ms 以上有声）と終わり（250 ms 以上無声）を検出して記録します
   （複数回発声してもすべて残ります）。応答中は条件に応じて自分の声が鳥の声 / 人間の声として聞こえます（または何も出ません）。
4. 実験者が **次のボタン（「▶ Next trial n+1」/ Space）を押した瞬間に試行が終わり、次の試行が始まります**。
   待ち時間はなく、教師の鳥が歌っている最中でも押せます（その場合は試行が短く記録されるだけです）。
   つまり `trials.csv` の 1 行は「ボタンを押してから次に押すまで」の区間です（`t_start_*` 〜 `t_end_*`、
   `end_reason = experimenter_next`）。「End (idle)」で試行を閉じて待機に戻すこともできます（`experimenter_end`）。
5. 30 回目の試行の後にもう一度押すと（ボタンは「■ End the last trial → Rest scene」になります）
   「お疲れさまでした」と出て、3 秒後に自動で「休む」場面へ移ります。

`SingingTrialTask` の **Trial Until Next Press** を外すと以前の動作（応答は最長 5 秒、声が止まって 1 秒で自動終了、
教師の鳥が歌っている間はボタン無効）に戻ります。

ピッチ（既定 E4 330 / G4 392 / B4 494 Hz）、各ピッチの回数、順序（ブロック / 交互 / ランダム）は最初の試行を始める前なら
パネルか `TaskRoot` の `SingingTrialTask` で変えられます。教師の音は鳥の音域に移調されますが、被験者はどのオクターブで歌っても
同じ音名なら一致と判定します（`pitch_match`）。「Repeat the last trial」で同じ目標をもう一度行えます（`attempt` が増え、両方とも記録に残ります）。

### 記録データ

`StudyRecorder` が記録開始から Play 終了までを **プロジェクト内の `StudyData/<日時>_<被験者ID>/`**（`Assets` と同じ階層。
`.gitignore` に入っているので git では共有されず、`Assets` の外なので Unity にも取り込まれません）に書きます。
パネルの「Open the recordings folder」で開けます。ヘッドセット単体のビルドでは `persistentDataPath/StudyData/` です。

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

### 音が聞こえないとき（PC スピーカー / ヘッドセット）

パネルの「Audio output (PC)」メーターが振れていれば Unity は音を出しています。聞こえないのは出力先の問題です。

- Unity の音は **Windows の既定の再生デバイス**に出ます（ログの `XR: Error setting active audio output driver.
  Falling back to default.` はヘッドセットへの自動切替が失敗して既定デバイスに落ちた印）。
- パネルの **「Windows default output」** にそのデバイス名・音量・ミュート状態（MUTED）が出ます。ミュート中なら警告と
  「Unmute the Windows output」ボタンが出ます（2026-11-05 に PC から音が出なかった原因はこれでした: Realtek スピーカーが
  Windows 側でミュート）。Unity の Game ビューの Mute Audio も同様に検出します。
- **Unity は Windows の既定出力に従わないことがあります。** Quest Link が有効だと Oculus ランタイムが Unity の出力を
  「ヘッドホン (Oculus Virtual Audio Device)」に付け替えます（Windows の既定が Realtek スピーカーのままでも）。パネルの
  **「Unity output device」** に Unity のオーディオセッションが実際に乗っているデバイスとそのミュート状態が出ます
  （2026-11-05 に PC から音が出なかった 2 つ目の原因: Unity は Oculus 仮想デバイスに出ていて、それが Windows 側でミュート）。
  ミュートなら「Unmute Unity's output device」ボタンで解除できます（ヘッドセット側に聞かせるにはこれが必要です）。
- **PC monitor output**（`PcAudioMonitor`、AudioListener に自動で付きます）: Unity の出力をそのまま PC のスピーカー
  （Oculus / Virtual でない最初の waveOut デバイス）にも流します。既定の **Auto** は「Unity の実際の出力先（上の Unity output
  device、見つからなければ Windows の既定出力）がヘッドセット（Oculus Virtual Audio Device など）のときだけ」ミラーするので、
  Link でヘッドセットに音を出しつつ実験者も PC で聞けます。Unity が PC スピーカーに出ているときは二重になるのでミラーしません
  （判定はミラー自身の出力ストリームを除外し、Active なセッションを優先します）。**Always** で常時、**Off** で停止。「Device (part of the name)」で
  デバイスを指定できます。遅延は 0.1 秒程度です。
- Link でヘッドセット側に聞かせるには、Windows のサウンド設定で既定の出力を「Headphones (Oculus Virtual Audio Device)」に
  するか、Meta Quest Link アプリの設定でリンク時にオーディオを切り替えるようにしてください（PC モニター出力が Auto で
  PC スピーカーにも出ます）。

### 人間アバター（中性マネキン 1 体）

| Id | モデル | 出典・ライセンス | 置き場所 |
|----|--------|------------------|----------|
| `neutral` | Unity **Starter Assets の Armature**（白いロボット型マネキン。顔・髪・服・性差なし、指まで Humanoid にマッピング済み） | Unity Companion License（`LICENSE.md` 同梱）。Meta が **Movement SDK の体トラッキングサンプルの「Stylized」キャラクター**としてそのまま配布・使用しているモデルで（oculus-samples/Unity-Movement `Shared/ThirdParty/Unity/`）、Meta のリターゲットで体や手が崩れないことが保証されている。機械的・無性のアバターは Lugrin et al. 2015 (IEEE VR) / Gorisse et al. 2019 の系統 | `Resources/Avatars/UnityArmature/Armature.fbx`（`UnityArmatureAvatarImport.cs` が Humanoid・Read/Write 有効・URP マット材質で取り込み） |

2026-11-05 まで使っていた Rocketbox の男性 / 女性と MakeHuman の中性体は外しました（MakeHuman の中性体は女性に見える、
Rocketbox は IOBT で手のリグが崩れる、の 2 点。研究で広く使われる「無性のマネキン / ロボット」アバターに統一）。
別の Humanoid モデルを試すには Resources 配下に置いて `Avian Embodiment` → `Human Avatars` に id / Resources パス /
目の位置（`Eye Offset From Head Bone`、頭ボーン基準の T ポーズでの目の位置。`Avian Voice/Embodiment/Debug/Measure Human Avatar Eye Point`
で頭ボーン・頭頂・顔前面の位置を Console に出せます。Armature は頭ボーンから +0.035 m 上 / +0.09 m 前）を足します。モデルが無い id はパネルに
「missing」と出ます。記録の `events.jsonl` / `trials.csv` には `Human:neutral` のように id 付きで残ります。

歌う場面では、鳥アバターでも人間アバターでも **教師の鳥（`FriendParrot`）が被験者の右隣に出て**歌います（人間アバターのときは
鳥の自分は隠れ、人間の体と教師の鳥が見えます）。実験者パネルの Condition のボタンで切り替えます（V キーは鳥 ⇄ 人間）。

**IOBT と人間アバターの崩れ対策**（`AvianHumanoidRetargeter`、2026-11-05）: Meta XR Core SDK の
`OVRUnityHumanoidSkeletonRetargeter` は既定で ほぼ全関節の **位置** を被験者の関節位置で上書きし（体格が違うと体が伸び縮みして
崩れる）、**指** も個別にリターゲットし（指の軸が違うと手が潰れる）、さらに骨盤に 60° の回転補正（Meta のサンプル用）を
掛けます。本プロジェクトの派生クラスはこれを **回転のみ（腰の位置だけ追従）、指は T ポーズのまま手首に追従、骨盤補正なし** に
変え、SDK が T ポーズのスナップショットを取る直前に `HumanFirstPersonDriver` がリグを T ポーズ（Humanoid の筋肉値 0）に
戻します。体格差で頭の高さがずれる分は、毎フレーム体全体を平行移動して目をヘッドセットに合わせます（骨は触りません）。
トラッキングが途切れたらリターゲットを一時停止して腕を下ろした姿勢に戻します。

**鏡と頭部**: HMD の一人称視点では **自分のアバターの頭部が見えない**ようにし、鏡（Fragilem17 の反射カメラ）には顔ごと
映します（`FirstPersonHeadHider`、鳥・人間の両方に付きます）。頭ボーン（Humanoid の Head、鳥は `parrot_Head_bone`）と
その子（くちばし・目・あご）にウェイトが付いた三角形をスキンメッシュから別のサブメッシュに分けて、一人称カメラの描画中だけ
透明マテリアルに差し替えます（マテリアル名に head / hair / opacity / eyelash を含むスロットと、頭ボーン下の別メッシュも同様）。
鳥の目の位置（`BirdFirstPersonDriver.eyeOffsetFromHeadBone`）はそのままで、頭・くちばしが視界に入らなくなります。
モデルは Read/Write 有効で取り込む必要があります（無い場合は警告を出して頭が見えたままになります）。鏡はメインカメラの描画開始イベントの中で反射を
描くため、頭部非表示は毎フレーム購読をやり直してイベントの最後に回り、「反射を顔つきで描く → 頭を消す → HMD 用に描く」の順を
保証しています（場面切替で鏡が再有効化されても崩れません）。確認は Play 中に「Debug/Capture Game View」で Game ビューを保存。

人間アバターは専用レイヤー **HumanAvatar (12)** に置かれます。鏡と池の反射用の補助ライト（鳥向けに強め）はこのレイヤーに
当たらないため、白飛びしません。髪（`m002_opacity`）はアルファカットアウト、肌は smoothness 0.3 で再インポートされます
（`Avian Voice → Embodiment → Reimport Human Avatar`）。

ができます。キーボードでも同じ操作ができます（Game ビューにフォーカスがある時）。

| したいこと | PC キーボード | 実験者パネル |
|------------|---------------|--------------|
| 場面を変える | **1〜4**、**0**（練習）、**N**（次）、←（前） | Scene flow のボタン |
| 歌う場面で次の試行を始める | **Space** / **R** | Start trial / Next trial ボタン |
| アバターを切り替える | **V** | Bird / Human ボタン |
| コントローラを一時的に有効化 | — | Enable XR controllers トグル |

視界の下の「アバター: 鳥 / 人間 … 体トラッキング: 全身 / 上半身 / なし」のカードは既定で非表示です（被験者に操作を見せないため）。
動作確認のときはパネルの Show the status card、または `showSwitchHint` で表示できます。

## 初回セットアップ

1. プロジェクトを開くと Package Manager が **Meta XR Core SDK (com.meta.xr.sdk.core 207.0.1)** を
   `https://npm.developer.oculus.com` から取得します（`Packages/manifest.json` に scoped registry を追加済み）。
2. メニュー **Avian Voice → Embodiment → Configure Everything** を実行する。これで
   - 人間アバター（Armature FBX）を Humanoid として再インポート（`UnityArmatureAvatarImport.cs`）
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
| `Scripts/BodyTracking/*` (asmdef `AvianVoice.BodyTracking`) | SDK 非依存の関節データ `BodyPoseFrame`、バックエンド IF、トラッキング空間のヘッドセット姿勢 `HeadsetTrackingPose`、予備のトラッキング空間→ワールドの推定 `TrackingSpaceSolver` |
| `Scripts/MetaBody/MetaBodyTrackingBackend.cs` (asmdef `AvianVoice.MetaBody`, SDK がある時だけコンパイル) | `OVRBody` から関節を取得。人間アバターには `OVRUnityHumanoidSkeletonRetargeter` を付けて Humanoid にリターゲット |
| `Scripts/MetaBody/Editor/MetaBodyTrackingProjectSetup.cs` | 上記の OpenXR / Oculus 設定を一括適用 |
| `Scripts/Embodiment/Avatar/AvatarEmbodimentController.cs` | アバター切替、ヘッドセットの姿勢（使えなければカメラ・手の位置）から体トラッキング空間を合わせる、ヒント表示 |
| `Scripts/Embodiment/Avatar/BirdFirstPersonDriver.cs` | 鳥: 目をカメラに、頭を HMD の向きに、体の向きを肩のラインに、翼を腕の上げ下げに |
| `Scripts/MetaBody/AvianHumanoidRetargeter.cs` | Meta のリターゲッタの派生: 回転のみ・腰の位置だけ追従・指は固定・骨盤補正なし・T ポーズ取得直前のコールバック |
| `Scripts/Embodiment/Avatar/HumanFirstPersonDriver.cs` | 人間: ルートをトラッキング原点に置き目をカメラに合わせる。T ポーズの再適用、トラッキング無しのフォールバック姿勢、データ途切れ時のリターゲット一時停止 |
| `Scripts/Embodiment/Avatar/FirstPersonHeadHider.cs` | 一人称カメラだけ頭部（頭ボーン以下の三角形 / 頭・髪のマテリアル）を非表示（鏡・池の反射には映る）。鳥と人間の両方 |
| `Scripts/Study/StudyFlowController.cs` | 4 場面フロー（`TaskManager.SetFlow`）、`SingingTrialTask` の割り当て、`StudyRecorder` の開始、**条件（アバター × 声のフィードバック）** の設定と記録 |
| `Scripts/Study/SingingTrialTask.cs` | 試行形式の歌う場面（教師の 1 音 → 応答、発声検出、30 回で休む場面へ） |
| `Scripts/Audio/HumanVoiceSynth.cs` | 人間の声のフィードバック: 被験者の音程・音量に追従して歌う母音「あ」（フォルマント合成）。`BirdVoiceMapper.SetFeedback` で鳥の声 / 人間の声 / なしを切替 |
| `Scripts/Study/StudyRecorder.cs` | `events.jsonl` / `trials.csv` / `mic.wav` / `output.wav` / 試行切り出しの書き出し |
| `Scripts/Study/PcAudioMonitor.cs` | Unity 出力の PC スピーカーへのミラー（winmm）、Windows 既定出力のミュート検出 |
| `Scripts/Study/ListenerOutputTap.cs`, `WavWriter.cs` | AudioListener のミックス取得、16 bit WAV ストリーム書き込み |
| `Resources/Avatars/UnityArmature/` | 中性マネキン（Unity Starter Assets Armature、Unity Companion License、`LICENSE.md` 参照） |
| `Editor/UnityArmatureAvatarImport.cs` | Armature FBX の Humanoid 取り込み（Read/Write 有効）とマテリアル設定 |

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

**体トラッキング空間の合わせ方（2026-11-07）**: 体の関節は Meta（OVRPlugin）の空間、カメラは Unity の XR Origin の空間で
届きます。体トラッキング用に追加される OVRManager が Unity 側の原点モード（床 / 目の高さ、リセンター許可）を書き換えるため、
Play 開始時や Quest のリセンターのたびに二つの空間がずれることがあります（実際のログで頭の誤差 2.1〜3.1 m）。以前はずれを
検出すると「頭と手からの推定」に切り替え、その後はセッション中ずっと手の位置の誤差で体全体の向きが揺れていました（鳥の胴、
人間アバター全体が不自然に回る）。今は **ヘッドセット自身の姿勢を OVRPlugin の空間でも取り、カメラの姿勢と突き合わせて毎フレーム
正確に対応づけ**ます（`AvatarEmbodimentController` の `Align From Headset Pose`、`HeadsetTrackingPose`）。ずれはその場で
合わせ直され、Console に `Body tracking space re-aligned to the headset` と出ます。確認用メニュー:
- **Debug/Log Tracking Space (Play Mode)**: 方式（`mode=headset` が正常）と、頭関節とカメラの差（`headJointErr` は 0.1 m 前後が正常）。
- **Debug/Switch Body Tracking Origin (test, Play Mode)**: 原点を床 ⇄ 目の高さに切り替えて、わざと空間をずらす（アバターが
  本人に重なったままなら正常。もう一度実行で床に戻ります）。
- **Log Avatar State** の `space=headset offsetFromXrOrigin=…` は、旧方式なら生じていたずれの大きさです。

## 既知の制限

- IOBT の脚トラッキングは Quest 3 / Quest Pro 系で、Quest 2 は上半身のみ（自動判定、ヒントに「上半身」と出ます）。
- 体トラッキングと「ハンドとコントローラの同時使用」は併用不可（`OVRManager.SimultaneousHandsAndControllersEnabled` は off のまま）。
- 鳥アバターは等身大のままなので、ユーザーの目線の高さに浮いて見えます（元シーンの止まり木とは別の体験になります）。
- 飛行場面（4）では元の `ParrotFlightPresentation` が主カメラから鳥を隠す設定を引き継いでいます。

---

# English summary

`Avian_Lab_Embodiment.unity` is a copy of `Avian_Lab.unity` plus: Meta Quest body tracking (IOBT) driving the
avatar, the avatar eyes locked to the XR camera, and an avatar switch (bird `PlayerParrot` ⇄ human). The human body
is one gender-neutral mannequin: the Unity Starter Assets "Armature" robot (Unity Companion License), the same model
Meta ships as the stylized character of its body-tracking samples, so Meta's retargeting is known to keep its body and
hands intact. Retargeting is rotation-only (hips position only, fingers kept, no pelvis tweak, T-pose re-applied right
before the SDK snapshot) so the avatar keeps its proportions. The study has four conditions, avatar (bird / human) x
voice feedback (the participant's own voice heard as bird chirps / as a sung human vowel, or not at all), chosen from the
experimenter panel; the participant's own head (bird or human) is hidden from the headset camera but visible in the
mirror. Toggle bird / human with **V** (keyboard).

The study flow is four scenes (meet, breath, singing, rest). The singing scene is experimenter-paced: each
press of the panel button (or Space) makes the teacher bird sing one of three target pitches and the participant
sings back; the participant's voice onset / offset are detected. A trial lasts from one press to the next (no
wait, the button is always available), so each row of `trials.csv` is the interval between two presses; 3 pitches
x 10 = 30 trials, and the press after the last one moves the session to the rest scene automatically. `StudyRecorder` writes `events.jsonl`, `trials.csv`,
`mic.wav`, `output.wav` and per-trial microphone segments under `<project>/StudyData/<date>_<id>/` (git-ignored),
every timestamp carrying wall clock, Unity time, DSP time and the microphone sample index.

Setup: open the project (Meta XR Core SDK 207.0.1 is pulled from the Meta scoped registry), then run
**Avian Voice → Embodiment → Configure Everything**, build for Android and allow body tracking on the headset.
Without a headset the avatars follow the head only. See the table above for the scripts involved.
