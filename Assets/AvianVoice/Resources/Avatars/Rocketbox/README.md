# Microsoft Rocketbox avatar (Male_Adult_01)

Source: https://github.com/microsoft/Microsoft-Rocketbox (`Assets/Avatars/Adults/Male_Adult_01`)
License: MIT (see `LICENSE.md` next to this file). The library was published by Microsoft for
research and academic use and is now MIT-licensed, so it can be used freely in this project.

Files:

- `Male_Adult_01/Male_Adult_01.fbx` — rigged body (3ds Max Biped bones, imported as Unity Humanoid by
  `Assets/AvianVoice/Editor/RocketboxAvatarImport.cs`).
- `Male_Adult_01/Textures/*.tga` — body / head / hair (opacity) colour, normal and specular maps.

This folder lives under `Resources` so the embodiment scene can load the avatar by path
(`Avatars/Rocketbox/Male_Adult_01/Male_Adult_01`). To use another Rocketbox avatar, copy its `Export`
FBX and `Textures` folder next to this one and change the path on the `AvatarEmbodimentController`.
