# Avian Voice

You become a parrot in a forest. The Unity scene to press Play on is **Avian Lab**.

**Team handbook (中文 / English / 日本語)** → [TEAM_GUIDE.md](TEAM_GUIDE.md) · open [TEAM_GUIDE.html](TEAM_GUIDE.html) in a browser and use the language buttons. That handbook is the current description of the visit, desktop controls, and headset controls.

## Quick start

1. Open the project in **Unity 6000.3.4f1** (or compatible Unity 6).
2. Open `Assets/AvianVoice/Scenes/Avian_Lab.unity`. The scene name is **Avian Lab**.
3. Press Play and allow the microphone.
4. At a desk: hold the right mouse button to look, WASD to walk, and open **I want …** (or press M) to move between the six moments. In a headset: look with your head and use the controller menu button for the same list.
5. Speak or hum. Your voice is heard as birdsong. During flight, louder or higher singing climbs; silence glides down.

## Desktop vs Quest

- **Desktop**: XR Simulation or keyboard; **WASD + right-mouse look** via `DesktopFlyCamera`; `PlatformProfile` uses smaller DSP buffer (256).
- **Quest**: Build Android with Meta OpenXR; external mic via USB/OTG; DSP buffer 512.

Assign `Assets/AvianVoice/Settings/PlatformProfile.asset` on `AvianLabRuntimeSetup` or `AvianAppBootstrap`.

**XR Rig:** Use `XR Origin Hands (XR Rig).prefab` from XRI Hands Demo — not `Complete XR Origin Set Up Variant` (broken nested prefabs in this project).

## Logs

Trial samples append to `Application.persistentDataPath/avian_trials.jsonl`.

## Replace placeholder bird

Import a rigged bird model and assign bones to `AvianAppBootstrap` (wings, chest, beak). Disable `AvianLabRuntimeSetup.autoSetup` when using a custom prefab.
