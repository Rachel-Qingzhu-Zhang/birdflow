# Avian Voice 场景与任务说明（中文）

本文档说明 **Birdflow / Avian Voice** 项目中每个场景的用途、六个 Birdy 任务的内容、键盘/VR 交互方式，以及主要设置资源。适合被试说明、实验设计与日常调试查阅。

**主入口场景**：`Assets/AvianVoice/Scenes/Avian_Lab.unity`  
**相关资源**：`Assets/AvianVoice/Settings/AvianVoiceSettings.asset`、`PlatformProfile.asset`

更宏观的项目背景见：[PROJECT_OVERVIEW_CN.md](PROJECT_OVERVIEW_CN.md)  
英文操作与排错见：[SCENES_GUIDE.md](SCENES_GUIDE.md)

---

## 一、场景总览

| 场景文件 | 用途 | 推荐用法 |
|----------|------|----------|
| **Avian_Lab.unity** | 主实验室：森林布景 + 双鹦鹉 + XR + 六任务同场景切换 | **日常开发与实验主入口** |
| Task_PitchMatching.unity | 旧版音高匹配（独立场景） | 备用；Lab 内按 `4` 为 Singing |
| Task_FlightControl.unity | 旧版飞行（独立场景） | 备用；Lab 内按 `3` 为 Flying |
| Task_SongPerformance.unity | 旧版鸣叫序列（独立场景） | 备用；Lab 内按 `5` 为 Call Friends |

当前推荐流程：**只打开 Avian_Lab**，用数字键 `1`–`6` 切换任务，无需反复加载场景（`TaskManager` 为 **InScene** 模式）。

---

## 二、Avian_Lab 场景内容

### 2.1 环境与布景

- **森林环境**：来自 ForestVision 等资产，针叶林氛围。
- **池塘环境（AvianPond）**：来自 Fine3D `Environment_Pond`，通过菜单 **Avian Voice → Scene Setup → Merge Fine3D Pond into Lab** 合并到场景中，默认偏移至 `(80, 0, 60)`，与鸟架区分离。
  - 含地形、睡莲、木桥、水面涟漪粒子、水下锦鲤（`koi_fish.fbx`，由 `PondFishSpawner` 生成）。
  - **平面反射水面**（`PondWaterReflection` + `AvianVoice/PondWater` Shader）：任务 3/4/5 开启，可看到飞行中玩家鸟的实时倒影。水面为有机池塘形状（非矩形），带波纹法线与岸边渐隐。
  - 若水面仍是矩形：菜单 **Avian Voice → Scene Setup → Upgrade Pond Water Surface** 一键升级网格与材质参数。
  - 任务 1/2/6 池塘反射关闭，避免与镜子双重平面反射影响 VR 性能。
- **栖木**：场景根物体 `Cylinder`（灰色圆柱），两只鹦鹉站在其两侧。
- **XR Rig**：`XR Origin Hands (XR Rig)`（XRI Hands Demo 预制体），提供头显相机与手部追踪。
- **运行时自动创建**（若缺失）：
  - `AvianLabRuntimeSetup`：麦克风、语音分析、呼吸、HUD、任务切换等。
  - `TaskManager`、`TaskRoot`：六个任务脚本挂载点。
  - 头显前方 **TaskFlowUI**（任务名、说明、分数）与 **TrainingHUD**（呼吸/音高/响度条）。

### 2.2 两只鹦鹉（场景预置）

场景中应有两只 **Parrot** 预制体实例，**不再在运行时复制**：

| 物体名 | 角色 | 头顶标记 | 行为 |
|--------|------|----------|------|
| **FriendParrot** | 鸟友（演示者） | **橙色** 小球 | 自动按节奏呼吸；任务中「教你」吸气/呼气、唱参考音 |
| **PlayerParrot** | 玩家化身 | **浅蓝色** 小球 | 按 **Space** 呼吸；镜子中显示为玩家鸟身（任务 1/2/6） |

**摆放方式**：

- 在 Hierarchy 中直接拖动两只鸟到栖木上合适位置，**Ctrl+S 保存场景**。
- 或菜单：**Avian Voice → Place Two Parrots On Perch**（自动左右摆放到 `Cylinder` 两侧）。
- 可选组件 **Bird Scene Role**（Friend / Player）标明角色；也可仅靠物体名 `FriendParrot` / `PlayerParrot`。

**鸟友教学反馈**（橙色鸟，`FriendTeachingCue` + `FriendBreathVisual`）：

- 胸口 **橙色呼吸球**随吸气胀大、呼气缩小。
- 吸/呼切换时播放不同音高的 **chirp** 提示。
- 胸廓骨骼缩放更明显，便于模仿节奏。

### 2.3 镜子（Mirror）

- 实现：`MirrorBodySetup` + `AvianMirror` Shader + RenderTexture。
- 镜子显示 **PlayerParrot**（玩家鸟）的第三人称影像。
- **自动开关**（按任务）：
  - **开启**：任务 1 Meet、任务 2 Breath Sync、任务 6 Reflection
  - **关闭**：任务 3 Flying、任务 4 Singing、任务 5 Call Friends
- **池塘水面反射**（与镜子互斥，按任务自动切换）：
  - **开启**：任务 3 Flying、任务 4 Singing、任务 5 Call Friends
  - **关闭**：任务 1 Meet、任务 2 Breath Sync、任务 6 Reflection
- 总开关：`AvianVoiceSettings.mirrorEnabled`（默认 true）。

### 2.4 语音与身体系统（全场景共用）

| 系统 | 作用 |
|------|------|
| **MicrophoneCapture + VoiceAnalyzer** | 采集麦克风，输出音高 Hz、响度、voiced |
| **BirdVoiceMapper** | Transform / Assistive 两种嗓音映射模式 |
| **BreathSimulator** | Space 吸气 / 松开呼气，驱动玩家鸟身体 |
| **BirdFriendDemonstrator** | 鸟友独立呼吸时间轴（4/4 或 6/6），挂在 `TaskRoot` |
| **SessionRecorder** | 约 30 Hz 写入 `avian_trials.jsonl` |

**Transform 模式（默认）**：你的发声触发 **鸟鸣**（`BirdCallLibrary`），强调「变成鸟在叫」。  
**Assistive 模式**：保留人声听感并变调/混响。按 **`T`** 切换。

---

## 三、六个任务详解（Avian_Lab 内按键切换）

头显前方 **TaskFlowUI** 显示：任务标题、实时说明、Score。  
切换任务后，仅对应任务脚本 `enabled = true`，其余关闭。

### 任务 1 — Meet Your Bird（按键 `1`）

**任务 ID**：`meet`

**设计意图**：初次与「鸟自我」建立联结——通过跟鸟友同步呼吸，在镜子中看见并排两只鸟。

**场景内容**：

- `FriendParrot` + `PlayerParrot` 并排站在栖木上。
- 镜子 **开启**。
- 鸟友演示 **4 秒吸气 / 4 秒呼气**（`BirdFriendDemonstrator.Intro4x4`）。

**你要做什么**：

1. 观察橙色鸟胸口呼吸球与听 chirp 节奏。
2. **按住 Space** 吸气，**松开** 呼气，尽量与鸟友同相位、同深度。
3. 呼吸同步度 ≥ **80%** 且连续保持 **10 秒** 即成功。

**HUD 示例**：

```
Orange bird teaches: watch chest + listen. Match 4s in / 4s out with Space.
Friend: INHALE ↑ (3.2s)  |  You: hold Space  |  sync 72% — hold 6.5s
```

**成功反馈**：五次连续 chirp（`SuccessChirpBurst`）、鸟友发光脉冲、提示 "Success! You met your bird self."

**调试**：按 **`C`** 瞬间完成（正式实验前在 Inspector 关闭 `enableDebugCheat`）。

---

### 任务 2 — Breath Sync（按键 `2`）

**任务 ID**：`breath_sync`

**设计意图**：在两种呼吸模式（4/4 与 6/6）下训练与鸟友的节律同步。

**流程**：

| 阶段 | 鸟友节奏 | 成功条件 |
|------|----------|----------|
| Pattern A | 4s 吸 / 4s 呼 | 同步 ≥80%，连续 **8 秒** |
| Pattern B | 6s 吸 / 6s 呼 | 同上 |

- 同步掉线（<80%）时，鸟友会 **重新开始** 当前模式（`RestartPattern`）。
- 镜子 **开启**。

**交互**：同任务 1（Space 呼吸 + 看/听鸟友）。

**HUD 示例**：

```
PatternA — Friend: EXHALE ↓ (2.1s)  |  You: release Space  |  sync 85% — hold 4.0s
```

---

### 任务 3 — Flying（按键 `3`）

**任务 ID**：`flying`

**设计意图**：用呼吸能量隐喻飞行——深吸气上升，呼气滑翔下降（**不再用音高控高**）。

**场景内容**：

- 镜子 **关闭**；**池塘水面反射开启**（若已合并 AvianPond）。
- 默认起点在池塘上空（`XrRigTaskStartPoses` 的 `flying` 位姿）；低头可见玩家鸟在水面的倒影。
- `FlightControlTask` 挂在 **XR Origin** 上，带 `CharacterController`。
- 鸟友鹦鹉可进入伴飞展示（`ParrotFlightPresentation`）。

**你要做什么**：

| 操作 | 效果 |
|------|------|
| **按住 Space**，呼吸深度 0→0.5 | 缓慢上升 |
| **深吸**（深度 0.5→1） | 快速上升 |
| **松开 Space** / 呼气 | 下降、滑翔 |
| 稳定呼吸（深度适中） | 分数升高，伴飞鸟进入滑翔姿态 |

**移动**：XR Origin 持续 **向前**（约 1.75 m/s）+ 垂直分量由呼吸决定。

**桌面预览**：WASD 移动视角锚点；Space 仍控制呼吸。

---

### 任务 4 — Singing Branch（按键 `4`）

**任务 ID**：`singing`

**设计意图**：四音旋律模仿——鸟友先唱，你跟音高 + 保持呼吸。

**旋律**（默认 Hz）：E4(330) → G4(392) → A4(440) → B4(494)

**你要做什么**：

1. 听橙色鸟友播放当前参考音（`BirdCallLibrary`）。
2. 对着麦克风哼/唱，使音高接近目标（容差约 **40 Hz**）。
3. 同时保持呼吸（Space），深度 >0.25；深吸（≥0.7）可提升得分权重。
4. 每个音稳定匹配约 **0.45 秒** 进入下一音。

**HUD 示例**：

```
Orange bird sings E4 first — match its pitch + keep breath (Space).
Hold E4 — pitch 328/330 Hz, breath 0.65
```

**成功**：四音完成后播放成功 chirp 串。

---

### 任务 5 — Call Friends（按键 `5`）

**任务 ID**：`call_friends`

**设计意图**：进入任务时前方已有鸟群伙伴悬空栖息；用旋律 + 呼吸同步「呼唤」它们飞来环绕，形成交流合唱。

**流程**：

1. **进入任务**：玩家视角前方扇形内生成 **7** 只小鸟（`CallFriendsCompanionBehaviour`），idle 悬空栖息，仅 Task 5 存在，离开任务自动销毁。
2. 鸟友演示 Pattern A 呼吸（4/4）。
3. 依次模仿两个音：**E4、G4**（可配置 `melodyHz`）。
4. 评分 = **音高匹配 60%** + **呼吸同步 40%**；呼吸同步需 ≥ **55%**；稳定约 **0.45 s** 触发。
5. 每完成一音，**最近的未响应 idle 鸟**飞来绕玩家环绕并 chirp：
   - 第 1 音（E4）：**1** 只飞来
   - 第 2 音（G4）：再 **2** 只飞来，随后**剩余全部**加入环绕
6. 鸟群到齐后短合唱（E4/G4 各一轮，鸟群交错回应）+ `SuccessChirpBurst`。

**镜子**：关闭。池塘水面反射开启。

**HUD 示例**：`Call E4 + breath sync 68%` → `Flock arrived — friends communicate!`

---

### 任务 6 — Reflection（按键 `6`）

**任务 ID**：`reflection`

**设计意图**：回顾本次体验（当前为 **占位** 实现，正式问卷待接）。

**内容**：

- 镜子 **开启**。
- 显示三项缓存分数（Breathing / Voice / Synchronization，来自前面任务）。
- 玩家鸟（`PlayerParrot`）逐渐淡出。
- 约 0.5 s 后播放告别 chirp。
- HUD 占位文案：`How did it feel to become a bird? (placeholder — press N for next task)`

**交互**：阅读分数即可；按 **`N`** / **`]`** 可切到下一任务。

---

## 四、全局交互速查

### 4.1 任务切换

| 按键 | 功能 |
|------|------|
| **`1`** | Meet Your Bird |
| **`2`** | Breath Sync |
| **`3`** | Flying |
| **`4`** | Singing Branch |
| **`5`** | Call Friends |
| **`6`** | Reflection |
| **`N`** 或 **`]`** | 下一任务（循环 1→6→1） |

### 4.2 呼吸与嗓音

| 输入 | 功能 |
|------|------|
| **Space 按住** | 吸气（玩家鸟 `PlayerParrot`） |
| **Space 松开** | 呼气 |
| **对着麦克风发声** | 音高分析、鸟鸣映射、任务评分 |
| **`T`** | Transform ↔ Assistive 语音模式 |

### 4.3 调试（Pilot）

| 按键 | 功能 |
|------|------|
| **`C`** | 当前任务作弊：瞬间成功或推进（Meet / Breath Sync / Singing / Call Friends 等） |

正式实验前在各任务 Inspector 将 **Enable Debug Cheat** 设为 false。

### 4.4 桌面无头显预览

| 输入 | 功能 |
|------|------|
| **WASD** | 平移 |
| **Q / E** | 降 / 升 |
| **Shift** | 加速 |
| **右键 + 鼠标** | 环顾 |

翅膀：桌面默认 **模拟扑翼**（`PlatformProfile.useSimulatedHands = true`）。

### 4.5 HUD 面板

| 面板 | 内容 |
|------|------|
| **TaskFlowUI**（中上） | 任务名、实时说明、Score |
| **TrainingHUD**（右侧） | 呼吸条、响度、音高 Hz、延迟 ~ms |
| **ModeToggle**（左侧） | `Transform (T)` / Assistive 模式 |

右侧提示行可能仍显示 `1-4=Task`；实际已支持 **`1`–`6`**。

---

## 五、独立任务场景（Task_*.unity）

若将 `TaskManager.switchMode` 改为 **LoadScene**，或通过 Build Settings 单独打开：

| 场景 | 加载后默认任务 | 与 Lab 按键对应 |
|------|----------------|-----------------|
| Task_PitchMatching | `singing`（映射） | 与 Lab 任务 4 类似旧逻辑 |
| Task_FlightControl | `flying` | 与 Lab 任务 3 类似 |
| Task_SongPerformance | `call_friends` | 与 Lab 任务 5 类似 |

这些场景多为 **BasicScene 模板**（地面 + 光），**不含** Avian_Lab 的森林与双鹦鹉布置；开发请以 **Avian_Lab** 为准。

---

## 六、设置与配置文件

### 6.1 AvianVoiceSettings.asset

路径：`Assets/AvianVoice/Settings/AvianVoiceSettings.asset`

| 分组 | 常用字段 | 说明 |
|------|----------|------|
| **Audio** | `sampleRate` 48000 | 麦克风采样率 |
| | `frameSize` 1024 | 分析窗长度（≈21 ms @48k） |
| | `analysisHopSize` 256 | 分析步长，越小延迟越低 |
| | `voicedThreshold` | 判定「有声」阈值 |
| | `loudnessGain` | 响度增益 |
| | `dspBufferSizeOverride` 256 | Unity 音频缓冲，128–256 更低延迟 |
| **Transform 鸟鸣** | `referencePitchHz` | 变速参照音高 |
| | `transformCooldownUsesClipLength` | 触发间隔跟 clip 时长 |
| | `transformBlockOverlap` | 防止鸟鸣叠层 |
| **身体/呼吸** | `inhaleSpeed` / `exhaleSpeed` | Space 呼吸快慢 |
| | `chestScaleMax` 等 | 玩家鸟胸廓缩放幅度 |
| | `breathDrivesWings` | 呼吸是否驱动翅膀张开 |
| **实验开关** | `mirrorEnabled` | 镜子总开关 |
| | `wingMappingEnabled` | 翅膀与手追踪映射 |
| | `recorderSampleHz` 30 | 日志采样率 |

### 6.2 PlatformProfile.asset

路径：`Assets/AvianVoice/Settings/PlatformProfile.asset`

| 字段 | 桌面默认 | Quest 建议 |
|------|----------|------------|
| `useSimulatedHands` | true（无手追踪时模拟拍翅） | false（用手追踪） |
| `desktopDspBufferSize` | 128 | — |
| `questDspBufferSize` | — | 512 |
| `requestMicrophonePermission` | true | true |

### 6.3 场景与编辑器菜单

| 菜单项 | 作用 |
|--------|------|
| **Avian Voice → Setup Project** | 生成 Settings、Build Settings、基础 Lab |
| **Place Two Parrots On Perch** | 在打开的场景摆放 FriendParrot + PlayerParrot |
| **Apply Avian Lab Setup (Parrot + Breath)** | 绑定 XR Bootstrap、鹦鹉组件 |
| **Wire Bootstrap On XR Rig** | 修复麦克风/鸟鸣/HUD 引用 |
| **Fix Duplicate XR Rigs In Open Scene** | 删除重复 XR Origin |

### 6.4 实验日志

- 文件：`avian_trials.jsonl`
- 位置：`Application.persistentDataPath`（Windows Editor 通常在 `%USERPROFILE%\AppData\LocalLow\...\Birdflow\`）
- 典型字段：`timestamp`, `taskId`, `sceneId`, `voiceMode`, `pitchHz`, `loudness`, `breathDepth`, `breathSyncScore`, `score`, `latencyMs`

---

## 七、推荐体验顺序（被试 / 自测）

1. 打开 **Avian_Lab**，Play，允许麦克风。
2. **`1` Meet**：跟橙色鸟友呼吸 10 秒，看镜子里的两只鸟。
3. **`2` Breath Sync**：完成 4/4 与 6/6 两阶段。
4. **`3` Flying**：深吸上升、呼气下降，感受向前飞。
5. **`4` Singing**：听鸟友唱四音，跟唱 + 呼吸。
6. **`5` Call Friends**：旋律 + 呼吸召唤鸟群。
7. **`6` Reflection**：查看三项分数与告别。

全程可随时 **`T`** 切换语音模式；桌面用 **Space + 麦克风** 即可完整走通（VR 上手部追踪增强翅膀映射）。

---

## 八、常见问题

**Q：只看到一只鹦鹉？**  
- Hierarchy 确认同时存在 `FriendParrot` 与 `PlayerParrot`；运行菜单 **Place Two Parrots On Perch** 并保存场景。

**Q：鸟友不像在「教」？**  
- 请在任务 **1 或 2** 测试（鸟友自动呼吸）；看橙色胸口球与听 phase chirp；HUD 应显示 `Friend: INHALE/EXHALE`。

**Q：镜子是黑的？**  
- 确认 `mirrorEnabled` 为 true；`MirrorBodySetup` 的 `birdBodyRoot` 指向 `PlayerParrot`。

**Q：没有鸟鸣？**  
- 菜单 **Wire Bootstrap On XR Rig**；确认 `BirdCallLibrary` 与 `transformBirdSource` 已绑定。

**Q：呼吸条不动？**  
- 按住 **Space**；确认 `BreathSimulator` 在 XR Origin 的 Bootstrap 上已初始化。

---

## 九、脚本与任务对应关系

```
Avian_Lab.unity
├── FriendParrot / PlayerParrot     ← BirdSceneRoles（场景预置，不复制）
├── Cylinder                        ← 栖木
├── XR Origin Hands                 ← AvianAppBootstrap, FlightControlTask
├── TaskRoot
│   ├── BirdFriendDemonstrator      ← 鸟友呼吸时间轴
│   ├── MeetBirdTask                ← 任务 1
│   ├── BreathSyncTask              ← 任务 2
│   ├── SingingBranchTask           ← 任务 4
│   ├── CallFriendsTask             ← 任务 5（运行时生成 CallFriendsCompanionBehaviour 鸟群）
│   ├── CallFriendsCompanionBehaviour / CallFriendsCompanionLayout  ← 任务 5 鸟群栖息与飞来
│   └── ReflectionTask              ← 任务 6
└── TaskManager + TaskModeKeyboardSwitcher（按键 1–6）
```

---

*文档版本与当前代码一致（六任务 InScene 模式、场景双鹦鹉、鸟友教学提示）。若增删任务或场景，请同步更新本文档与 `TaskManager`。*
