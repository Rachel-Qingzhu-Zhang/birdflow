# Birdflow / Avian Voice — 项目内容与 User Flow

> 依据当前代码与场景配置整理（`Avian_Lab` InScene 六任务模式）。  
> 主入口：`Assets/AvianVoice/Scenes/Avian_Lab.unity`  
> 详细任务参数见：[SCENES_AND_TASKS_CN.md](SCENES_AND_TASKS_CN.md)

---

## 一、项目是什么

**Birdflow（Avian Voice）** 是面向 **CHI 2027** 的 VR 研究原型，在 Unity 6 + URP + OpenXR 上探索：

| 主题 | 内容 |
|------|------|
| **鸟身幻觉** | 第一人称鹦鹉化身 + 镜中/水面倒影的第三人称自我 |
| **嗓音 → 鸟鸣** | 麦克风实时音高/响度 → Transform（鸟鸣）或 Assistive（人声辅助） |
| **呼吸驱动身体** | Space 吸/呼 → 胸廓、翅膀、飞行高度 |
| **结构化任务** | 同一实验室内 6 段体验，评估呼吸同步、发声与化身感 |

**推荐用法**：只打开 `Avian_Lab`，Play 后用数字键 **`1`–`6`** 切换任务（`TaskManager` = **InScene**，不反复加载场景）。

### 核心角色与环境

| 元素 | 说明 |
|------|------|
| **FriendParrot**（橙球） | 鸟友：演示呼吸、唱参考音、教学 chirp |
| **PlayerParrot**（浅蓝球） | 玩家鸟身：Space 呼吸、麦克风发声驱动 |
| **镜子** | 任务 1 / 2 / 6 开启，显示玩家鸟第三人称影像 |
| **池塘水面反射** | 任务 3 / 4 / 5 开启，飞行时可低头看倒影 |
| **森林 + 栖木 + AvianPond** | ForestVision / Fine3D 池塘布景 |

### 全局输入（全程共用）

| 输入 | 作用 |
|------|------|
| **Space 按住 / 松开** | 吸气 / 呼气 |
| **麦克风发声** | 音高匹配、鸟鸣触发 |
| **`T`** | Transform ↔ Assistive 语音模式 |
| **`1`–`6`** | 切任务 |
| **`N` / `]`** | 下一任务 |
| **`C`** | 调试作弊完成（正式实验请关闭） |
| **WASD + 右键** | 桌面预览移动环顾 |

---

## 二、场景总览

| 场景 | 角色 | User Flow 概要 |
|------|------|----------------|
| **Avian_Lab** | **主实验场景** | 进入 → 六任务循环体验（见第三节） |
| Task_PitchMatching | 旧独立场景（备用） | 听参考音 → 跟音高（≈ Lab 任务 4） |
| Task_FlightControl | 旧独立场景（备用） | 音高/呼吸控飞（旧逻辑；Lab 任务 3 已改为呼吸控高） |
| Task_SongPerformance | 旧独立场景（备用） | 旋律序列 + 鸟群（≈ Lab 任务 5） |

日常开发与被试实验：**以 Avian_Lab 为准**。

---

## 三、Avian_Lab — 完整 User Flow

```
打开 Avian_Lab → Play → 允许麦克风
        │
        ▼
   默认任务 1（Meet）
        │
   ┌────┼────┬────┬────┬────┐
   │    │    │    │    │    │
   1    2    3    4    5    6
 Meet Breath Fly Sing Call Reflect
   │    │    │    │    │    │
   └────┴────┴────┴────┴────┘
        │
        ▼（可随时 N 循环下一任务）
   全程 SessionRecorder 写 avian_trials.jsonl
```

### 视觉反馈按任务切换

| 任务 | 镜子 | 池塘反射 | 主要空间 |
|------|------|----------|----------|
| 1 Meet | ON | OFF | 栖木 + 镜前 |
| 2 Breath Sync | ON | OFF | 栖木 + 镜前 |
| 3 Flying | OFF | ON | 池塘上空飞行 |
| 4 Singing | OFF | ON | 池塘区 |
| 5 Call Friends | OFF | ON | 前方鸟群扇区 |
| 6 Reflection | ON | OFF | 栖木 + 镜前 |

---

### 任务 1 — Meet Your Bird（按键 `1`）

**目标**：跟鸟友同步呼吸，在镜子里「遇见」鸟自我。

```
进入任务
  → 鸟友开始 4s 吸 / 4s 呼演示（胸口橙球 + chirp）
  → 玩家看镜中并排两只鸟
  → 按住 Space 吸气 / 松开呼气，对齐相位与深度
  → 同步度 ≥ 80% 且连续保持 10 秒
  → 成功：连串 chirp + 鸟友发光 + 「You met your bird self」
  →（可选）按 N 进入任务 2
```

**成功条件**：`sync ≥ 0.8` × `hold 10s`

---

### 任务 2 — Breath Sync（按键 `2`）

**目标**：在两种呼吸节律下保持与鸟友同步。

```
进入任务
  → Pattern A：鸟友 4/4，玩家用 Space 跟随
  → 同步 ≥ 80% 连续 8 秒 → 进入 Pattern B
  → Pattern B：鸟友 6/6，同上再保持 8 秒
  → 同步跌破 80% → 当前 Pattern 重新开始
  → 两阶段完成 → 成功反馈 →（可选）N → 任务 3
```

**成功条件**：Pattern A 与 B 各 `sync ≥ 0.8` × `hold 8s`

---

### 任务 3 — Flying（按键 `3`）

**目标**：用呼吸能量「飞」——深吸上升、呼气滑翔下降（**不用音高控高**）。

```
进入任务
  → 镜子关；池塘水面反射开；XR Rig 移到飞行起点
  → 持续向前飞行（约 1.75 m/s）
  → Space 浅吸 → 缓升；深吸 → 快升；松开 → 下降滑翔
  → 呼吸稳定则分数上升，伴飞姿态更自然
  → 低头可见水面中玩家鸟倒影
  →（可选）N → 任务 4
```

**桌面**：WASD 移视角；Space 仍控呼吸。

---

### 任务 4 — Singing Branch（按键 `4`）

**目标**：模仿鸟友四音旋律，同时保持呼吸。

```
进入任务
  → 鸟友唱第 1 音（默认 E4 330 Hz）
  → 玩家对着麦跟音（容差约 ±40 Hz）+ 保持 Space 呼吸
  → 稳定匹配约 0.45 s → 下一音
  → 序列：E4 → G4 → A4 → B4
  → 四音完成 → 成功 chirp 串 →（可选）N → 任务 5
```

**评分要点**：音高命中 + 呼吸深度（深吸 ≥0.7 加权更高）

---

### 任务 5 — Call Friends（按键 `5`）

**目标**：用旋律 + 呼吸「呼唤」前方伙伴飞来环绕合唱。

```
进入任务
  → 前方扇形生成 7 只 idle 悬空小鸟（仅本任务存在）
  → 鸟友演示 4/4 呼吸
  → 玩家依次唱 E4、G4（+ 呼吸同步 ≥55%）
  → 评分 ≈ 音高 60% + 呼吸 40%；稳定约 0.45 s
  → 第 1 音成功 → 1 只飞来环绕
  → 第 2 音成功 → 再 2 只飞来，随后剩余全部加入
  → 鸟群到齐 → 短合唱 + SuccessChirpBurst
  → 离开任务时鸟群自动销毁 →（可选）N → 任务 6
```

---

### 任务 6 — Reflection（按键 `6`）

**目标**：回顾体验分数，做告别（问卷为占位）。

```
进入任务
  → 镜子开；显示 Breathing / Voice / Synchronization 缓存分
  → 继续可跟鸟友做轻量呼吸同步（可视化反馈）
  → 约 0.5 s 后播放告别 chirp
  → HUD：How did it feel to become a bird?（占位）
  → 按 N 循环回任务 1，或结束会话
```

---

## 四、推荐被试完整流程（约一局）

1. 打开 **Avian_Lab** → Play → 允许麦克风  
2. **`1` Meet**：跟橙鸟呼吸 10 秒，看镜子  
3. **`2` Breath Sync**：完成 4/4 与 6/6  
4. **`3` Flying**：深吸上升、呼气滑翔，看水面倒影  
5. **`4` Singing**：跟唱四音 + 呼吸  
6. **`5` Call Friends**：呼唤鸟群环绕  
7. **`6` Reflection**：看三分 + 告别  

全程可按 **`T`** 切换语音模式。桌面用 **Space + 麦克风** 即可走通；Quest 上手部追踪增强翅膀映射。

---

## 五、独立 Task 场景 User Flow（备用）

仅在 `TaskManager.switchMode = LoadScene` 或单独打开场景时使用。环境多为简易模板，**无** Lab 的森林/双鹦鹉布置。

### Task_PitchMatching

```
进入 → 听参考鸟叫 → 用相近音高哼唱 → 匹配分数驱动羽毛发光 → 循环下一参考音
```

对应 Lab 内任务 **`4` Singing** 的旧独立版。

### Task_FlightControl

```
进入 → 变化音高控高度 + Space 调稳定性 → 向前飞行 → 观察高度变化
```

注意：Lab 内任务 **`3` Flying** 已改为 **呼吸控高**，与此旧场景逻辑不同。

### Task_SongPerformance

```
进入 → 按旋律序列跟唱 → 每命中一音得分 + 生成鸟群占位 → 完成序列
```

对应 Lab 内任务 **`5` Call Friends** 的旧独立版。

---

## 六、系统架构（便于对照代码）

```
Avian_Lab.unity
├── FriendParrot / PlayerParrot     鸟友 / 玩家
├── Cylinder                       栖木
├── AvianPond + PondWaterReflection 池塘倒影
├── XR Origin Hands
│   ├── AvianAppBootstrap          麦 / 分析 / 鸟鸣 / HUD
│   └── FlightControlTask          任务 3 挂在 Rig 上
├── TaskRoot
│   ├── BirdFriendDemonstrator
│   ├── MeetBirdTask               1
│   ├── BreathSyncTask             2
│   ├── SingingBranchTask          4
│   ├── CallFriendsTask            5
│   └── ReflectionTask             6
└── TaskManager + TaskModeKeyboardSwitcher
```

**日志**：`Application.persistentDataPath/avian_trials.jsonl`（约 30 Hz）

**设置**：`Assets/AvianVoice/Settings/AvianVoiceSettings.asset`、`PlatformProfile.asset`

---

## 七、一句话总结

本项目是一个 **VR 鸟身 + 嗓音/呼吸训练实验室**：在同一森林实验室场景里，被试依次经历「遇见鸟自我 → 呼吸同步 → 呼吸飞行 → 跟唱 → 呼唤鸟群 → 回顾」，用麦克风与 Space 完成从化身建立到社交合唱的完整体验链路。

---

*文档与当前 InScene 六任务实现一致。若增删任务或改成功条件，请同步更新本文与 `SCENES_AND_TASKS_CN.md`。*
