# Avian_Lab_Embodiment — 体トラッキングで動くアバターとアバター切替

`Assets/AvianVoice/Scenes/Avian_Lab_Embodiment.unity` は `Avian_Lab.unity` のコピーに、次の 3 点を足したシーンです。

1. **Quest の体トラッキング（IOBT / Full Body）** で、ユーザーの体の向き・腕・（対応機種では）脚がアバターに反映される。
2. **視点がアバターの目に一致する**（アバターの目が XR カメラの位置に来るように毎フレーム合わせる）。
3. **アバター切替モード**: 鳥（`PlayerParrot`）⇄ 人間（Microsoft Rocketbox `Male_Adult_01`）。

シーン内の `Avian Embodiment` オブジェクトに `AvatarEmbodimentController` が付いています。元の `Avian_Lab` の 6 場面・
音声・鏡・池などはそのまま動きます。

> **注意**: 体トラッキングとアバター切替は **Avian_Lab_Embodiment** を開いて Play したときだけ動きます。
> 元の `Avian_Lab` を Play しても鳥は止まり木に乗ったままで、ユーザーの体には追従しません。
> メニュー **Avian Voice → Embodiment → Open Avian_Lab_Embodiment** で開けます。

## 操作（実験者は PC、被験者はヘッドセット）

被験者のコントローラ入力は **既定で無効**です（`Avian Embodiment` の `Participant Controllers Enabled`）。
スティック旋回、メニューボタン、X / B ボタンはすべて無視され、被験者は頭・手・体・声だけで参加します。
XR リグの手メッシュ・関節・レイ表示も既定で非表示です（`Participant Hands Visible`、トラッキング自体は動き続け、
翼や人間アバターの腕がその代わりに見えます）。

実験者は PC 側で操作します。メニュー **Avian Voice → Experimenter Panel (実験者パネル)** を開くと、Play 中に

- 場面 1〜6 のボタン / 前の場面・次の場面
- 場面 3 で「教師の鳥に歌わせる」（被験者の Space / R と同じ）
- アバター: 鳥 / 人間
- 被験者コントローラの有効/無効、手の表示、視界下の状態カードの表示
- 現在の場面・アバター・体トラッキング状態の表示、**音声出力メーター**、詳細ログ出力

### 音が聞こえないとき

パネルの「音声出力 (PC 側)」メーターが振れていれば Unity は音を出しています。聞こえないのは出力先の問題です。
Unity の音は **Windows の既定の再生デバイス**に出ます（ログの `XR: Error setting active audio output driver. Falling back to default.`
はヘッドセットへの自動切替が失敗して既定デバイスに落ちた印）。Link でヘッドセット側に聞かせるには、Windows の
サウンド設定で既定の出力を「Headphones (Oculus Virtual Audio Device)」にするか、Meta Quest Link アプリの
設定でリンク時にオーディオを切り替えるようにしてください。PC スピーカーで聞く場合はその逆です。

### 人間アバターの見た目

人間アバターは専用レイヤー **HumanAvatar (12)** に置かれます。鏡と池の反射用の補助ライト（鳥向けに強め）はこのレイヤーに
当たらないため、白飛びしません。髪（`m002_opacity`）はアルファカットアウト、肌は smoothness 0.3 で再インポートされます
（`Avian Voice → Embodiment → Reimport Human Avatar`）。

ができます。キーボードでも同じ操作ができます（Game ビューにフォーカスがある時）。

| したいこと | PC キーボード | 実験者パネル |
|------------|---------------|--------------|
| 場面を変える | **1〜6**、**N**（次）、←（前） | 場面ボタン |
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
| `Resources/Avatars/Rocketbox/` | 人間アバター（MIT ライセンス、`README.md` 参照） |

鳥の「目」の位置は `BirdFirstPersonDriver.eyeOffsetFromHeadBone`（頭ボーン基準、ルート座標系）で調整できます。
人間アバターを別の Humanoid モデルに替えるには、Resources 配下に置いて `humanAvatarResourcePath` を変更してください。

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
avatar, the avatar eyes locked to the XR camera, and an avatar switch (bird `PlayerParrot` ⇄ human Microsoft
Rocketbox `Male_Adult_01`, MIT licensed). Toggle with **V** (keyboard) or the **left X** button (Quest).

Setup: open the project (Meta XR Core SDK 207.0.1 is pulled from the Meta scoped registry), then run
**Avian Voice → Embodiment → Configure Everything**, build for Android and allow body tracking on the headset.
Without a headset the avatars follow the head only. See the table above for the scripts involved.
