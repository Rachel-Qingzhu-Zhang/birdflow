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
