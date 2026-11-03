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
            "Assets/AvianVoice/Resources/Avatars/Rocketbox/Male_Adult_01/Male_Adult_01.fbx";

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

        [MenuItem("Avian Voice/Embodiment/Log Avatar State (Play Mode)")]
        public static void LogAvatarState()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>();
            Debug.Log("[AvianVoice] Avatar state: " + (controller != null ? controller.DescribeState() : "no AvatarEmbodimentController (is Avian_Lab_Embodiment playing?)"));
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

        /// <summary>Same as the participant pressing Space / R: the teacher bird sings the current note again.</summary>
        public static bool ReplayGuideNote()
        {
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
              .Append(" paused=").Append(AudioListener.pause)
              .Append(" listeners=").Append(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length)
              .Append(" sampleRate=").Append(config.sampleRate).Append(" dsp=").Append(config.dspBufferSize)
              .Append(" speakerMode=").Append(config.speakerMode)
              .Append(" driverCaps=").Append(AudioSettings.driverCapabilities)
              .Append(" spatializer=").Append(AudioSettings.GetSpatializerPluginName());
            var manager = UnityEngine.Object.FindAnyObjectByType<AvianVoice.Tasks.TaskManager>();
            sb.Append(" task=").Append(manager != null ? manager.CurrentTaskId : "none");
            sb.Append(" masterRms=").Append(MeasureListenerRms().ToString("0.0000"));
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
            var sb = new System.Text.StringBuilder("[AvianVoice] Human avatar materials:");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(HumanAvatarAssetPath))
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
            AssetDatabase.ImportAsset(HumanAvatarAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[AvianVoice] Human avatar: " + DescribeAvatar());
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
            var existing = UnityEngine.Object.FindAnyObjectByType<AvatarEmbodimentController>(FindObjectsInactive.Include);
            if (existing != null)
                return false;

            var go = new GameObject(AvatarEmbodimentBootstrap.ControllerObjectName);
            go.AddComponent<AvatarEmbodimentController>();
            Undo.RegisterCreatedObjectUndo(go, "Add embodiment controller");
            Debug.Log("[AvianVoice] Added AvatarEmbodimentController to the scene.");
            return true;
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

        static string DescribeAvatar()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(HumanAvatarAssetPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
                return "no Avatar sub-asset at " + HumanAvatarAssetPath;
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
