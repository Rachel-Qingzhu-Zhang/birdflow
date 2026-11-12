# Unity "Armature" mannequin (gender-neutral human avatar)

The white, faceless robot-style humanoid from Unity's free **Starter Assets** package, in the copy that Meta
redistributes with its body-tracking samples:

- Source: https://github.com/oculus-samples/Unity-Movement/tree/main/Shared/ThirdParty/Unity
  (commit 77274f138974087ba2909bab2723fc6ff851338b, 2026-09-28). Meta uses this exact model as the
  "Stylized" character of the Movement SDK body-tracking samples, so Meta's Humanoid retargeting is known to
  keep its body and hands intact.
- Licence: **Unity Companion License** (`LICENSE.md` next to this file, as shipped by Meta). Keep the licence
  file with the model.
- Files: `Armature.fbx` (Humanoid rig, all finger bones mapped in `Armature.fbx.meta`), the three
  `Armature_*_AlbedoTransparency.png` textures. The `.meta` was taken from Meta's repository (bone mapping)
  with the foreign material remaps removed and Read/Write enabled; `Assets/AvianVoice/Editor/UnityArmatureAvatarImport.cs`
  applies the rest (Humanoid, readable mesh, matte URP materials with these textures).

| Id in `AvatarEmbodimentController` | Resources path | Notes |
|------------------------------------|----------------|-------|
| `mannequin` | `Avatars/UnityArmature/Armature` | kept but not offered since 2026-11-12 (`selectable` off; the study offers neutral MakeHuman / male / female Rocketbox). It was the default human body as `neutral` from 2026-11-05 to 2026-11-12. |

Why this model: the study needs a human body that reads as neither male nor female. No validated androgynous
human avatar is freely redistributable (Mixamo bots, SMPL-X, Ready Player Me forbid redistribution; the VALID
library has no neutral body; MakeHuman androgynous exports still read as one gender), so a faceless mannequin /
robot avatar, the line of Lugrin et al. 2015 (IEEE VR) and Gorisse et al. 2019, is used instead.
