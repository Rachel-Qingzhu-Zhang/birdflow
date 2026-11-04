# Microsoft Rocketbox avatars (Male_Adult_01, Female_Adult_01)

Source: https://github.com/microsoft/Microsoft-Rocketbox (`Assets/Avatars/Adults/<name>`)
License: MIT (see `LICENSE.md` next to this file). The library was published by Microsoft for
research and academic use and is now MIT-licensed, so it can be used freely in this project.
Reference: Gonzalez-Franco et al., "The Rocketbox Library and the Utility of Freely Available Rigged
Avatars", Frontiers in Virtual Reality, 2020 (doi:10.3389/frvir.2020.561558). Rocketbox bodies are the
standard stimuli in virtual-embodiment work (Maselli & Slater 2013 and many CHI / IEEE VR / UIST studies).

Files per avatar:

- `<name>/<name>.fbx` — rigged body (3ds Max Biped bones, imported as Unity Humanoid by
  `Assets/AvianVoice/Editor/RocketboxAvatarImport.cs`).
- `<name>/Textures/*.tga` — body / head / hair (opacity) colour, normal and specular maps.

| Folder | Id in `AvatarEmbodimentController` | Notes |
|--------|------------------------------------|-------|
| `Male_Adult_01` | `male` | textures `m002_*` |
| `Female_Adult_01` | `female` | textures `f001_*` (the `_facial.fbx` and wrinkle maps were not copied) |

The gender-neutral body (`neutral`, `Resources/Avatars/MakeHuman/Neutral_Adult_01`) is not a Rocketbox
model; see `Assets/AvianVoice/EMBODIMENT_SCENE.md` for how it is generated with MakeHuman.

This folder lives under `Resources` so the embodiment scene can load the avatars by path. To add another
Rocketbox avatar, copy its `Export` FBX and `Textures` folder next to these and add an entry to
`Human Avatars` on the `AvatarEmbodimentController`.
