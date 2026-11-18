# 教師の鳥の参照音（3 音）

歌う場面で教師の鳥が被験者に提示する 3 つの音です（2026-11-14 時点の設定）。

Unity のプロジェクトにこの 3 音の音声ファイルはありません。試行のたびに `BirdSongSynth`（`Assets/AvianVoice/Scripts/Audio/BirdSongSynth.cs`）が
`AvianVoiceSettings.asset` の値からその場で合成しています（`SingingTrialTask` → `BirdVoiceMapper.PlayGuideNote` → `BirdSongSynth.PlayChirp`）。
ここにあるのは、その出力をファイルにしたものです。

| 目標（被験者が歌う高さ） | 教師の鳥が出す高さ（8 倍） | 提示された音（Unity の出力） | 合成コードからの書き出し |
|---|---|---|---|
| B3 246.94 Hz | B6 1975.5 Hz | `1_B3_as_heard_bird_bird-voice_trial001.wav` | `1_B3_synth_clean_B6_1976Hz.wav` |
| D4 293.66 Hz | D7 2349.3 Hz | `2_D4_as_heard_bird_bird-voice_trial011.wav` | `2_D4_synth_clean_D7_2349Hz.wav` |
| F#4 369.99 Hz | F#7 2959.9 Hz | `3_Fs4_as_heard_bird_bird-voice_trial021.wav` | `3_Fs4_synth_clean_Fs7_2960Hz.wav` |

すべて 48 kHz、モノラル、16 bit です。

- **提示された音**: セッション `20261114_130508_P002` の `output.wav`（被験者が聞いた音のミックス）から、各音の最初の試行を切り出したもの
  （音の 0.05 秒前から 1.1 秒間）。同名の `_spectrogram.png` はそのスペクトログラム（0〜8 kHz）です。
- **合成コードからの書き出し**: `BirdSongSynth` の計算を Python に写して（`teacher_notes.py`）同じ設定で作ったもの。Unity が書き出したファイルではありません。
  提示された音と高さ・長さ・音量が一致します。雑音成分とトリルの位相は試行ごとに違うので、波形は一致しません。
- `teacher_notes.csv`: 3 音の設定値と、同じセッションの 120 回（各音 40 回）の実測のまとめ。
- `teacher_notes_all_trials.csv`: 120 回それぞれの実測。

## 音の作り

- 正弦波 + 第 2 倍音（0.18）+ 音の高さ付近に絞った雑音（0.06）
- トリル: 27.2 Hz で ±0.6 半音。同じ周期で振幅が 0.70〜1.0 倍に揺れる
- 出だし: 2.5 半音下から 0.04 秒で目標の高さへ
- 長さ: ゲート 0.9 秒、立ち上がりの時定数 8 ms、減衰 15 ms（振幅がピークの 10 % 以上の区間は 0.93 秒）
- 音量: `output.wav` 上で RMS −9.7 dBFS、ピーク約 0.62。耳元での大きさは PC の音量設定によります

## 作り直すとき

ピッチや `AvianVoiceSettings.asset` の合成の値を変えたら、このフォルダの音は古くなります。`teacher_notes.py` の先頭にある設定値（`S`）を合わせてから:

```bash
python Tools/TeacherReferenceNotes/teacher_notes.py StudyData/<セッション> Tools/TeacherReferenceNotes
```

スペクトログラムは ffmpeg で作っています（`aresample=16000,showspectrumpic=s=900x400:legend=1:scale=log:fscale=lin`）。
