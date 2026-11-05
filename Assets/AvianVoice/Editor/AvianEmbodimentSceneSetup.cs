#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using AvianVoice.Embodiment.Avatars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Editor helpers for the Avian_Lab_Embodiment scene (body-tracked avatar + avatar switching).
    /// </summary>
    public static class AvianEmbodimentSceneSetup
    {
        public const string ScenePath = "Assets/AvianVoice/Scenes/Avian_Lab_Embodiment.unity";
        public const string HumanAvatarAssetPath =
            "Assets/AvianVoice/Resources/Avatars/UnityArmature/Armature.fbx";

        /// <summary>All selectable human bodies (see AvatarEmbodimentController.humanAvatars).</summary>
        public static readonly string[] HumanAvatarAssetPaths =
        {
            HumanAvatarAssetPath,
        };

        const string ImportCheckSessionKey = "AvianVoice.Embodiment.AvatarImportChecked";

        /// <summary>Once per editor session: make sure the human avatar was imported as Humanoid.</summary>
        [InitializeOnLoadMethod]
        static void CheckAvatarImportOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(ImportCheckSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                SessionState.SetBool(ImportCheckSessionKey, true);
                var importer = AssetImporter.GetAtPath(HumanAvatarAssetPath) as ModelImporter;
                if (importer == null)
                    return;

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    Debug.Log("[AvianVoice] Human avatar is not Humanoid yet; reimporting.");
                    AssetDatabase.ImportAsset(HumanAvatarAssetPath, ImportAssetOptions.ForceUpdate);
                }

                Debug.Log("[AvianVoice] Human avatar import: " + DescribeAvatar());
            };
        }

        [MenuItem("Avian Voice/Embodiment/Condition/Bird + bird voice (Play Mode)")]
        public static void ConditionBirdVoice() => SetCondition(AvatarKind.Bird, true);

        [MenuItem("Avian Voice/Embodiment/Condition/Bird + own voice only (Play Mode)")]
        public static void ConditionBirdOwn() => SetCondition(AvatarKind.Bird, false);

        [MenuItem("Avian Voice/Embodiment/Condition/Human + human voice (Play Mode)")]
        public static void ConditionHumanVoice() => SetCondition(AvatarKind.Human, true);

        [MenuItem("Avian Voice/Embodiment/Condition/Human + own voice only (Play Mode)")]
        public static void ConditionHumanOwn() => SetCondition(AvatarKind.Human, false);

        /// <summary>Same as the Condition buttons of the experimenter panel (avatar x voice feedback).</summary>
        static void SetCondition(AvatarKind kind, bool convertedVoice)
        {
            var flow = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Study.StudyFlowController>();
            if (flow == null)
            {
                Debug.LogWarning("[AvianVoice] No StudyFlowController (enter Play mode in Avian_Lab_Embodiment first).");
                return;
            }

            flow.SetCondition(kind, convertedVoice);
            Debug.Log("[AvianVoice] Condition set from the menu: " + flow.ConditionLabel);
        }

        [MenuItem("Avian Voice/Embodiment/Log Avatar State (Play Mode)")]
        public static void LogAvatarState()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>();
            var sb = new System.Text.StringBuilder("[AvianVoice] Avatar state: ");
            sb.Append(controller != null ? controller.DescribeState() : "no AvatarEmbodimentController (is Avian_Lab_Embodiment playing?)");
            sb.Append(" | ").Append(DescribeBird("player", AvianVoice.Embodiment.BirdSceneRoles.Player));
            sb.Append(" | ").Append(DescribeBird("friend", AvianVoice.Embodiment.BirdSceneRoles.Friend));
            var cam = Camera.main;
            if (cam != null)
                sb.Append(" | mainCamera=").Append(cam.name).Append(" pos=").Append(cam.transform.position.ToString("F2")).Append(" mask=0x").Append(cam.cullingMask.ToString("X"));
            var monitor = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Study.PcAudioMonitor>();
            if (monitor != null)
                sb.Append(" | ").Append(monitor.Describe());
            Debug.Log(sb.ToString());
        }

        static string DescribeBird(string label, Transform bird)
        {
            if (bird == null)
                return label + "=none";
            var renderers = bird.GetComponentsInChildren<Renderer>(true);
            var on = 0;
            foreach (var r in renderers)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                    on++;
            return label + "=" + bird.name + " active=" + bird.gameObject.activeInHierarchy + " renderersOn=" + on + "/" + renderers.Length
                   + " pos=" + bird.position.ToString("F2") + " layer=" + bird.gameObject.layer;
        }

        [MenuItem("Avian Voice/Embodiment/Debug/PC Audio Monitor - Always (Play Mode)")]
        public static void PcAudioMonitorAlways() => SetPcAudioMonitorMode(AvianVoice.Study.PcAudioMonitor.MonitorMode.Always);

        [MenuItem("Avian Voice/Embodiment/Debug/PC Audio Monitor - Auto (Play Mode)")]
        public static void PcAudioMonitorAuto() => SetPcAudioMonitorMode(AvianVoice.Study.PcAudioMonitor.MonitorMode.Auto);

        static void SetPcAudioMonitorMode(AvianVoice.Study.PcAudioMonitor.MonitorMode mode)
        {
            var monitor = AvianVoice.Study.PcAudioMonitor.Instance ?? AvianVoice.Study.PcAudioMonitor.Ensure();
            if (monitor == null)
            {
                Debug.LogWarning("[AvianVoice] No AudioListener / PcAudioMonitor (is the scene playing?).");
                return;
            }

            monitor.Mode = mode;
            Debug.Log("[AvianVoice] " + monitor.Describe());
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Capture Game View (Play Mode)")]
        public static void CaptureGameView()
        {
            var dir = System.IO.Path.Combine(Application.persistentDataPath, "AvianStudy", "screenshots");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "game_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AvianVoice] Game view screenshot: " + path);
        }

        [MenuItem("Avian Voice/Embodiment/Toggle Avatar (Play Mode)")]
        public static void ToggleAvatar()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>();
            if (controller != null)
                controller.Toggle();
            else
                Debug.LogWarning("[AvianVoice] No AvatarEmbodimentController in the running scene.");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Next Human Avatar (Play Mode)")]
        public static void NextHumanAvatar()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>();
            if (controller == null || controller.HumanAvatars == null || controller.HumanAvatars.Length == 0)
            {
                Debug.LogWarning("[AvianVoice] No AvatarEmbodimentController / human avatars in the running scene.");
                return;
            }

            var options = controller.HumanAvatars;
            var index = 0;
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].id == controller.HumanAvatarId)
                    index = i;
            }

            for (var step = 1; step <= options.Length; step++)
            {
                var candidate = options[(index + step) % options.Length];
                if (controller.SetHumanAvatar(candidate.id))
                {
                    controller.SetAvatar(AvatarKind.Human);
                    Debug.Log("[AvianVoice] Human avatar now: " + controller.DescribeState());
                    return;
                }
            }

            Debug.LogWarning("[AvianVoice] No other human avatar model is available.");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Measure Human Avatar Eye Point")]
        public static void MeasureHumanAvatarEyePoint()
        {
            var prefab = Resources.Load<GameObject>("Avatars/UnityArmature/Armature");
            if (prefab == null)
            {
                Debug.LogWarning("[AvianVoice] Human avatar model not found in Resources/Avatars/UnityArmature/Armature.");
                return;
            }

            var go = UnityEngine.Object.Instantiate(prefab);
            try
            {
                go.hideFlags = HideFlags.HideAndDontSave;
                var root = go.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.localScale = Vector3.one;
                var animator = go.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogWarning("[AvianVoice] Human avatar is not Humanoid.");
                    return;
                }

                // Muscle-space neutral pose = the T-pose HumanFirstPersonDriver measures the eye offset in.
                using (var handler = new HumanPoseHandler(animator.avatar, root))
                {
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    for (var i = 0; i < pose.muscles.Length; i++)
                        pose.muscles[i] = 0f;
                    handler.SetHumanPose(ref pose);
                }

                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var eye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                var sb = new System.Text.StringBuilder("[AvianVoice] Human avatar eye point (root frame, muscle T-pose): ");
                sb.Append("headBone=").Append(head != null ? head.position.ToString("F3") : "none");
                sb.Append(" leftEyeBone=").Append(eye != null ? eye.position.ToString("F3") : "none");
                foreach (var skinned in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    var weights = skinned.sharedMesh.boneWeights;
                    var bones = skinned.bones;
                    var isHead = new bool[bones.Length];
                    for (var i = 0; i < bones.Length; i++)
                        isHead[i] = head != null && bones[i] != null && (bones[i] == head || bones[i].IsChildOf(head));
                    Vector3 min = Vector3.one * 1e9f, max = -min, allMin = min, allMax = max;
                    var count = 0;
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var p = skinned.transform.TransformPoint(vertices[i]);
                        allMin = Vector3.Min(allMin, p);
                        allMax = Vector3.Max(allMax, p);
                        var w = weights[i];
                        var headWeight = 0f;
                        if (isHead[w.boneIndex0]) headWeight += w.weight0;
                        if (isHead[w.boneIndex1]) headWeight += w.weight1;
                        if (isHead[w.boneIndex2]) headWeight += w.weight2;
                        if (isHead[w.boneIndex3]) headWeight += w.weight3;
                        if (headWeight < 0.5f)
                            continue;
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                        count++;
                    }

                    sb.Append(" | ").Append(skinned.name).Append(": body ").Append(allMin.ToString("F3")).Append("..").Append(allMax.ToString("F3"))
                      .Append(" head(").Append(count).Append(" verts) ").Append(min.ToString("F3")).Append("..").Append(max.ToString("F3"));
                    if (head != null && count > 0)
                        sb.Append(" headTop-headBone=").Append((max.y - head.position.y).ToString("F3"))
                          .Append(" headFront-headBone=").Append((max.z - head.position.z).ToString("F3"));
                    UnityEngine.Object.DestroyImmediate(baked);
                }

                Debug.Log(sb.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Load Task 3 Singing (Play Mode)")]
        public static void DebugLoadSinging()
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Tasks.TaskManager>();
            if (manager != null)
                manager.LoadTaskById("singing");
            else
                Debug.LogWarning("[AvianVoice] No TaskManager in the running scene.");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Replay Guide Note (Play Mode)")]
        public static void DebugReplayGuideNote()
        {
            ReplayGuideNote();
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Play Human Voice Test Note (Play Mode)")]
        public static void DebugPlayHumanVoiceNote()
        {
            var mapper = AvianVoice.Audio.BirdVoiceMapper.Find();
            if (mapper == null)
            {
                Debug.LogWarning("[AvianVoice] No BirdVoiceMapper (enter Play mode first).");
                return;
            }

            mapper.PlayGuideNote(330f, 1.5f, AvianVoice.Core.VoiceFeedback.Human);
            Debug.Log("[AvianVoice] Human voice test note: E4 (330 Hz) for 1.5 s through HumanVoiceSynth.");
        }

        /// <summary>Same as the participant pressing Space / R: the teacher bird sings the current note again.</summary>
        public static bool ReplayGuideNote()
        {
            var trials = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Study.SingingTrialTask>();
            if (trials != null && trials.isActiveAndEnabled)
                return trials.StartNextTrial("menu");

            var singing = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Tasks.SingingBranchTask>();
            if (singing == null || !singing.isActiveAndEnabled)
            {
                Debug.LogWarning("[AvianVoice] Singing task is not active; switch to scene 3 first.");
                return false;
            }

            singing.ReplayMelody();
            return true;
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Log Audio State (Play Mode)")]
        public static void LogAudioState()
        {
            var sb = new System.Text.StringBuilder();
            var config = AudioSettings.GetConfiguration();
            sb.Append("[AvianVoice] Audio: listenerVolume=").Append(AudioListener.volume)
              .Append(" editorMute=").Append(EditorUtility.audioMasterMute)
              .Append(" paused=").Append(AudioListener.pause)
              .Append(" listeners=").Append(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length)
              .Append(" sampleRate=").Append(config.sampleRate).Append(" dsp=").Append(config.dspBufferSize)
              .Append(" speakerMode=").Append(config.speakerMode)
              .Append(" driverCaps=").Append(AudioSettings.driverCapabilities)
              .Append(" spatializer=").Append(AudioSettings.GetSpatializerPluginName());
            var manager = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Tasks.TaskManager>();
            sb.Append(" task=").Append(manager != null ? manager.CurrentTaskId : "none");
            sb.Append(" masterRms=").Append(MeasureListenerRms().ToString("0.0000"));
            AvianVoice.Study.PcAudioMonitor.RefreshDefaultOutput();
            var monitor = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Study.PcAudioMonitor>();
            sb.Append(" | ").Append(monitor != null ? monitor.Describe() : "defaultOutput='" + AvianVoice.Study.PcAudioMonitor.DefaultOutputName + "' muted=" + AvianVoice.Study.PcAudioMonitor.DefaultOutputMuted);
            sb.Append(" waveOut=[").Append(string.Join("; ", AvianVoice.Study.PcAudioMonitor.OutputDeviceNames())).Append("]");
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.Append(" | ").Append(source.gameObject.name)
                  .Append(" active=").Append(source.isActiveAndEnabled)
                  .Append(" playing=").Append(source.isPlaying)
                  .Append(" vol=").Append(source.volume.ToString("0.00"))
                  .Append(" mute=").Append(source.mute)
                  .Append(" spatial=").Append(source.spatialBlend.ToString("0.0"))
                  .Append(" clip=").Append(source.clip != null ? source.clip.name : "procedural/none")
                  .Append(" mixer=").Append(source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "-");
                if (source.isPlaying)
                    sb.Append(" rms=").Append(MeasureSourceRms(source).ToString("0.0000"));
            }

            Debug.Log(sb.ToString());
        }

        static readonly float[] RmsBuffer = new float[1024];

        /// <summary>RMS of the mixed output that reaches the AudioListener (0 = silence).</summary>
        public static float MeasureListenerRms()
        {
            AudioListener.GetOutputData(RmsBuffer, 0);
            return Rms(RmsBuffer);
        }

        static float MeasureSourceRms(AudioSource source)
        {
            source.GetOutputData(RmsBuffer, 0);
            return Rms(RmsBuffer);
        }

        static float Rms(float[] samples)
        {
            double sum = 0;
            for (var i = 0; i < samples.Length; i++)
                sum += samples[i] * samples[i];
            return Mathf.Sqrt((float)(sum / samples.Length));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Inspect Human Avatar Materials")]
        public static void InspectHumanAvatarMaterials()
        {
            foreach (var path in HumanAvatarAssetPaths)
                InspectHumanAvatarMaterials(path);
        }

        static void InspectHumanAvatarMaterials(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                Debug.Log("[AvianVoice] Human avatar not present: " + path);
                return;
            }

            var sb = new System.Text.StringBuilder("[AvianVoice] " + System.IO.Path.GetFileNameWithoutExtension(path) + ": " + DescribeAvatar(path));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftEye, HumanBodyBones.Jaw, HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes })
                {
                    var t = animator.GetBoneTransform(bone);
                    sb.Append(' ').Append(bone).Append('=').Append(t != null ? t.name : "-");
                }
            }

            if (model != null)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    sb.Append(" | ").Append(renderer.name).Append(" bounds=").Append(renderer.bounds.size).Append(" mats=").Append(string.Join("+", renderer.sharedMaterials.Select(m => m != null ? m.name : "null")));
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Material material)
                    continue;

                sb.Append(" | ").Append(material.name).Append(" shader=").Append(material.shader != null ? material.shader.name : "null");
                foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_SpecGlossMap", "_EmissionMap", "_OcclusionMap" })
                {
                    if (material.HasProperty(prop))
                        sb.Append(' ').Append(prop).Append('=').Append(material.GetTexture(prop) != null ? material.GetTexture(prop).name : "null");
                }

                foreach (var prop in new[] { "_Smoothness", "_Metallic", "_Surface", "_AlphaClip", "_WorkflowMode", "_Cull" })
                {
                    if (material.HasProperty(prop))
                        sb.Append(' ').Append(prop).Append('=').Append(material.GetFloat(prop).ToString("0.00"));
                }

                foreach (var prop in new[] { "_BaseColor", "_SpecColor", "_EmissionColor" })
                {
                    if (material.HasProperty(prop))
                        sb.Append(' ').Append(prop).Append('=').Append(material.GetColor(prop));
                }

                sb.Append(" keywords=").Append(string.Join(",", material.shaderKeywords));
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Avian Voice/Embodiment/Open Avian_Lab_Embodiment")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Avian Voice/Embodiment/Ensure Embodiment Controller (Open Scene)")]
        public static void EnsureControllerInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != AvatarEmbodimentBootstrap.SceneName)
            {
                Debug.LogWarning("[AvianVoice] Open " + ScenePath + " first.");
                return;
            }

            if (EnsureController())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Avian Voice/Embodiment/Reimport Human Avatar (Humanoid)")]
        public static void ReimportHumanAvatar()
        {
            foreach (var path in HumanAvatarAssetPaths)
            {
                if (AssetImporter.GetAtPath(path) == null)
                    continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log("[AvianVoice] Human avatar: " + DescribeAvatar(path));
            }
        }

        [MenuItem("Avian Voice/Embodiment/Configure Everything")]
        public static void ConfigureEverything()
        {
            ReimportHumanAvatar();
            ConfigureMetaBodyTracking();

            var wasOpen = SceneManager.GetActiveScene().path == ScenePath;
            var scene = wasOpen ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ScenePath);
            if (EnsureController())
                EditorSceneManager.SaveScene(scene);

            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[AvianVoice] Embodiment scene configured.");
        }

        /// <summary>Batch-mode entry point: Unity -batchmode -executeMethod AvianVoice.Editor.AvianEmbodimentSceneSetup.BatchVerify</summary>
        public static void BatchVerify()
        {
            try
            {
                ConfigureEverything();
                var avatar = DescribeAvatar();
                var controller = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>(FindObjectsInactive.Include);
                Debug.Log("[AvianVoice] EMBODIMENT VERIFY OK. avatar=" + avatar + " controller=" + (controller != null) +
                          " backendAssembly=" + (FindMetaSetupType() != null));
            }
            catch (Exception e)
            {
                Debug.LogError("[AvianVoice] EMBODIMENT VERIFY FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static bool EnsureController()
        {
            var changed = false;
            var existing = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>(FindObjectsInactive.Include);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(AvatarEmbodimentBootstrap.ControllerObjectName);
                go.AddComponent<AvatarEmbodimentController>();
                Undo.RegisterCreatedObjectUndo(go, "Add embodiment controller");
                Debug.Log("[AvianVoice] Added AvatarEmbodimentController to the scene.");
                changed = true;
            }

            if (UnityEngine.Object.FindAnyObjectByType<AvianVoice.Study.StudyFlowController>(FindObjectsInactive.Include) == null)
            {
                Undo.AddComponent<AvianVoice.Study.StudyFlowController>(go);
                Debug.Log("[AvianVoice] Added StudyFlowController to the scene.");
                changed = true;
            }

            return changed;
        }

        static void ConfigureMetaBodyTracking()
        {
            var type = FindMetaSetupType();
            if (type == null)
            {
                Debug.LogWarning("[AvianVoice] Meta XR Core SDK not installed (com.meta.xr.sdk.core); skipped body tracking configuration.");
                return;
            }

            var method = type.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public);
            var status = method?.Invoke(null, null) as string;
            Debug.Log("[AvianVoice] Meta body tracking: " + status);
        }

        static Type FindMetaSetupType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("AvianVoice.MetaBody.Editor.MetaBodyTrackingProjectSetup", false))
                .FirstOrDefault(t => t != null);
        }

        static string DescribeAvatar() => DescribeAvatar(HumanAvatarAssetPath);

        static string DescribeAvatar(string path)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
                return "no Avatar sub-asset at " + path;
            return avatar.name + " isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid;
        }

        static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
                return;

            scenes.Insert(Math.Min(1, scenes.Count), new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
