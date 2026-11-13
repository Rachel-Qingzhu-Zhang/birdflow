# Avian_Lab_Embodiment — 体トラッキングで動くアバターとアバター切替

`Assets/AvianVoice/Scenes/Avian_Lab_Embodiment.unity` は `Avian_Lab.unity` のコピーに、次の点を足した実験用シーンです。

1. **Quest の体トラッキング（IOBT / Full Body）** で、ユーザーの体の向き・腕・（対応機種では）脚がアバターに反映される。
2. **視点がアバターの目に一致する**（アバターの目が XR カメラの位置に来るように毎フレーム合わせる）。
3. **アバター切替モード**: 鳥（`PlayerParrot`）⇄ 人間。人間は **中性 / 男性 / 女性 の 3 体から選択**（2026-11-12〜。
   中性 = MakeHuman の性別 0.5・胸が平らな体、男性 / 女性 = Microsoft Rocketbox）。それまでのロボット型マネキン
   （Unity Starter Assets の Armature）はプロジェクトに残してあり、設定 1 つで選択肢に戻せます（下記）。
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
XR リグの手メッシュ・関節・**コントローラーのモデル・つまみ / ポークのポインター・レイ**（Near-Far / Teleport Interactor の線）も
既定で非表示です（`Participant Hands Visible`、トラッキング自体は動き続け、翼や人間アバターの腕がその代わりに見えます）。
2026-11-12 まではリグの「… Hand」の下だけを隠していたため、コントローラーのモデルが見え、レイも線を描く部品
（`CurveVisualController` / `XRInteractorLineVisual`）が自分で線を再表示するので一瞬ずつ出ていました。今は「… Controller」の下も
隠し、線を描く部品そのものを止めています（確認: `Debug/Log XR Rig Renderers`）。

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

| ボタン | アバター | 被験者が聞く自分の声 | 記録（`voice_feedback` / `condition` = 条件フォルダ名） |
|--------|----------|----------------------|------------------------------------------|
| Bird + bird voice | 鳥 | 鳥の声（`BirdSongSynth`、声が出ている間ずっと音程に追従して鳴る） | `bird` / `bird_bird-voice` |
| Bird + own voice only | 鳥 | なし（アプリは何も出さない。地声だけ） | `none` / `bird_own-voice` |
| Human + human voice | 人間（中性 / 男性 / 女性、パネルの Human body で選ぶ） | 自分の声そのもの（`MicPassthrough`、マイク入力を変換せずにヘッドホンへ出す。2026-11-12 より前は `HumanVoiceSynth` の合成母音「あ」） | `human` / `human_human-voice` |
| Human + own voice only | 人間（同上） | なし | `none` / `human_own-voice` |

人間アバターの体（中性 / 男性 / 女性）は条件には含めず、**データごとのフラグ**で残します: すべてのイベント・`trials.csv`・
`samples.csv` の `human_avatar` 列（`neutral` / `male` / `female`、鳥のときは空）と `avatar_model`、試行 WAV のファイル名の末尾
（`..._male.wav`）。

- 声のフィードバックは `BirdVoiceMapper.SetFeedback` で切り替わり、歌う場面の応答中だけでなく、どの場面でも同じ設定です。
  V キーで鳥 ⇄ 人間を切り替えても「変換あり / なし」は保たれます（鳥なら鳥の声、人間なら人間の声になります）。
- 教師の鳥の手本（guide note）は **どの条件でも鳥の声**です（`SingingTrialTask.teacherVoice = Bird`）。
  人間アバターのときに手本も人間の声にしたい場合は `Teacher Voice` を `FollowAvatar` にします（`events.jsonl` の
  `guide_start` に `teacher_voice` が残ります）。
- 鳥の声は 2026-11-07 から人間の声と同じ連続型です（有声の間ゲートが開き、音程・音量に追従。止まると減衰）。
  以前の「声の立ち上がりごとに約 80 ms のさえずりを 1 回鳴らす」離散型はコードに残してあり、
  `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` の `Synth Continuous` をオフにすると戻ります。
  どちらで記録したかは `events.jsonl` の `voice_feedback` イベントの `bird_feedback`（`continuous` / `chirps`）に残ります。
- 自分の鳥の声の音量（2026-11-13）: `AvianVoiceSettings` の `Synth Own Voice Volume`（既定 0.5 = 先生の鳥のガイド音より 6 dB 下。
  1 で以前と同じ）。ガイド音は変わりません。以前は同じレベルでしたが（output.wav の実測でガイド音 -9.7 dB、大きく歌った自分の声 -9.4 dB）、
  自分の声は目標の 1 オクターブ上で歌われて 4〜5.5 kHz になり（先生は 2〜3 kHz）、ずっと大きく聞こえていました。
  記録時の値は `voice_feedback` イベントの `bird_own_voice_volume` に残ります。
- 時間応答（2026-11-07）: 声のフィードバックは解析器の音量エンベロープだけでゲートします（立ち上がり 2 ms、
  減衰の時定数 12 ms、`AvianVoiceSettings.loudnessAttackSeconds / loudnessReleaseSeconds`）。合成器の減衰の時定数は 15 ms
  （-30 dB まで約 50 ms）、音程の平滑化は 20 ms です。実測（`Debug/Test Voice Response`: 0.5 秒の合成音を解析器に
  流し込み、`output.wav` と `debug_voice_inject` イベントで測定）では、入力停止から -30 dB まで鳥 100 ms / 人 90 ms、
  入力開始から発音まで約 60 ms（変更前は減衰 220〜245 ms）。
- 途切れ対策（2026-11-13）: 声のフィードバックのゲートは有声判定（`voicedThreshold`）で開き、エンベロープが
  `voicedThreshold × feedbackGateCloseRatio`（既定 0.5）を `feedbackGateHoldSeconds`（既定 30 ms）下回り続けたときだけ閉じます。
  以前は開閉が同じレベルで、小さい声がしきい値付近で揺れると 1 つの音の中で鳥の声が切れて鳴り直していました
  （セッション 20261113_165018: 28 音中 20 回）。同じ `mic.wav` での再計算では 72 fps で 32 回 → 0 回、18 fps で 21 回 → 2 回、
  声が止まってから消えるまでは中央値で約 55 ms 長くなります。記録される有声フラグ・発声区間は変わりません。
  値は `voice_feedback` イベントの `feedback_gate_close_ratio` / `feedback_gate_hold_s` に残ります。
- **音の出口（2026-11-12 から）**: 既定では **PC につないだデバイスだけ**から音が出ます。パネルの Status 欄
  「Sound output」= `PC device only`、出すデバイスは「Output device (PC)」で選びます（次回以降も記憶）。
  Unity 自体の出力（Link 中はヘッドセット、それ以外は Windows の既定）は無音にします。
  - 仕組み: `PcAudioMonitor` が AudioListener の最終ミックスを PC のデバイスへ流し（ミラー）、そのあとの最後のフィルター
    `ListenerOutputSilencer` がミックスを消します。`output.wav` の取り出し口（`ListenerOutputTap`）は消す前にあるので、
    録音は被験者が聞いた音のままです。PC のデバイスを開けなかったときは、無音にせず Unity 自体の出力を残します。
  - PC 側の出力は低遅延の方式（WASAPI 共有モード、`Scripts/Study/WasapiOutput.cs`）です。Unity がミックスしてから
    **約 40 ms**（予備バッファ `Reserve Ms`、既定 40）＋ デバイス自体の遅れで鳴ります。2026-11-12 より前の方式（waveOut、
    約 0.1 秒遅れ）は、デバイスが WASAPI を受け付けないときの予備として残してあり、そのときはパネルに waveOut と出ます。
    Bluetooth ヘッドホンはデバイス側で 0.2〜0.3 秒遅れるので、有線のヘッドホンを使ってください。
    - 予備バッファが要る理由: Unity の音声スレッドはエディタで約 13 秒ごとに 16〜20 ms 止まります。予備がそれより小さいと
      短い途切れになります（パネルの「Output delay (PC)」の gaps、`pc_audio_monitor` イベントの `underruns`）。途切れたら
      予備を目標まで溜め直してから再開します。Unity の出力と PC のデバイスはクロックが違うので、予備の量を保つために
      再生速度を最大 0.3 %（5 セント）動かします。
    - 調整: AudioListener の `PcAudioMonitor` コンポーネントの `Reserve Ms`（小さいほど遅れが減り、途切れやすくなる）、
      `Device Buffer Ms`、`Low Latency`（オフで以前の waveOut）。
    - 確認（2026-11-12、エディタ、ヘッドフォン (Realtek)、約 50 秒）: 予備 35.6〜40.0 ms、途切れは起動直後の 1 回のみ、
      破棄 0 回。耳までの遅れそのもの（音響的な実測）は未測定です。
  - 自分の声を聞かせる条件（鳥の声 / 人間の声）では、この遅れがそのまま加わります。人間の声（マイクの素通し）は
    アプリ内で 30 ms（`micPassthroughLatencyMs`）＋ 40 ms（この予備）＝ 約 70 ms に、マイクとヘッドホンの機器の遅れが加わります。
  - 「Sound output」の他の選択肢: `PC device + Unity's own output`（両方。両方聞こえる人には二重に聞こえます）、
    `Unity's own output, + PC device while that is the headset`（2026-11-12 より前の既定 Auto）、`Unity's own output only`。
    2026-11-12 より前に保存した設定（旧 `PC monitor output`）は引き継がず、新しい既定から始まります。
  - 記録: セッション開始時とモードを変えたときの `pc_audio_monitor` イベントに `mode` / `unityOutputSilenced` /
    `silencerLast` / デバイス名 / `backend`（`wasapi` / `waveOut`）/ `reserveMs` / `underruns` / `drops`。確認（2026-11-12、`StudyData/20261112_173853_P000`）: 鳥の声を鳴らしている間、ミックスの
    RMS 0.33〜0.36 に対して Unity 自体の出力は 0.0000、`output.wav` には -10 dBFS で記録。
  - Windows 側でそのデバイスがミュートだと PC からも聞こえません（パネルに MUTED と出ます）。
- 使用中のマイクはパネルの Status 欄「Microphone in use」に出ます（機器名・サンプリングレート・入力レベル）。
  「Input device」で機器を選ぶとその場で切り替わり、次回以降も記憶されます（Auto = XR 中はヘッドセットのマイク）。
- Unity 自体の出力先は「Unity's own output device」で選べます（2026-11-12、`Scripts/Study/AudioOutputRouting.cs`）。
  既定の `PC device only` では Unity 自体の出力は無音なので、この行は「Sound output」を他の選択肢にしたときと、
  ここで以前に Windows 側の設定をしたとき（戻せるように）だけ出ます。Unity には
  自分の出力デバイスを選ぶ API がないので、Windows の「アプリごとの出力デバイス」（設定 > サウンド > 音量ミキサーと
  同じ設定）を Unity Editor に対して切り替えます。
  - この設定は Windows 側に残ります: Unity を閉じても残り、同じバージョンの Editor で開く他のプロジェクトにも効きます。
    戻すときは「Windows default (automatic)」を選びます。
  - 実際に音が出ているデバイスはすぐ上の「Unity output device」です。選んだデバイスと違うままのときは警告と
    「Restart Unity's audio output」ボタン（Play 中。短い無音、マイクも再起動）が出ます。ヘッドセット接続中は
    ヘッドセットのランタイムが出力先を決めることがあり、その場合は Meta Quest Link アプリの設定「Audio output in VR」で
    変えます（未確認: このパネルの選択が Link 接続中にも効くかどうか）。
  - 記録: 選んだとき `audio_output_device` イベント、再起動したとき `audio_output_restart` イベント。
    メニュー **Debug/Log Audio Output Devices** で、デバイス一覧・このアプリの選択・実際の出力先をログに出せます。
  - 確認済み（2026-11-12、Windows 11 build 26200）: デバイス一覧と現在の選択の読み取り、設定呼び出しが受け付けられること。
    実際に切り替えて音が移ることは未確認です。
- 人間の声の条件（Human + human voice）は 2026-11-12 から **マイク入力をそのままヘッドホンに出します**
  （`Scripts/Audio/MicPassthrough.cs`。解析も合成も通しません）。鳥の声と同じく、聞こえるのは声を追従している間
  （歌う場面の応答中など）だけで、手本が鳴っている間は閉じています。
  - 遅れ: マイクの書き込み位置から `AvianVoiceSettings.micPassthroughLatencyMs`（既定 30 ms）だけ後ろを再生します。
    これにマイクと出力機器（Quest Link など）の遅れが加わります。マイクと出力は別のクロックなので、距離を毎フレーム測り、
    再生速度を最大 0.3 %（5 セント）だけ動かして保ちます。再生位置がマイクを追い越した / 大きく遅れたときだけ位置を
    飛ばします（resync。音が一瞬途切れます。繰り返すなら `micPassthroughLatencyMs` を上げる）。
  - 音量: `AvianVoiceSettings.micPassthroughGain`（既定 1 = マイクのまま）。マイクの入力レベルで決まるので、鳥の声の条件と
    聞こえる大きさを揃えるにはここで合わせます（まだ実測していません）。`output.wav` では `mic.wav` より 3 dB 低く
    入ります（Unity がモノラル音源を左右に振り分けるときの減衰。他の音源も同じ）。
  - 記録: `voice_feedback` イベントの `human_feedback`（`microphone` / `synth`）、`mic_passthrough` イベント
    （`phase` = `start` / `open` / `close` / `resync_passed` / `resync_behind`、`target_ms`、`lag_ms`、`gain`、`resyncs`）、
    聞こえている間は 1 秒ごとに `mic_passthrough_trace`（その 1 秒の遅れの `lag_min_ms` / `lag_max_ms` / `lag_mean_ms`、
    `lag_smoothed_ms`、`rate_trim` = 再生速度の補正量）。`output.wav` には被験者が聞いた自分の声がそのまま入ります。
    パネルの Status 欄「Own voice to headphones」に現在の遅れと resync の回数が出ます。
  - 実測（2026-11-12、エディタ、ヘッドセットなし、約 19 fps、マイク ECM-W3R、`StudyData/20261112_164954_P000`）:
    `output.wav` にマイクの音がそのまま入っていること（相関 0.9〜0.998）、遅れが平均 30〜35 ms に保たれることを確認。
    出力側が約 13 秒ごとに 16〜20 ms 止まり（DSP 時刻が実時間から遅れる。素通し以外の音も同じ）、そのたびに遅れが
    約 46 ms に増え、10 秒ほどで目標へ戻ります。resync は 0 回。ヘッドセットを着けた状態の耳までの遅れは未測定です。
    `mic.wav` と `output.wav` の位置合わせだけで遅れを測ると、この出力の停止のぶんだけ見かけの値がずれます
    （`t_utc` に対する `mic_sample` / `out_frame` / `t_dsp` の進みを見ると区別できます）。
  - 音がスピーカーで鳴っていて、それをマイクが拾うと、ハウリングします。この条件では「Output device (PC)」を
    スピーカーではなくヘッドホンにしてください。
  - 以前の合成母音に戻すには `AvianVoiceSettings.asset` の `Human Voice From Microphone` をオフにします。
    手本を人間の声にした場合（`Teacher Voice = FollowAvatar`）の手本は、どちらの設定でも `HumanVoiceSynth` の合成母音です。
    `Debug/Test Voice Response`（合成音を解析器に流し込む測定）は、マイク入力を通さないので、この条件の応答は測れません。
- `HumanVoiceSynth` の音量は鳥の手本と同じ約 -11 dBFS に合わせてあります（2026-11-05 に `output.wav` で実測。
  変えるときは `HumanVoiceSynth.volume`）。
- `events.jsonl` の全イベントに `voice_feedback` / `condition` / `human_avatar`、`trials.csv` と `samples.csv` に同じ列、
  条件を変えたときに `condition_end` / `condition_start` と `voice_feedback` イベント（`condition_label` = パネルの表記、
  例 `Human:neutral+human`）が入ります。2026-11-12 より前の記録では `condition` はこのパネル表記でした。

### 呼吸の場面の長さ（2026-11-13 に 1 分へ変更）

呼吸の場面は、吸う 6 秒・吐く 6 秒を 5 周 = 60 秒で、終わると自動で歌う場面に進みます（どの条件でも同じ）。それまでは
4 周 = 48 秒でした。長さは `AvianVoiceSettings` の Teacher Breath にある `Pattern B Cycles`（周数）×
（`Pattern B Inhale Seconds` + `Pattern B Exhale Seconds`）で決まります。実測（samples.csv の `breath_depth`）: 呼吸 60.2 秒、
場面に入ってから歌う場面に切り替わるまで 61.9 秒（呼吸が始まるまで約 0.9 秒、終わりの合図 0.6 秒を含む）。

### 呼吸の場面（人間アバター、2026-11-07、2026-11-12 に拡大）

鳥と同じ呼吸の信号（教師の鳥の吸う / 吐くパターン）で、人間アバターの胸も膨らみます（`HumanBreathEmbodiment`、
人間アバターに自動で付きます）。首と肩がつながる胸の骨を、最大吸気で前後 22 %・左右 10 %・縦 3 % 拡大し、肩を
上げます（下の「肩の動き」）。首と肩には逆の拡大を掛けるので、頭・腕の大きさは変わりません（中性の体で胸の厚み 21.2 → 25.2 cm、幅 32.2 → 34.5 cm、
肩 31 mm、頭の移動 16 mm、手 34 mm）。2026-11-12 までは前後 6 %・左右 3 % で、鏡は正面から見るため、ほとんど見えませんでした。
動くのは呼吸の場面（`breath_sync`）だけで、場面の切り替えで 0.6 s かけて出入りします。量はコンポーネントの
`Depth Inhale` / `Width Inhale` / `Shoulder Rise` で変えられます。確認用: **Debug/Load Task 2 Breathing**、
**Debug/Measure Human Breathing**、**Debug/Log Human Breathing Now**、**Debug/Hold Human Breathing: Full Inhale / Exhale / Off**
（吸った状態・吐いた状態で止めて見比べる）。

肩の動き（2026-11-13 に変更）: それまでは鎖骨の骨全体を 22 mm 上にずらすだけで、呼吸していることが伝わりにくかったため、
鎖骨を内側の端を軸に上へ回し、肩先（腕の付け根）を最大吸気で 35 mm 持ち上げるようにしました（`Shoulder Rise`、0〜80 mm）。
上腕の向きはそのまま保つので、腕は外に開かず肩と一緒に上がります。胸の拡大分と合わせて、肩先は約 41 mm 上がり
（頭に対して +35 mm、中性の体。変更前は骨の位置で 31 mm）、手も同じだけ（約 43 mm）上がります。
確認用: **Debug/Render Human Breathing (exhale and full inhale)** は Play なし・ヘッドセットなしで、全ての人間の体を
吐いた状態 / 吸いきった状態で正面と横から描画し（`Temp/HumanBreathingTest/breathing.png`）、実測値をログに出します。
**Debug/Log Human Breathing Now** は、その時点の肩先の高さ（胸の骨からの mm）も出します。

鳥アバターの呼吸の大きさ（2026-11-13）: 人間アバターに比べて大げさに見えたため、鳥の呼吸の動き（胸・腹の拡大と、呼吸に合わせた
翼の開き・羽ばたき・持ち上げ）を `AvianVoiceSettings` の **Breath Motion Scale**（Teacher Breath、既定 0.65）で一律に弱めています
（胸の骨 +40 % → +26 %、腹 +28 % → +18 %、翼の開き 14° → 約 9°）。1 にすると元の大きさです。自分の鳥と教師の鳥の両方に
掛かり、歌うときの姿勢には掛かりません。

### 人間アバター条件の表示（2026-11-12）

- 人間アバターの間、自分の鳥はすべてのレンダラーを毎フレーム確認して隠します。以前は、人間アバターのまま体（中性 / 男性 /
  女性）を切り替えると、次に歌う場面へ入ったときに自分の鳥が人間の体に重なって表示されていました。教師の鳥は全条件で表示します。
- 被験者に見える案内カードの文言は**全条件で同一**です（`AvianUiCopy.GuideText`、`SceneGuide`）。場面のタイトル（英語のみ）の下に、
  指示を日本語・英語の順で常に併記します（言語切り替えには依りません）。

  | 場面 | タイトル | 指示 |
  |------|----------|------|
  | 練習 | Practice | なし（タイトルのみ。歌唱中の表示も出しません） |
  | 1 | Meet Scene | 鏡の中の自分を見てください。 / Look at yourself in the mirror. |
  | 2 | Breathing Scene | 鏡の中の自分に合わせて呼吸してください。 / Breathe together with yourself in the mirror. |
  | 3 | Singing Scene | 先生と同じ音程で歌ってください。 / Sing the same pitch as the teacher. |
  | 4 | Rest Scene | 少し休みましょう。 / Take a short rest. |

  歌う場面の試行中は、指示の代わりに「この音を聴いてください。 / Listen to this note.」「今度はこの音を歌ってください。 /
  Now sing this note.」「次の音を待ってください。 / Wait for the next note.」「お疲れさまでした。そのまま休んでください。 /
  All done. Rest now.」が同じく併記で出ます。音名（B3 など）と周波数は被験者には出しません（下の「案内カードの位置と音名の非表示」）。
- 場面選択メニュー（「Choose a scene」のカードと、閉じたときのチップ）は被験者の視界に出しません（`SceneWishMenu` の **Show Menu**、
  既定オフ。M キーや XR のメニューボタンでも開きません）。場面の切り替えは実験者パネルか 1〜4 / 0 / N キーで行います。
- 音程が合ったときに教師の鳥が光る機能は、歌う場面と練習ではオフです（`SingingTrialTask` の **Pitch Match Glow**。
  `singing_task_enabled` イベントに `pitch_match_glow` が入ります）。被験者は音程の合い具合を目では分かりません。

### 案内カードの位置と音名の非表示（2026-11-12）

**案内カードは鏡の上部に固定**（`SceneGuide`、寸法は `AvianHudLayout.GuideMirror*`）

- これまで案内カードはカメラの子（頭に追従する表示）で、頭を動かすと一緒に動くため酔いやすいという指摘がありました。
  鏡を表示する場面（練習・会う・呼吸・歌う・休む）では、カードをシーン内の鏡の上部に固定します。頭や体を動かしても
  カードは動きません。場面が変わったときと鏡自体が動いたときだけ置き直します。
- 位置は鏡の中央、被験者が立つ側を向き、目の方へ少し傾けます（最大 30°）。基準は頭の位置ではなく、その場面の XR リグ原点
  （床）+ 目の高さ 1.55 m（`GuideNominalEyeHeight`）なので、被験者やヘッドセットの置き場所に依らず毎回同じ位置です。
- 高さは「鏡の枠の上端から 8 cm 上」が基本ですが、目の高さから見た仰角が 20°（`GuideMirrorMaxElevationDegrees`）を超える
  場合は、仰角 20° の高さで**ガラスの 5 cm 手前**に置きます。`Avian_Lab_Embodiment` の鏡は枠が幅 3.25 m × 高さ 4.13 m
  （ガラス 2.31 × 3.08 m）、立ち位置は鏡の 1.46 m 手前で、枠の上では仰角が約 60° になり読めないため、後者になります
  （カード中心は床から 2.06 m、ガラス上端の 0.96 m 下。鏡に映る木の上の方に重なり、鳥や人間アバターの像には重なりません）。
  小さい鏡なら枠の上に出ます。仰角を変えるときは `GuideMirrorMaxElevationDegrees` を変えます。
- 大きさは立ち位置から鏡までの距離に比例させます（`GuideMirrorScalePerMeter`）。このシーンではカード 0.87 × 0.18 m、
  見かけの大きさは約 34° × 7°、本文の文字は約 1.3°（頭に追従していたときは約 1.1°）です。
- カードは UI レイヤー（11）で、鏡の反射カメラはこのレイヤーを描かないので、鏡の中にカードは映りません。
- 鏡を出さない場面（飛ぶ・仲間を呼ぶ。実験の流れの外）だけは従来どおりカメラに付きます。実験の場面で鏡が 1 秒待っても
  見つからないときも同じ扱いになり、Console に警告（`SceneGuide: no visible mirror ...`）が出ます。
- 頭に追従する表示で残っているものは、いずれも実験では出ません: 場面選択メニュー（Show Menu オフ）、デバッグ HUD
  （`AvianVoiceSettings` の Show Hud Panels オフ。オンにすると自分の声の Hz が出ます）、アバター切り替えのヒント
  （`AvatarEmbodimentController` の Show Switch Hint オフ）、もう一度のチップ（実験外の歌う / 仲間を呼ぶ場面のみ）。

**音名と周波数は被験者に出さない**

- 歌う場面の案内は「この音を聴いてください。 / Listen to this note.」「今度はこの音を歌ってください。 / Now sing this note.」
  です（以前は「B3を聴いてください。」のように音名入り）。音名の文字が注意を引くためです。練習の場面はもともとタイトルのみです。
  実験外の「仲間を呼ぶ」「歌う（旧）」の案内も同じ関数（`AvianUiCopy.ShowSingingCue`）なので音名は出ません。
- デバッグ HUD の進行表示（TaskModeHUD、実験ではオフ）も音名ではなく「Trial n / 30」にしました。
- 記録と実験者側は変えていません。`trials.csv` の `note` / `target_hz`、`events.jsonl` の `trial_start`（`note`）、
  切り出し音声のファイル名（`trial_001_a1_B3.wav`）、実験者パネルのボタン表示、Study Monitor には音名と Hz がそのまま出ます。

**確認用メニュー**（Avian Voice > Embodiment > Debug、Play 中）

- **Log Scene Guide**: カードの配置（`mirror_top_over_glass` / `mirror_above_frame` / `on_camera`）、ワールド座標、大きさ、
  表示中の文言、カメラ位置を Console に出します。表示中のすべてのテキストから音名・Hz を探し、`textsWithNoteOrHz` に数を出します。
- **Scene Guide - Move Viewer Test**: ヘッドセットなしで XR リグを左 0.5 m・後ろ 0.3 m、右 25°・上 12° に動かし（もう一度で戻す）、
  カードのワールド座標を出します。固定されていれば座標は変わりません。
- **Capture Game View**: `StudyData/screenshots/` に Game ビューを保存します。

2026-11-12 の確認（ヘッドセットなし、エディタの Play）: 5 場面すべてと鳥 / 人間の両条件でカードは同じ座標
`(34.794, -10.639, 65.925)`、リグを動かしても座標は不変、試行中の文言に音名なし（`textsWithNoteOrHz=0`）、
`trials.csv` の `note` は `B3` のまま。**ヘッドセットでの見え方（読みやすさ、仰角 20° の見上げやすさ、立体視での奥行き感）は未確認です。**

### 歌う場面の試行（3 ピッチ × 10 回）

1. 場面 3「歌う」に切り替える。被験者には「次の音を待ってください」と出ます。
2. パネルの **「▶ Start trial n / 30 — teacher bird sings B3」** を押す（または Game ビューで Space）。
   教師の鳥が目標の音を約 0.9 秒歌い（案内は「この音を聴いてください」）、0.3 秒おいて「今度はこの音を歌ってください」に変わります。
   音名はパネルにだけ出て、被験者には出ません。
3. 被験者が歌う。声の始まり（60 ms 以上有声）と終わり（250 ms 以上無声）を検出して記録します
   （複数回発声してもすべて残ります）。応答中は条件に応じて自分の声が鳥の声として / そのまま（人間の声の条件）聞こえます（または何も出ません）。
4. 実験者が **次のボタン（「▶ Next trial n+1」/ Space）を押した瞬間に試行が終わり、次の試行が始まります**。
   待ち時間はなく、教師の鳥が歌っている最中でも押せます（その場合は試行が短く記録されるだけです）。
   つまり `trials.csv` の 1 行は「ボタンを押してから次に押すまで」の区間です（`t_start_*` 〜 `t_end_*`、
   `end_reason = experimenter_next`）。「End (idle)」で試行を閉じて待機に戻すこともできます（`experimenter_end`）。
5. 30 回目の試行の後にもう一度押すと（ボタンは「■ End the last trial → Rest scene」になります）
   「お疲れさまでした」と出て、3 秒後に自動で「休む」場面へ移ります。

`SingingTrialTask` の **Trial Until Next Press** を外すと以前の動作（応答は最長 5 秒、声が止まって 1 秒で自動終了、
教師の鳥が歌っている間はボタン無効）に戻ります。

ピッチ（既定 B3 247 / D4 294 / F#4 370 Hz。2026-11-07 に E4 / G4 / B4 から A3 / C4 / E4 に下げたあと、少し低すぎたので
全音上げました。音の間隔は同じで、男性でも出せる高さ、最後の F#4 は男性には少し厳しい高さです。女性は 1 オクターブ上で
答えられます。一致の許容幅も同じ比率で 40 → 30 Hz にしたので、各音の判定の厳しさは半音単位で最初の設定と同じです）、各ピッチの回数、順序（ブロック / 交互 / ランダム）は最初の試行を始める前なら
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
| `samples.csv` | 連続信号（1 行 1 サンプル、ほぼ毎フレーム）: アナライザの声の高さ・信頼度・音量、呼吸の深さ・位相。各行に `t_utc` / `t_unity` / `t_dsp` / `mic_sample` / `out_frame` が付くので `mic.wav` やイベントと揃えられます。`score_task` が空の行が定期サンプル、タスク名が入った行はそのタスクがスコアを出した時点の行です |
| `mic.wav` | 被験者マイク（セッション全体、モノラル 16 bit） |
| `output.wav` | 被験者が聞いた音（AudioListener のミックス、モノラル 16 bit）。教師の音がここに入ります |
| `conditions.csv` | 条件の区間ごとに 1 行（時系列順）: 条件、人間アバター、開始 / 終了時刻、`mic.wav` / `output.wav` 上の範囲（`mic_sample_start`〜`mic_sample_end`）、その区間の試行数・サンプル数。同じ条件に戻ると新しい行になります |
| `conditions/<条件>/` | **条件ごとのフォルダ**（`bird_bird-voice` / `bird_own-voice` / `human_human-voice` / `human_own-voice`、使った条件だけ作られます）。中身はその条件の間に書かれた行だけの `events.jsonl` / `trials.csv` / `samples.csv`（セッション直下の同名ファイルの行のコピー）と、下の試行 WAV |
| `conditions/<条件>/segments/trial_NNN_aN_<音名>[_<人間アバター>][_run<N>].wav` | 試行開始〜終了のマイク音声の切り出し。試行番号は条件ごとに 1 から始まります。同じ条件をやり直した分には `_run2`、`_run3` が付き、上書きはしません。`trials.csv` の `segment_file` はセッションフォルダからの相対パスです |

セッション直下のファイルが**時系列の全体**、`conditions/` がそれを**4 条件に分けたもの**です（2026-11-12 から。それ以前は
`segments/` がセッション直下にあり、2 つ目の条件の試行 1〜n が 1 つ目の条件の同じ番号の WAV を上書きしていました。
音声は `mic.wav` に残っています）。連続音声（`mic.wav` / `output.wav`）は複製せず、条件ごとの範囲は `conditions.csv` で
分かります。

**条件の途中でやり直すとき（`run`）**: どうしようもないミスで 30 回を取り直すときは、Play も条件もそのままで、パネルの
**Restart from trial 1 (reset)** を押して試行 1 から取り直します。それまでの行と WAV は消えずに残り、取り直した分には
`run` = 2（さらにやり直せば 3 …）が付きます（`trials.csv` / `samples.csv` / 全イベントの `run` 列、WAV 名の `_run2`、
`events.jsonl` の `trial_run_start`）。`run` は条件ごとに数え、その条件で試行番号が 1 に戻ったら増えます（あとで同じ条件に
戻ってやり直した場合も同じ）。1 回だけのやり直し（Repeat the last trial）は `attempt` が増えるだけで `run` は変わりません。
**解析では各条件の `run` が最大の行だけを使います。** Study Monitor のカードは最新の run を表示し、前の run は下に薄く
「run 1 · 12 WAVs · started over」と分けて出します。

**Study Monitor**（`Tools/StudyMonitor`、Play で自動的に別ウィンドウが開きます。メニュー Avian Voice / Study Monitor）の
上段に 4 条件のカードがあり、試行 WAV が 1 本保存されるたびにその条件のカードにタイル（そのファイルの波形）が増えます。
無音・ほぼ無音・ファイルなし・声の検出なしのタイルには警告が付きます。

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
- **Sound output**（2026-11-12 より前は PC monitor output。`PcAudioMonitor`、AudioListener に自動で付きます）: Unity の出力を
  そのまま PC のデバイス（指定がなければ Oculus / Virtual でない最初の waveOut デバイス）に流します。既定は
  2026-11-12 から **PC device only**（PC のデバイスだけ。上の「音の出口」を参照）。以前の既定 **Auto** は「Unity の実際の出力先（上の Unity output
  device、見つからなければ Windows の既定出力）がヘッドセット（Oculus Virtual Audio Device など）のときだけ」ミラーするので、
  Link でヘッドセットに音を出しつつ実験者も PC で聞けます。Unity が PC スピーカーに出ているときは二重になるのでミラーしません
  （判定はミラー自身の出力ストリームを除外し、Active なセッションを優先します）。**Always** で常時、**Off** で停止。「Output device (PC)」で
  デバイスを一覧から選べます（2026-11-12 より前は名前の一部を入力する「Device (part of the name)」）。遅延は 0.1 秒程度です。
- Link でヘッドセット側に聞かせるには、Windows のサウンド設定で既定の出力を「Headphones (Oculus Virtual Audio Device)」に
  するか、Meta Quest Link アプリの設定でリンク時にオーディオを切り替えるようにしてください（PC モニター出力が Auto で
  PC スピーカーにも出ます）。

### 人間アバター（中性 / 男性 / 女性。既定は中性）

| Id | パネルの表示 | モデル | 出典・ライセンス | 置き場所 |
|----|--------------|--------|------------------|----------|
| `neutral`（既定） | neutral (MakeHuman) | **MakeHuman の中性の体**（性別 0.5・胸は平ら・顔テクスチャ / 髪 / 服なし・単色マット、眼球は同色） | MakeHuman / MPFB2 の CC0 アセット。`Tools/MakeHuman/generate_neutral_avatar.py` で生成（下記） | `Resources/Avatars/MakeHuman/Neutral_Adult_01/Neutral_Adult_01.fbx`（`MakeHumanAvatarImport.cs` が Humanoid・Read/Write 有効で取り込み） |
| `male` | male (Rocketbox) | **Microsoft Rocketbox Male_Adult_01**（顔・髪・服のテクスチャあり） | MIT（`Resources/Avatars/Rocketbox/LICENSE.md`、README 参照） | `Resources/Avatars/Rocketbox/Male_Adult_01/`（`RocketboxAvatarImport.cs`） |
| `female` | female (Rocketbox) | **Microsoft Rocketbox Female_Adult_01**（同上） | MIT | `Resources/Avatars/Rocketbox/Female_Adult_01/` |
| `mannequin`（選択肢に出さない） | mannequin (Armature) | Unity **Starter Assets の Armature**（白いロボット型マネキン。顔・髪・服・性差なし） | Unity Companion License（`LICENSE.md` 同梱）。Meta が Movement SDK の体トラッキングサンプルの「Stylized」キャラクターとして配布しているモデル | `Resources/Avatars/UnityArmature/Armature.fbx`（`UnityArmatureAvatarImport.cs`） |

**2026-11-12 から 中性 / 男性 / 女性 の 3 体**です（男性・女性は 2026-11-04 に使っていた Rocketbox を git 履歴から戻したもの）。
マネキンは `Avian Embodiment` → `Human Avatars` の `mannequin` の項目に残してあり、**`Selectable` にチェックを入れるとパネルと
「Next Human Avatar」に戻ります**（`Start Human Avatar Id` を `mannequin` にすれば最初からマネキン）。
**記録の id の変遷に注意**: `neutral` は 2026-11-04 は MakeHuman、2026-11-05〜11-12 は Armature のマネキン、2026-11-12 以降は
MakeHuman です（MakeHuman は 11-07〜11-12 は `makehuman`）。2026-11-12 以降の記録には id のほかにモデルのパスも残ります（下記）。

**記録されるアバター**（2026-11-12〜）: `events.jsonl` のすべての行に `avatar`（`Bird` / `Human:<id>`）と `avatar_model`
（人間なら Resources のパス、例 `Avatars/Rocketbox/Female_Adult_01/Female_Adult_01`、鳥なら `PlayerParrot`）、`trials.csv` の
各試行に `avatar` と `avatar_model` の列。セッション開始時の `human_avatars` イベントに、そのセッションで使える人間アバターの
一覧（id・パネルの表示・モデル・選択肢に出るか）、切り替えるたびの `avatar_changed` イベントに `human_id` / `human_label` /
`human_model` が残ります。

男性・女性（Rocketbox）の基準姿勢: モデルに保存された T ポーズでは頭が後ろに反り（あごが上がる）、口も開いているので、胴体・首・頭と
顔の骨（あご・目・まぶた）は **メッシュを作った姿勢（スキンのバインドポーズ）** から取ります（`HumanoidTPose`、確認は
`Debug/Render Human Rest Poses`）。MakeHuman と Armature はどちらでも同じ姿勢です。

MakeHuman を 2026-11-05 に一度外したときの
「胸があるように見える」「手のトラッキングがおかしい」は、どちらも **FBX の書き出しの不具合**が原因でした。体型の設定（性別 0.5・
胸の大きさ最小・人種の混合）が Blender のシェイプキーのまま FBX にブレンドシェイプとして入り、Unity はそれを取り込まないので、
**体型を当てる前の MakeHuman 基本メッシュ**が表示されていました。リグは体型を当てた体に合わせてあるため、手では関節が指の
メッシュから最大 6 cm ずれ（指を曲げると細い棒のように潰れる）、胸を小さくする設定も効いていませんでした。生成スクリプトは
書き出し前に体型をメッシュへ焼き込むようにしたので、今の FBX にはシェイプキーがありません。
2026-11-08: 性別 0.5・胸の大きさ最小でも小さなふくらみが残るので、生成スクリプトが胸の前面（鎖骨の下〜胸の下、脇の下まで）を周りの体から張った滑らかな面で埋め直して平らにしています（`flatten_chest`、胸骨の線・腹・上胸・体の側面はそのまま、最大 14 mm 引っ込む。`--flat-chest 0` で元の胸）。

別の Humanoid モデルを試すには Resources 配下に置いて `Avian Embodiment` → `Human Avatars` に id / Resources パス /
目の位置（`Eye Offset From Head Bone`、頭ボーン基準の T ポーズでの目の位置。`Avian Voice/Embodiment/Debug/Measure Human Avatar Eye Point`
で全アバターの頭ボーン・目のボーン・頭頂・顔前面の位置を Console に出せます。MakeHuman は頭ボーンから +0.038 m 上 / +0.075 m 前、
Rocketbox 男性 +0.099 / +0.086、女性 +0.090 / +0.086（目のボーン Bip01 LEye）、Armature は +0.035 m 上 / +0.09 m 前）を足します。
モデルが無い id はパネルに「missing」と出ます。

**MakeHuman の生成**（Blender 5.1 + MPFB2 拡張、CC0 のシステムアセットが MPFB のデータフォルダにあること）:

```
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --python Tools/MakeHuman/generate_neutral_avatar.py -- --out Assets/AvianVoice/Resources/Avatars/MakeHuman/Neutral_Adult_01
```

設定は出力先の `GENERATED.md` に残ります。作り直したら **Render Avatar Hand States**（下記）で指を曲げたときの見た目を確認してください。

歌う場面では、鳥アバターでも人間アバターでも **教師の鳥（`FriendParrot`）が被験者の右隣に出て**歌います（人間アバターのときは
鳥の自分は隠れ、人間の体と教師の鳥が見えます）。教師の鳥の場所は 1 回の Play を通して同じです（2026-11-14）: 最初に出たとき
（Play 開始時の練習の場面）に、シーンに置かれた自分の鳥の右 0.72 m = (35.571, -12.188, 63.770)、鏡向きに決まり、歌う場面でも
どの条件でも同じ場所に出ます。それまでは場面が始まるたびに「その時点の自分の鳥」の横に置き直していたため、被験者の頭の位置
（鳥アバター）や、人間に切り替えた時点の位置（人間アバター）によって、練習と違う場所に出ることがありました。場所は Play 開始時に
ログ（`Teacher bird spot for this session`）に出て、記録の `singing_task_enabled` イベントにも `teacher_position` として残ります。実験者パネルの Condition のボタンで切り替えます（V キーは鳥 ⇄ 人間）。

**IOBT と人間アバターの崩れ対策**（`AvianHumanoidRetargeter`、2026-11-05）: Meta XR Core SDK の
`OVRUnityHumanoidSkeletonRetargeter` は既定で ほぼ全関節の **位置** を被験者の関節位置で上書きし（体格が違うと体が伸び縮みして
崩れる）、**指** も個別にリターゲットし（指の軸が違うと手が潰れる）、さらに骨盤に 60° の回転補正（Meta のサンプル用）を
掛けます。本プロジェクトの派生クラスはこれを **回転のみ（腰の位置だけ追従）、骨盤補正なし** に変え、SDK が T ポーズの
スナップショットを取る直前に `HumanFirstPersonDriver` がリグを T ポーズに戻します。体格差で頭の高さが
ずれる分は、毎フレーム体全体を平行移動して目をヘッドセットに合わせます（骨は触りません）。トラッキングが途切れたら
リターゲットを一時停止して腕を下ろした姿勢に戻します。

**胴体の形**（`keepAvatarTorsoShape`、2026-11-10）: SDK は各骨の「始点→終点の向き」を被験者の骨格に合わせますが、
骨盤・首・鎖骨の関節の位置はリグごとに違うので、向きを合わせると胴体が曲がり、頭が傾き、肩が引っ張られます（実機の
基準姿勢で SDK は MakeHuman の鎖骨を 35°、Armature の骨盤を 59°・鎖骨を 22° 回していました。2026-11-10 の「MakeHuman の肩が
不自然」の原因）。骨盤・背骨・胸・首・頭・鎖骨は、被験者の骨が基準姿勢（T ポーズ）から回った分だけを、アバター自身の T ポーズに
掛けます（腕・脚・手は従来どおり向きを合わせるので、本物の手足と同じ方向を指します）。この方法は骨の向きを使わないので、SDK が
向きを決められずに動かさなかった頭も被験者の頭に追従します。T ポーズは Avatar に保存された T ポーズ（`HumanoidTPose`）です。
以前の「筋肉値 0」は T ポーズではなく、腕が約 40° 下・前、肘と膝が曲がった姿勢でした（トラッキングが途切れたときの姿勢も
膝が曲がっていました）。

**手と指**（`HandFrameRetargeting`、2026-11-07）: SDK は骨を **向きだけ** で合わせる（`FromToRotation`）ので、骨の軸まわりの
回転（手のひらの向き）はアバターの T ポーズのまま残ります。アバターの手のひらの向きが Meta の基準姿勢と違うと手が回ったまま
になり、指は違う軸で曲がって潰れます（実機の記録で Armature でも手首 25°・指 最大 78° のずれ）。前腕・手首・指はこの
クラスで上書きします: 各骨に「骨の向き + 手のひらの法線（中間・末節は親の向きから運んだもの）」の座標系を関節位置だけから作り、
被験者の骨とアバターの骨で一致させるので、リグごとの骨の軸の違いに左右されません。アバター側の基準は Humanoid の T ポーズ
ではなく **スキンのバインドポーズ**（メッシュを作った姿勢）を使い、指先はメッシュの頂点から求めます（MakeHuman の指に先端の
骨が無いため）。指は既定で被験者の指に追従します（`driveFingers`）。

**手首のひねり**（`forearmTwistShare`、2026-11-12）: 手首を回す（手のひらを上 / 下に向ける）とき、本物の腕は前腕の骨（橈骨）が
回るので皮膚は肘から手首へ徐々にねじれます。Meta の体の骨格ではこのねじれは手首のひねり関節にあり、前腕の関節は回らないので、
前腕にねじれ用の骨が無いリグ（MakeHuman・Rocketbox Adult_01・Armature のどれも無い）では手首だけがねじれ、皮膚が細くくびれて
いました。肘から手までのねじれの合計を毎フレーム測り、その **半分を前腕の骨に軸まわりで回して** 受け持たせます（Unity の Humanoid
の既定値「Lower Arm Twist 0.5」と同じ割合。手そのものの向きはトラッキングどおり）。確認は `Debug/Render Wrist Twist`（手首を
±80° 回したときの前腕、修正前 / 後）。Play 中の Log Avatar State に左右のねじれ角が出ます。

人間アバターのスキンは **1 頂点あたり 4 ボーン** で計算します（`AvatarEmbodimentController` が `SkinnedMeshRenderer.quality`
を Bone4 に固定）。プロジェクトの品質設定が「Low」（2 ボーン）なので、そのままだと MakeHuman のように関節で複数の骨を
混ぜる手は曲げたときに潰れます（パーツが硬い Armature は影響なし）。

確認用メニュー（Play していないとき）:
- **Debug/Hand Retarget Self-Test (with images)**: 一方のアバターを「トラッキングされた手」役（手のひらの向きを変えた基準姿勢）
  にして、グー・指さし・サムズアップ・パーなどの姿勢をもう一方へ移し、各骨の向きと手のひらの法線の誤差を新方式と SDK 方式で
  比べます（新方式は 0.1° 程度、SDK 方式は手のひらが 46〜86° ずれる）。画像は `Temp/HandRetargetTest/`。
- **Debug/Render Avatar Hand States**: 各アバターの手を取り込みのまま / 筋肉値 0 / 指を 60° 反らす / 60° 握る、で描画
  （スキンが壊れていれば一目で分かります）。
- **Debug/Body Retarget Self-Test (with images)**: Armature を「トラッキングされた体」役にして、腕を下ろす・上げる・前に出す・
  肩をすくめる・体をひねる・首を回すなどの全身の姿勢を MakeHuman へ移し、Unity 標準の対応付け（正解）と、実行時と同じ方式
  （修正前 / 修正後）を比べます。修正後は肩関節と手首の位置が正解とほぼ一致（多くの姿勢で 0.0〜0.2 cm、修正前は 4 cm）。
  画像は `Temp/BodyRetargetTest/`（正面 = 鏡に映る姿、一人称視点）。
- Play 中の **Log Avatar State** に、手のキャリブレーション結果と、被験者の手とアバターの手の向きの誤差（度）が出ます。

**鏡と頭部**: HMD の一人称視点では **自分のアバターの頭部が見えない**ようにし、鏡（Fragilem17 の反射カメラ）には顔ごと
映します（`FirstPersonHeadHider`、鳥・人間の両方に付きます）。頭ボーン（Humanoid の Head、鳥は `parrot_Head_bone`）と
その子（くちばし・目・あご）にウェイトが付いた三角形をスキンメッシュから別のサブメッシュに分けて、一人称カメラの描画中だけ
透明マテリアルに差し替えます（マテリアル名に head / hair / opacity / eyelash を含むスロットと、頭ボーン下の別メッシュも同様）。
鳥の目の位置（`BirdFirstPersonDriver.eyeOffsetFromHeadBone`）はそのままで、頭・くちばしが視界に入らなくなります。
モデルは Read/Write 有効で取り込む必要があります（無い場合は警告を出して頭が見えたままになります）。鏡はメインカメラの描画開始イベントの中で反射を
描くため、頭部非表示は毎フレーム購読をやり直してイベントの最後に回り、「反射を顔つきで描く → 頭を消す → HMD 用に描く」の順を
保証しています（場面切替で鏡が再有効化されても崩れません）。確認は Play 中に「Debug/Capture Game View」で Game ビューを保存。

人間アバターは専用レイヤー **HumanAvatar (12)** に置かれます。鏡と池の反射用の補助ライト（鳥向けに強め）はこのレイヤーに
当たらないため、白飛びしません。**音程が合ったときの鳥の光**（`ParrotMatchVisuals` の水色の点光源、半径 2.5 m）もこのレイヤーには
当たりません（2026-11-12）。歌う場面では被験者が音程どおりに歌うたび教師の鳥がこの光を出し、隣の人間アバターが水色に照らされて
「声を出しているときだけ池の反射が映り込む」ように見えていました。呼吸の場面では、隠れている自分の鳥の光が呼吸に合わせて
人間アバターを照らしていました。光を出す鳥自体が隠れているときは光も消えます（鳥の体の発光は従来どおり）。髪（`m002_opacity`）はアルファカットアウト、肌は smoothness 0.3 で再インポートされます
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
| `Scripts/MetaBody/AvianHumanoidRetargeter.cs` | Meta のリターゲッタの派生: 回転のみ・腰の位置だけ追従・骨盤補正なし・T ポーズ取得直前のコールバック。前腕・手首・指は `HandFrameRetargeting` で上書き |
| `Scripts/BodyTracking/HandFrameRetargeting.cs` | リグに依存しない手・指のリターゲット計算（骨の向き + 手のひらの法線の座標系、スキンのバインドポーズ、メッシュからの指先） |
| `Editor/HandRetargetSelfTest.cs` | ヘッドセット無しの手のリターゲット検証と、手の描画確認メニュー |
| `Editor/BodyRetargetSelfTest.cs` | ヘッドセット無しの全身のリターゲット検証（正解 / 修正前 / 修正後の比較画像） |
| `Scripts/BodyTracking/HumanoidTPose.cs` | Avatar に保存された T ポーズをリグに適用（筋肉値 0 は T ポーズではない） |
| `Scripts/Embodiment/Avatar/HumanFirstPersonDriver.cs` | 人間: ルートをトラッキング原点に置き目をカメラに合わせる。T ポーズの再適用、トラッキング無しのフォールバック姿勢、データ途切れ時のリターゲット一時停止 |
| `Scripts/Embodiment/Avatar/FirstPersonHeadHider.cs` | 一人称カメラだけ頭部（頭ボーン以下の三角形 / 頭・髪のマテリアル）を非表示（鏡・池の反射には映る）。鳥と人間の両方 |
| `Scripts/Study/StudyFlowController.cs` | 4 場面フロー（`TaskManager.SetFlow`）、`SingingTrialTask` の割り当て、`StudyRecorder` の開始、**条件（アバター × 声のフィードバック）** の設定と記録 |
| `Scripts/Study/SingingTrialTask.cs` | 試行形式の歌う場面（教師の 1 音 → 応答、発声検出、30 回で休む場面へ） |
| `Scripts/Audio/MicPassthrough.cs` | 人間の声の条件のフィードバック（2026-11-12 から）: マイク入力を変換せずにヘッドホンへ出す。マイクのリングバッファを一定の遅れで再生し、遅れを測って保つ |
| `Scripts/Audio/HumanVoiceSynth.cs` | 合成の人間の声（2026-11-12 からは人間の声の手本と、`Human Voice From Microphone` オフのときだけ）: 被験者の音程・音量に追従して歌う母音「あ」（フォルマント合成）。`BirdVoiceMapper.SetFeedback` で鳥の声 / 人間の声 / なしを切替。2026-11-12: 息の成分を聞こえる量に（倍音対雑音比 約 26 dB）、F1・F2 を直列の共振器にして基音を自然な強さに、歌う音量で声質（声門の開き・明るさ・息の量）が変わるようにしました（`Effort Amount` = 0 で声質の連動なし）。音量は以前と各音高で ±1 dB 以内、5 kHz 以上は以前と同じです |
| `Editor/HumanVoiceRenderTest.cs` | 人間の声を Play なしで WAV に書き出す（メニュー **Debug/Render Human Voice Test Files (WAV)** → `StudyData/diagnostics/human_voice_<時刻>/`）。聴き比べ用（弱・中・強、クレッシェンド、音高違い）と、揺らぎを切った測定用（`measure/`） |
| `Scripts/Study/StudyRecorder.cs` | `events.jsonl` / `trials.csv` / `mic.wav` / `output.wav` / 試行切り出しの書き出し |
| `Scripts/Study/PcAudioMonitor.cs` | Unity 出力の PC スピーカーへのミラー（winmm）、Windows 既定出力のミュート検出 |
| `Scripts/Study/ListenerOutputTap.cs`, `WavWriter.cs` | AudioListener のミックス取得、16 bit WAV ストリーム書き込み |
| `Resources/Avatars/MakeHuman/Neutral_Adult_01/` | 中性の体（id `neutral`、MakeHuman、CC0）。`Tools/MakeHuman/generate_neutral_avatar.py` で生成 |
| `Resources/Avatars/Rocketbox/` | 男性・女性の体（id `male` / `female`、Microsoft Rocketbox、MIT）。取り込み設定は `Editor/RocketboxAvatarImport.cs` |
| `Editor/MakeHumanAvatarImport.cs` | MakeHuman FBX の Humanoid 取り込み（Read/Write 有効）とマテリアル設定 |
| `Resources/Avatars/UnityArmature/` | ロボット型マネキン（id `mannequin`、選択肢に出さない。Unity Starter Assets Armature、Unity Companion License、`LICENSE.md` 参照） |
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
avatar, the avatar eyes locked to the XR camera, and an avatar switch (bird `PlayerParrot` ⇄ human). Since 2026-11-12
the experimenter chooses the human body among three: `neutral` = MakeHuman (CC0; gender 0.5, flat chest, no face
texture / hair / clothes, one plain matte colour, generated by `Tools/MakeHuman/generate_neutral_avatar.py`), `male` /
`female` = Microsoft Rocketbox Male_Adult_01 / Female_Adult_01 (MIT). The Unity Starter Assets "Armature" mannequin
stays in the project as `mannequin` with `selectable` off (tick it to offer it again). Ids changed over time (`neutral`
was the Armature from 2026-11-05 to 11-12), so every event line and trial row now also records `avatar_model` (the
Resources path), and each session logs the `human_avatars` catalog. The Rocketbox bodies' stored T-pose has the head
tipped back and the mouth open, so the rest pose takes the torso, head and face bones from the skin bind pose
(`HumanoidTPose`; check with Debug / Render Human Rest Poses). The generator bakes the body-shape targets into the mesh before the FBX export: left as shape keys
they reached Unity as dropped blend shapes, so the unshaped base mesh showed (chest visible) with the rig fitted to the
shaped body (hand joints up to 6 cm off the fingers, which collapsed when bent). Retargeting is rotation-only (hips
position only, no pelvis tweak, T-pose re-applied right before the SDK snapshot) so the avatar keeps its proportions.
Forearms, wrists and fingers are retargeted by `HandFrameRetargeting`: full bone frames (direction + palm normal) from
joint positions, matched between the tracked bind pose and the avatar's skin bind pose, because the SDK's
direction-only match leaves the hand rolled (measured on Quest data: wrists 25 deg, fingers up to 78 deg off). Human
avatar skins use 4 bones per vertex (the project's "Low" quality level allows 2, which collapses blended finger joints).
Check without a headset with Debug / Hand Retarget Self-Test (with images) and Debug / Render Avatar Hand States.
The torso (hips, spine, chest, neck, head, collarbones) keeps the avatar's own shape (`keepAvatarTorsoShape`, 2026-11-10):
it takes the tracked bones' rotation away from the tracker's bind pose, applied to the avatar's T-pose, instead of the
SDK's per-bone direction match, which bent rigs whose pelvis / neck / collarbone joints sit elsewhere (on real Quest
data the SDK turned MakeHuman's collarbones 35 deg, the Armature's pelvis 59 deg). The rest pose handed to the SDK is
the Avatar's stored T-pose (`HumanoidTPose`), not muscle zero (arms 40 deg down and forward, elbows and knees bent).
Check with Debug / Body Retarget Self-Test (with images). The study has four conditions, avatar (bird / human) x
voice feedback (the participant's own voice heard as a bird voice / as it is from the microphone, or not at all), chosen from the
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
