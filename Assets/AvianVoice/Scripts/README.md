# Avian Voice scripts

Start here when you need to change behavior. Namespaces did not change (`AvianVoice.Audio`, `AvianVoice.Embodiment`, …), only folder grouping.

Live lab scene: `Assets/AvianVoice/Scenes/Avian_Lab.unity`.

## Where to edit

| If you are changing… | Open |
|---|---|
| Mic, pitch, bird song | `Audio/` |
| App wiring, settings, input | `Core/` |
| One of the six lab tasks | `Tasks/` |
| Wish menu, HUD, onboarding text | `UI/` |
| Teacher breath timeline | `Physiology/` + `Embodiment/Parrot/BirdFriendDemonstrator.cs` |
| Player / friend parrot body, wings, voice motion | `Embodiment/Parrot/` |
| Mirror / baroque glass | `Embodiment/Mirror/` |
| Pond, fish, water reflection | `Embodiment/Pond/` |
| Call Friends flock | `Embodiment/Flock/` |
| Scene roles, lighting, XR start poses | `Embodiment/Scene/` |
| Legacy pigeon FPV (not the lab parrot) | `Embodiment/Pigeon/` |
| Trial logs | `Data/` |
| Editor menus | `../Editor/` |

## Runtime flow (lab)

1. `Core/AvianAppBootstrap` — mic, analyzer, synth, breath, body sync.
2. `Core/AvianInSceneTaskBootstrap` — six tasks on `TaskRoot`, keys 1–6.
3. `Embodiment/Scene/BirdSceneRoles` — `PlayerParrot` / `FriendParrot`.
4. `Embodiment/Parrot/ParrotBreathEmbodiment` — player chest, wings, mic bob.
5. `Embodiment/Parrot/FriendBreathVisual` — friend/teacher pose and singing float.
6. `Tasks/*Task` — meet, breath, fly, sing, call friends, reflection.

## Rules for new code

- Put a new script in the folder of its feature, not in `Embodiment/` root.
- Do not mix pond, mirror, and parrot logic in one file.
- Keep the `.meta` file next to the script so Unity scene references stay valid.
