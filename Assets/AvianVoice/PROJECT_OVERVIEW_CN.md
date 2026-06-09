# Birdflow / Avian Voice 项目总览（中文）

## 一、项目简介

**Birdflow** 是 Unity 6 上的 VR 研究原型项目，核心模块为 **Avian Voice**（`Assets/AvianVoice/`）。项目面向 **CHI 2027** 相关研究，探索：

- **鸟身幻觉（bird body illusion）**：第一人称视角下的鸟形身体、翅膀与喙部反馈；
- **嗓音 → 鸟鸣映射**：用户发声经实时分析后，触发鸟鸣或辅助听感（Transform / Assistive 两种模式）；
- **呼吸与身体联动**：用键盘或 VR 交互模拟吸气/呼气，驱动胸腔与翅膀动画；
- **结构化训练任务**：音高匹配、飞行控制、鸣叫序列等，用于评估「发声 + 呼吸」在鸟身体验中的学习与表现。

**推荐开发入口**：打开 `Assets/AvianVoice/Scenes/Avian_Lab.unity`，按 Play 后在同一场景内用数字键 `1`–`6` 切换 Birdy 六场景任务（无需反复加载独立场景）。

**场景 / 任务 / 交互 / 设置（中文详述）**：[SCENES_AND_TASKS_CN.md](SCENES_AND_TASKS_CN.md)  
更细排错与调参：[SCENES_GUIDE.md](SCENES_GUIDE.md) · 英文快速上手：[README.md](README.md)

---

## 二、技术环境与平台

| 项目 | 说明 |
|------|------|
| 引擎 | Unity **6000.3.4f1**（或兼容 Unity 6） |
| 渲染 | URP |
| XR | OpenXR；桌面可用 XR Simulation；Quest 使用 Meta OpenXR |
| 交互 | XR Interaction Toolkit 3.x、XR Hands |
| 音频 | 麦克风采集 → 音高/响度分析 → `BirdCallLibrary` 或 Audio Mixer |
| 实验日志 | `avian_trials.jsonl`（`Application.persistentDataPath`） |

**平台差异简述**：

- **桌面**：XR 模拟或 `DesktopFlyCamera`（WASD + 右键环顾）；DSP 缓冲偏小（如 256）以降低延迟。
- **Quest（Android）**：建议外接 USB/OTG 麦克风；DSP 缓冲约 512；构建前配置 `PlatformProfile`。

---

## 三、项目内容结构（主要资源）

| 目录 / 资源 | 作用 |
|-------------|------|
| `Assets/AvianVoice/` | 核心：脚本、场景、设置、音频、编辑器菜单 |
| `Assets/ForestVision/` | 森林环境资产；`Sample1.unity` 可作任务场景布景来源 |
| `Assets/Parrot/` | 鹦鹉模型与动画；任务中作为世界中的参考鸟/伴飞鸟 |
| `Assets/VRTemplateAssets/`、`Assets/Samples/XR Interaction Toolkit/` | VR 模板与 **XR Origin Hands** 等预制体 |
| `Assets/Scenes/BasicScene.unity` | Avian 场景的基础模板（地面 + 光照） |

运行时，名称以 `Avian` 或 `Task_` 开头的场景会由 **`AvianVoiceSceneAutoLoad`** 与 **`AvianLabRuntimeSetup`** 自动挂载麦克风、语音分析、鸟身控制、HUD、任务管理等组件（场景文件本身通常不预置完整 XR Rig，需首次手动拖入 Prefab 并保存）。

---

## 四、场景与任务一览

### 4.1 场景总表（Birdy 六任务 — `Avian_Lab` 内按键切换）

| 按键 | 任务 ID | 显示名 | 核心目标 |
|------|---------|--------|----------|
| `1` | `meet` | Meet Your Bird | 与鸟友 4/4 呼吸同步（>80%×10s），镜子 + 并排双鹦鹉 |
| `2` | `breath_sync` | Breath Sync | Pattern A（4/4）与 B（6/6）呼吸同步 |
| `3` | `flying` | Flying | 呼吸能量驱动升/滑翔/降（非音高控高） |
| `4` | `singing` | Singing Branch | 四音旋律 + 呼吸联合模仿 |
| `5` | `call_friends` | Call Friends | 旋律 + 呼吸呼唤，1→3→鸟群 |
| `6` | `reflection` | Reflection | 三分 placeholder + 问卷占位 + 告别 |

角色区分：场景中预置两只 Parrot（`FriendParrot` / `PlayerParrot`），头顶色球（鸟友 **橙** / 玩家 **浅蓝**）。鸟友在任务 1/2 中演示呼吸并带教学提示。镜子任务（1、2、6）使用 `AvianVoice/MirrorRT` Shader + RenderTexture。详见 [SCENES_AND_TASKS_CN.md](SCENES_AND_TASKS_CN.md)。

独立场景 `Task_PitchMatching` / `Task_FlightControl` / `Task_SongPerformance` 仍保留，加载后分别映射到任务 4 / 3 / 5。

---

### 4.2 Avian Lab（主实验室）

**场景路径**：`Assets/AvianVoice/Scenes/Avian_Lab.unity`  
**任务 ID**：`lab` · **显示名**：Avian Lab

#### 具体内容

- 第一人称 **占位鸟身**（或后续替换的 rigged 鸟模型）：胸、头、喙、双翅挂在相机前方；
- **麦克风实时分析**：音高 (Hz)、响度、发声置信度、延迟估算；
- **Transform / Assistive 两种语音模式**（默认 Transform，按 **`T`** 切换，见下节详解）；
- **呼吸模拟**：按住 `Space` 吸气、松开呼气；驱动胸腔缩放与翅膀微颤；
- **镜子模式**（可选，见下节）：前方虚拟镜中显示鸟身，强化身体所有权幻觉；
- **训练 HUD**：呼吸条、响度、音高、模式提示；
- **数据记录**：约 30 Hz 写入试验日志。

#### 设计目标

- 作为 **日常开发与系统联调** 的主场景；
- 在不引入任务评分的前提下，验证「麦 → 分析 → 鸟鸣/身体 → 呼吸」全链路是否稳定、可感知、延迟可接受；
- 为后续三个训练任务提供共用的运行时基础设施。

**不包含**：正式森林布景（需从 ForestVision 合并）、预置在场景内的 XR Rig（需手动添加一次）。

#### Transform 模式与 Assistive 模式（语音映射）

两种模式由 `BirdVoiceMapper`（`VoiceMapMode`）实现，研究上对应不同假设：**「嗓音被替换成鸟鸣」** vs **「保留人声但经听觉辅助处理」**。HUD 左侧会显示当前模式名（`Transform` / `Assistive`）。

| 对比项 | **Transform（变换）** | **Assistive（辅助）** |
|--------|----------------------|------------------------|
| **默认** | 是（进入 Play 即为该模式） | 按 `T` 或 HUD 开关切换 |
| **你听到什么** | **鸟鸣**（短 chirp WAV 或程序化鸟叫），**不是**自己的原声 | **自己的人声**（经监听通路），可做音高偏移与混响 |
| **输出通路** | `BirdTransformAudio` → `BirdCallLibrary.PlayNearest()` | `AssistiveMonitor` + 可选 **Audio Mixer** |
| **触发逻辑** | 检测到有效发声（`voiced`）后，按响度起音/持续发声规则 **间断播放** 鸟鸣；有冷却（常跟 clip 时长绑定），避免叠成噪声墙 | 持续根据实时音高/响度 **更新** 监听效果，不播放鸟叫库 |
| **音高如何参与** | 用麦克风估出的 `pitchHz` 选最近 `basePitchHz` 的 clip，并用 `AudioSource.pitch` 变速，使鸟叫音高跟随你的哼唱 | 以 `referencePitchHz`（默认 200 Hz）为参照，把检测音高映射为 **半音范围内的变调**（`assistivePitchRangeSemitones`，默认 ±12 半音）；响度可驱动混响发送量 `MicReverb` |
| **身体/喙部 FX** | 鸟鸣播放时仍有喙部粒子等（与发声分析共用） | 同样依赖发声分析，但 **不会** 触发 `BirdCallLibrary` |
| **实验用途** | 强化「我在用鸟的身体发声」的 **化身/替换** 体验 | 对照或辅助沟通条件：用户仍能识别自己的嗓音，仅获得听感帮助 |

**Transform 模式工作流程（简化）**

1. `MicrophoneCapture` 采集 → `VoiceAnalyzer` 得到音高、响度、`voiced`；
2. 满足起音或持续发声阈值，且冷却结束 → `BirdVoiceMapper` 调用库播放最近音高的 chirp；
3. 你感知到的是 **鸟在叫**，音高大致跟随你的哼唱/说话基频。

**Assistive 模式工作流程（简化）**

1. 同样在分析麦克风，但 **不再** 调用鸟叫库；
2. 取消静音 `AssistiveMonitor` 音源，按检测音高调整其 `pitch`；
3. 若已在 `AvianAppBootstrap` 上配置 **Audio Mixer**（`Assets/AvianVoice/Audio/Mixer/AvianVoice.mixer`，暴露 `MicPitch`、`MicReverb`），则 additionally 写 Mixer 参数实现更稳定的变调与混响；
4. **若未配置 Mixer**：仅 `AssistiveMonitor` 的 pitch 会变化，还需在工程中把 **麦克风输入路由到该监听链**（或 Mixer），否则可能听不到辅助效果——这是部署时要注意的一点。

**切换方式**：键盘 **`T`**（`KeyboardModeSwitcher`），或 HUD 上 **Transform / Assistive** 切换控件（`VoiceModeToggle`）。

---

#### 镜子模式（Mirror）

「镜子模式」不是第四种任务，而是 Avian Lab 里可选的 **视觉化身组件**，由 `MirrorBodySetup` 实现，用于 **身体所有权（body ownership）** 研究：除第一人称鸟身外，在前方提供一块 **第三人称镜中鸟身**。

| 项目 | 说明 |
|------|------|
| **开关** | `AvianVoiceSettings.mirrorEnabled`（默认 **true**）；为 `false` 时初始化后直接禁用镜子，不创建镜面 |
| **呈现形式** | 在 XR 锚点前方生成一块 **MirrorQuad**（约高 1.2 m、距前方 1.5 m），显示专用 **MirrorCamera** 渲染到 RenderTexture 的画面 |
| **相机拍什么** | 只渲染 `birdLayerMask` 指定层上的物体（设计为 **BirdMirror**，Layer 9）；鸟身模型需设到该层，镜中才清晰，否则会黑或空 |
| **相机跟随** | 每帧 `LateUpdate` 根据 `birdBodyRoot`（鸟身根节点）位置调整镜子相机，使镜中始终对准鸟胸/头区域 |
| **与语音模式关系** | **独立**。Transform / Assistive 只影响 **听觉**；镜子只影响 **是否看到镜中鸟身**，可与其他实验因子（如 `wingMappingEnabled`）组合做对照 |

**研究意图**：第一人称下用户主要看到自己的「鸟胸/翅膀」；镜子提供 **外视角的自我表征**，帮助被试把运动/呼吸/发声与「鸟的身体」绑定，常用于化身幻觉或对照（关镜 = 无额外第三人称反馈）。

**使用注意**

- 自动装配时 `MirrorBodySetup` 挂在 XR Origin 上；若镜中是黑的，检查鸟身是否在 **BirdMirror** 层，以及 `birdBodyRoot` 是否指向实际鸟模型根节点（占位鸟或 Crow 等）。
- 镜子与 **ForestVision / 任务场景** 无强绑定；合并环境后仍可在 Lab 中单独开关 `mirrorEnabled` 做 A/B。

---

### 4.3 音高匹配任务（Pitch Matching）

**独立场景**：`Assets/AvianVoice/Scenes/Task_PitchMatching.unity`  
**任务 ID**：`pitch` · **显示名**：Pitch Matching  
**同场景切换**：在 Avian_Lab 中按 `2`

#### 具体内容

- 约每 **3 秒** 播放一次 **参考鸟叫**（默认目标音高约 **440 Hz**，可在 Inspector 修改 `targetPitchHz`）；
- 用户哼唱/发声时，系统计算与目标音高的误差（容差约 **40 Hz**）；
- 匹配度映射为 **0–1 分数**，驱动鹦鹉/占位鸟 **胸部或羽毛 Emission 发光**（越接近越亮）；
- 分数上报 `TaskManager` 与 `SessionRecorder`。

#### 设计目标

- 训练用户 **听辨 + 模仿** 固定音高的鸟鸣；
- 用 **即时视觉正反馈**（发光）强化音高准确度，支撑阶段 3 的声乐/感知实验；
- 为论文提供可量化的 **音高匹配分数** 与时间序列日志。

---

### 4.4 飞行控制任务（Flight Control）

**独立场景**：`Assets/AvianVoice/Scenes/Task_FlightControl.unity`  
**任务 ID**：`flight` · **显示名**：Flight Control  
**同场景切换**：在 Avian_Lab 中按 `3`

#### 具体内容

- 将 **XR Origin（或 TaskRoot）** 作为飞行体：音高越高，垂直方向越高（`pitchHeightScale` 等参数可调）；
- **持续向前** 移动（`forwardSpeed`）；
- **呼吸深度** 接近中等（约按住 Space 使 breath ≈ 0.5）时 **滑翔更稳**；极端吸气/呼气降低稳定性；
- 场景中 **世界鹦鹉** 可进入伴飞展示（`ParrotFlightPresentation`）；
- 使用 `CharacterController` 与位姿快照，任务结束恢复原位。

#### 设计目标

- 体现 **「发声（音高）+ 呼吸」联合控制** 鸟类飞行隐喻，而非单一输入；
- 探索舒适、可学习的映射曲线（需 Quest 真机验证晕动症）；
- 为研究提供 **稳定性 / 飞行表现** 相关的行为与日志数据。

---

### 4.5 鸣叫序列任务（Song Performance）

**独立场景**：`Assets/AvianVoice/Scenes/Task_SongPerformance.unity`  
**任务 ID**：`song` · **显示名**：Song Performance  
**同场景切换**：在 Avian_Lab 中按 `4`

#### 具体内容（三阶段 + 终章合唱）

每音一步，循环 **听 → 应 → 和**：

| 阶段 | 行为 |
|------|------|
| **Listen** | 领唱鹦鹉播放目标音；HUD 显示 `● Listen: E4 (1/4)` 等 |
| **Respond** | 你的回合：需 **稳定保持** 目标音高约 **0.45 s**（非瞬间命中）；HUD 显示保持进度 `%`；领唱鹦鹉随匹配度发光 |
| **Joining** | 一只 **鸟群成员** 飞入（绕圈轨道 + 按音符着色），**回鸣**该音；短暂停顿后进入下一音 |
| **Finale** | 四音完成后，领唱与鸟群 **依次合唱** 整段旋律 |

- 唱错音：领唱播放偏低提示叫，HUD 提示目标音名与实际 Hz；
- 鸟群成员：`FlockMemberBehaviour`（轨道、颜色、空间回鸣）；
- 调试 **`C`**：跳过当前步，直接触发 Joining + 鸟群加入。

#### 设计目标

- 从「点对点就生成小球」升级为 **呼应式鸣唱**（领唱—模仿—族群和声）；
- **保持音高** 比单帧命中更接近真实鸣唱控制；
- 终章合唱强化 **群体归属** 与任务完成感。

---

## 五、任务模式与操作速查

### 5.1 在 Avian_Lab 内切换（推荐）

| 按键 | 模式 | 说明 |
|------|------|------|
| `1` | Meet Your Bird | 4/4 呼吸跟随鸟友，镜子开 |
| `2` | Breath Sync | Pattern A/B 呼吸同步 |
| `3` | Flying | 吸气上升、呼气下降；镜子关 |
| `4` | Singing Branch | 四音旋律 + 呼吸 |
| `5` | Call Friends | 呼唤鸟群递进 |
| `6` | Reflection | 分数与问卷 placeholder |
| `N` / `]` | 下一任务 | 在 6 个任务间循环 |
| **`C`** | **调试作弊** | 当前任务瞬间成功 / 推进（Pilot 用，正式实验前关闭） |

`TaskManager` 默认 **InScene** 模式：只启用/禁用对应任务脚本，**不** `LoadScene`。头显前有 **TaskModeHUD** 显示任务名、说明与分数。正式实验前可在 `PitchMatchingTask` / `SongPerformanceTask` 的 Inspector 关闭 **Enable Debug Cheat**。

若将 `switchMode` 设为 **LoadScene**，则 `AdvanceTask()` 会异步加载 `Task_*.unity` 独立场景（旧流程，仍保留在 Build Settings）。

### 5.2 通用输入

| 输入 | 作用 |
|------|------|
| `Space` 按住 / 松开 | 吸气 / 呼气 |
| `T` | Transform ↔ Assistive 语音模式 |
| 对着麦克风发声 | 分析、鸟鸣、喙部 FX、任务评分 |
| 桌面 `WASD`、`Q`/`E`、右键 + 鼠标 | 移动与环顾（无头显预览） |

---

## 六、运行时系统架构（简述）

```
Play（Avian_* / Task_* 场景）
  └── AvianLabRuntimeSetup
        └── XR Origin Hands + AvianAppBootstrap
              ├── MicrophoneCapture → VoiceAnalyzer
              ├── BirdVoiceMapper + BirdCallLibrary
              ├── BreathSimulator → BreathBodySync → BirdBodyController
              ├── WingHandMapper / BeakVocalFX / MirrorBodySetup
              └── SessionRecorder → avian_trials.jsonl
        └── TaskManager
              ├── lab      （无任务脚本）
              ├── pitch    → PitchMatchingTask
              ├── flight   → FlightControlTask
              └── song     → SongPerformanceTask
```

**语音模式**：

- **Transform**：嗓音触发鸟鸣（短 chirp WAV 或程序化），强调「变成鸟在叫」；
- **Assistive**：保留人声听感并处理（Mixer），强调辅助沟通/听感。

---

## 七、实验数据与对照条件

**日志文件**：`avian_trials.jsonl`（JSON Lines，追加写入）

**典型字段**：`timestamp`, `taskId`, `voiceMode`, `pitchHz`, `loudness`, `breathDepth`, `score`, `latencyMs`

**对照开关示例**（`AvianVoiceSettings` / `PlatformProfile`）：

- 镜子开/关（`mirrorEnabled`）
- 翅膀与手追踪映射开/关（`wingMappingEnabled`）
- 平台与 DSP 缓冲配置（桌面 vs Quest）

---

## 八、建议研发节奏（参考）

| 周次 | 重点 | 目标 |
|------|------|------|
| 第 1 周 | Avian_Lab | 麦延迟、音高稳定、两种语音模式可区分 |
| 第 2 周 | Avian_Lab | 真鸟模型、镜子、Mixer；翅膀与手追踪对齐 |
| 第 3 周 | Pitch Matching + 森林环境 | 参考刺激、评分与发光反馈 |
| 第 4 周 | Flight Control | 舒适飞行映射、呼吸稳定性 |
| 第 5 周 | Song Performance | 旋律设计、鸟群视觉、被试流程 |
| 第 6 周 | 全部场景 | Quest 真机、日志导出、对照条件冻结 |

---

## 九、首次使用 checklist

1. Unity 菜单：**Avian Voice → Setup Project**
2. 打开 `Avian_Lab.unity`，拖入 Prefab：  
   `Assets/Samples/XR Interaction Toolkit/3.4.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab`  
   （勿用损坏嵌套引用的 `Complete XR Origin Set Up Variant`）
3. 保存场景，Play，允许麦克风权限
4. 按住 `Space` 试呼吸条；发声看 HUD；按 `T` 切换语音模式；按 `1`–`4` 试任务

---

## 十、相关文档索引

| 文档 | 路径 | 用途 |
|------|------|------|
| 本文 | `Assets/AvianVoice/PROJECT_OVERVIEW_CN.md` | 项目与场景/任务目标总览（中文） |
| **场景与任务详述** | `Assets/AvianVoice/SCENES_AND_TASKS_CN.md` | **每场景内容、六任务、交互、设置（中文）** |
| 场景操作指南 | `Assets/AvianVoice/SCENES_GUIDE.md` | 详细步骤、排错、调参、HUD 字段 |
| 英文 README | `Assets/AvianVoice/README.md` | 快速开始与平台说明 |
| 实验参数资源 | `Assets/AvianVoice/Settings/AvianVoiceSettings.asset` | 语音/鸟鸣/分析阈值 |
| 平台配置 | `Assets/AvianVoice/Settings/PlatformProfile.asset` | 桌面 vs Quest |

---

*文档根据当前仓库 `Assets/AvianVoice` 代码与场景指南整理；若新增场景，请保持 `Avian_` 或 `Task_` 命名前缀，或更新 `AvianVoiceSceneAutoLoad` 中的场景名判断。*
