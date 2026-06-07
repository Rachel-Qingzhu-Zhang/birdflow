# Avian Voice

VR bird body illusion vocal training prototype for CHI 2027 research.

**中文项目总览（场景与任务目标）** → [PROJECT_OVERVIEW_CN.md](PROJECT_OVERVIEW_CN.md)

**中文场景说明与后续操作（推荐阅读）** → [SCENES_GUIDE.md](SCENES_GUIDE.md)

## Quick start

1. Open the project in **Unity 6000.3.4f1** (or compatible Unity 6).
2. Menu: **Avian Voice → Setup Project** (creates lab scene, settings, build entries, layers).
3. Menu: **Avian Voice → Create Task Scenes** (optional task scenes).
4. Open `Assets/AvianVoice/Scenes/Avian_Lab.unity` and press Play.
5. Allow microphone access. Hold **Space** to inhale. Press **T** to toggle Assistive / Transform voice mode.

## Desktop vs Quest

- **Desktop**: XR Simulation or keyboard; **WASD + right-mouse look** via `DesktopFlyCamera`; `PlatformProfile` uses smaller DSP buffer (256).
- **Quest**: Build Android with Meta OpenXR; external mic via USB/OTG; DSP buffer 512.

Assign `Assets/AvianVoice/Settings/PlatformProfile.asset` on `AvianLabRuntimeSetup` or `AvianAppBootstrap`.

**XR Rig:** Use `XR Origin Hands (XR Rig).prefab` from XRI Hands Demo — not `Complete XR Origin Set Up Variant` (broken nested prefabs in this project).

## Logs

Trial samples append to `Application.persistentDataPath/avian_trials.jsonl`.

## Replace placeholder bird

Import a rigged bird model and assign bones to `AvianAppBootstrap` (wings, chest, beak). Disable `AvianLabRuntimeSetup.autoSetup` when using a custom prefab.
