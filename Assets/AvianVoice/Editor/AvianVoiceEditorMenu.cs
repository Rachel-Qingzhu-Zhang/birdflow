#if UNITY_EDITOR
using System.IO;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Editor
{
    public static class AvianVoiceEditorMenu
    {
        const string Root = "Assets/AvianVoice";
        const string SettingsPath = Root + "/Settings/AvianVoiceSettings.asset";
        const string PlatformPath = Root + "/Settings/PlatformProfile.asset";
        const string LabScenePath = Root + "/Scenes/Avian_Lab.unity";
        const string PigeonPrefabPath = "Assets/ANIMALS FULL PACK/Birds Pack/Pigeon/Prefabs/Pigeon_PBR.prefab";

        [MenuItem("Avian Voice/Setup Project")]
        public static void SetupProject()
        {
            EnsureFolders();
            EnsureSettingsAssets();
            EnsureLayers();
            ConfigureAudioLatency();
            ConfigureAndroidPermissions();
            CreateLabScene();
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AvianVoice] Project setup complete.");
        }

        [MenuItem("Avian Voice/Ensure In-Scene Task Switcher (Open Scene)")]
        public static void EnsureInSceneTaskSwitcherInOpenScene()
        {
            if (!SceneManager.GetActiveScene().name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab (or another Avian_* scene) first.");
                return;
            }

            AvianInSceneTaskBootstrap.EnsureForLabScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AvianVoice] In-scene TaskManager wiring refreshed. Play: keys 1-4 switch tasks.");
        }

        [MenuItem("Avian Voice/Apply Avian Lab Setup (Parrot + Breath)")]
        public static void ApplyAvianLabSetup()
        {
            EnsureSettingsAssets();
            WireBootstrapOnXrRig();
            ApplyParrotSceneOverrides();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AvianVoice] Avian Lab setup applied. Save scene (Ctrl+S), then Play and hold Space.");
        }

        const string ParrotPrefabPath = "Assets/Parrot/Prefabs/Parrot.prefab";

        [MenuItem("Avian Voice/Clean Up Stale Lab Scene")]
        public static void CleanUpStaleLabScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab first.");
                return;
            }

            var friend = FindSceneBird(scene, BirdSceneRoles.FriendName);
            var player = FindSceneBird(scene, BirdSceneRoles.PlayerName);
            if (friend == null || player == null)
            {
                Debug.LogWarning("[AvianVoice] Need FriendParrot and PlayerParrot in the scene.");
                return;
            }

            SetupSceneParrot(friend, isFriend: true);
            SetupSceneParrot(player, isFriend: false);

            var perch = GameObject.Find(BirdSceneRoles.PerchName)?.transform;
            if (perch != null)
                PositionPerchParrotsAndMirror(scene, perch, friend, player);
            else
                PlaceMirrorAtPerchInternal(scene, log: false);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AvianVoice] Lab scene cleanup done. Save scene (Ctrl+S).");
        }

        static Transform FindSceneBird(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == objectName)
                        return t;
                }
            }

            return null;
        }

        [MenuItem("Avian Voice/Place Two Parrots On Perch")]
        public static void PlaceTwoParrotsOnPerch()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab (or another Avian_* scene) first.");
                return;
            }

            var perch = GameObject.Find(BirdSceneRoles.PerchName);
            if (perch == null)
            {
                Debug.LogWarning("[AvianVoice] No '" + BirdSceneRoles.PerchName + "' perch found in scene.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParrotPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[AvianVoice] Parrot prefab not found at " + ParrotPrefabPath);
                return;
            }

            var friend = FindOrCreateSceneParrot(prefab, BirdSceneRoles.FriendName, BirdSceneRole.RoleKind.Friend);
            var player = FindOrCreateSceneParrot(prefab, BirdSceneRoles.PlayerName, BirdSceneRole.RoleKind.Player);
            PositionPerchParrotsAndMirror(scene, perch.transform, friend, player);
            SetupSceneParrot(friend, isFriend: true);
            SetupSceneParrot(player, isFriend: false);
            RemoveExtraParrotCopies(friend, player);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "[AvianVoice] Layout ready: mirror in front of XR Origin, parrots on perch behind headset. Save (Ctrl+S), then Play.");
        }

        [MenuItem("Avian Voice/Place Mirror At Perch")]
        public static void PlaceMirrorAtPerch()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab (or another Avian_* scene) first.");
                return;
            }

            PlaceMirrorAtPerchInternal(scene, log: true);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void PlaceMirrorAtPerchInternal(Scene scene, bool log)
        {
            var xr = FindHandsXrOrigin(scene);
            if (xr == null)
            {
                Debug.LogWarning("[AvianVoice] No XR Origin found in scene.");
                return;
            }

            var viewForward = xr.transform.forward;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.01f)
                viewForward = Vector3.forward;
            viewForward.Normalize();

            var viewerEye = AvianLabSceneLayout.ViewerEyePoint(xr.transform);
            var mirrorSize = new Vector2(1.5f, 2.2f);
            var mirrorPos = AvianLabSceneLayout.ResolveMirrorPositionNearBirds(
                xr.transform,
                viewForward,
                AvianLabSceneLayout.MirrorAheadMeters,
                AvianLabSceneLayout.MirrorPanelHeightMeters);
            var mirrorRot = AvianLabSceneLayout.ResolveMirrorRotation(mirrorPos, viewerEye);

            var root = GameObject.Find(MirrorBodySetup.WorldMirrorObjectName);
            if (root == null)
            {
                root = new GameObject(MirrorBodySetup.WorldMirrorObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Place mirror");
            }

            root.transform.SetPositionAndRotation(mirrorPos, mirrorRot);
            root.transform.localScale = Vector3.one;
            SetLayerRecursive(root, BirdSceneRoles.MirrorDisplayLayer);

            if (root.GetComponent<WorldMirrorScenePlacement>() == null)
                Undo.AddComponent<WorldMirrorScenePlacement>(root);

            var quad = root.transform.Find("MirrorQuad");
            if (quad == null)
            {
                var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(quadGo, "Place mirror quad");
                quadGo.name = "MirrorQuad";
                quadGo.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(quadGo.GetComponent<Collider>());
                quad = quadGo.transform;
            }

            quad.localPosition = Vector3.zero;
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(mirrorSize.x, mirrorSize.y, 1f);
            var quadRenderer = quad.GetComponent<MeshRenderer>();
            if (quadRenderer != null)
                MirrorBodySetup.ConfigureMirrorPanelRenderer(quadRenderer);

            Fragilem17MirrorBridge.ConfigureMirrorQuad(
                quad.gameObject,
                new Fragilem17MirrorBridge.Settings
                {
                    TextureSize = 1536,
                    MirrorPanelSize = new Vector2(1.5f, 2.2f),
                    MaxRenderingDistance = 80f,
                    ReflectionLayers = PlanarReflectionUtility.BuildDefaultReflectionMask(),
                    MirrorMaterial = Fragilem17MirrorBridge.ResolveDefaultMaterial()
                });

            var frame = root.transform.Find("MirrorFrame");
            if (frame == null)
            {
                var frameGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(frameGo, "Place mirror frame");
                frameGo.name = "MirrorFrame";
                frameGo.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(frameGo.GetComponent<Collider>());
                frame = frameGo.transform;
            }

            frame.localPosition = new Vector3(0f, 0f, -0.04f);
            frame.localRotation = Quaternion.identity;
            frame.localScale = new Vector3(mirrorSize.x + 0.18f, mirrorSize.y + 0.18f, 1f);
            var frameRenderer = frame.GetComponent<MeshRenderer>();
            if (frameRenderer != null)
            {
                var frameShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (frameShader != null)
                {
                    var frameMat = new Material(frameShader);
                    frameMat.color = new Color(0.15f, 0.72f, 0.95f, 1f);
                    frameRenderer.sharedMaterial = frameMat;
                }

                MirrorBodySetup.ConfigureMirrorPanelRenderer(frameRenderer);
            }

            if (log)
            {
                Debug.Log(
                    "[AvianVoice] Mirror placed at " + mirrorPos +
                    " in front of XR Origin. Save scene (Ctrl+S).");
            }
        }

        static void PositionPerchParrotsAndMirror(
            Scene scene,
            Transform perch,
            Transform friend,
            Transform player)
        {
            var xr = FindHandsXrOrigin(scene);
            if (xr == null)
            {
                Debug.LogWarning("[AvianVoice] No XR Origin found; cannot apply lab layout.");
                return;
            }

            var viewForward = xr.transform.forward;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.01f)
                viewForward = Vector3.forward;
            viewForward.Normalize();

            var perchPos = AvianLabSceneLayout.ResolvePerchPosition(xr.transform, viewForward);
            Undo.RecordObject(perch, "Position Perch");
            perch.position = perchPos;

            var standCenter = AvianLabSceneLayout.ResolveParrotStandCenter(perchPos);
            AvianLabSceneLayout.ResolveParrotPositions(
                standCenter,
                viewForward,
                out var friendPos,
                out var playerPos);
            var faceMirror = AvianLabSceneLayout.ResolveParrotFacingMirror(viewForward);

            Undo.RecordObject(friend, "Position FriendParrot");
            Undo.RecordObject(player, "Position PlayerParrot");
            friend.SetPositionAndRotation(friendPos, faceMirror);
            player.SetPositionAndRotation(playerPos, faceMirror);

            PlaceMirrorAtPerchInternal(scene, log: false);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        [MenuItem("Avian Voice/Wire Bootstrap On XR Rig")]
        public static void WireBootstrapOnXrRig()
        {
            EnsureSettingsAssets();
            var scene = SceneManager.GetActiveScene();
            var handsOrigin = FindHandsXrOrigin(scene);
            if (handsOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] No 'XR Origin Hands' found in open scene.");
                return;
            }

            var bootstrap = handsOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[AvianVoice] AvianAppBootstrap missing on " + handsOrigin.name);
                return;
            }

            var headAnchor = FindCameraOffset(handsOrigin.transform) ?? handsOrigin.transform;
            var transformSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.TransformSourceName);
            var assistiveSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.AssistiveSourceName);
            var library = handsOrigin.GetComponent<BirdCallLibrary>();
            var profile = AssetDatabase.LoadAssetAtPath<PlatformProfile>(PlatformPath);
            var birdRoot = FindActiveBirdRoot(handsOrigin.transform);
            var birdAnimator = birdRoot != null ? birdRoot.GetComponent<Animator>() : null;

            var so = new SerializedObject(bootstrap);
            so.FindProperty("headAnchor").objectReferenceValue = headAnchor;
            so.FindProperty("transformBirdSource").objectReferenceValue = transformSource;
            so.FindProperty("assistiveMonitorSource").objectReferenceValue = assistiveSource;
            so.FindProperty("callLibrary").objectReferenceValue = library;
            so.FindProperty("platformProfile").objectReferenceValue = profile;
            if (birdAnimator != null)
                so.FindProperty("birdAnimator").objectReferenceValue = birdAnimator;

            WireBirdEmbodimentBones(so, birdRoot);
            var autoWire = so.FindProperty("autoWireParrotEmbodiment");
            if (autoWire != null)
                autoWire.boolValue = true;
            so.ApplyModifiedProperties();

            ApplyParrotSceneOverrides();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AvianVoice] Wired AvianAppBootstrap on '" + handsOrigin.name +
                      "'. Save scene (Ctrl+S). Play: speak into mic (Transform mode) for bird calls.");
        }

        [MenuItem("Avian Voice/Scene Setup/Add Per-Task Start Poses (Open Scene)")]
        public static void AddTaskStartPosesInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab first — per-task poses are for the six in-scene tasks.");
                return;
            }

            var xr = FindPreferredXrOrigin(scene);
            if (xr == null)
            {
                Debug.LogWarning("[AvianVoice] No XR Origin found in open scene.");
                return;
            }

            EnsureTaskStartPoses(xr);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = xr;
            Debug.Log(
                "[AvianVoice] Per-task start poses added on '" + xr.name +
                "'. Move XR Origin, pick task 1-6 in Inspector, Capture, then Save scene.");
        }

        [MenuItem("Avian Voice/Setup Pigeon FPV Body In Open Scene")]
        public static void SetupPigeonFpvBodyInOpenScene()
        {
            EnsureSettingsAssets();
            var scene = SceneManager.GetActiveScene();
            var handsOrigin = FindHandsXrOrigin(scene);
            if (handsOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] No 'XR Origin Hands' found in open scene.");
                return;
            }

            var headAnchor = FindCameraOffset(handsOrigin.transform) ?? handsOrigin.transform;
            var pigeonRoot = EnsurePigeonInScene(headAnchor);
            if (pigeonRoot == null)
            {
                Debug.LogWarning("[AvianVoice] Could not find or create Pigeon_PBR in scene.");
                return;
            }

            EnsurePigeonComponents(pigeonRoot.gameObject);
            DisableConflictingBirds(headAnchor);

            var bootstrap = handsOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[AvianVoice] AvianAppBootstrap missing on " + handsOrigin.name);
                return;
            }

            var transformSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.TransformSourceName);
            var assistiveSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.AssistiveSourceName);
            var library = handsOrigin.GetComponent<BirdCallLibrary>();
            var profile = AssetDatabase.LoadAssetAtPath<PlatformProfile>(PlatformPath);

            var so = new SerializedObject(bootstrap);
            so.FindProperty("headAnchor").objectReferenceValue = headAnchor;
            so.FindProperty("transformBirdSource").objectReferenceValue = transformSource;
            so.FindProperty("assistiveMonitorSource").objectReferenceValue = assistiveSource;
            so.FindProperty("callLibrary").objectReferenceValue = library;
            so.FindProperty("platformProfile").objectReferenceValue = profile;
            WireBirdEmbodimentBones(so, pigeonRoot);
            so.FindProperty("autoWireParrotEmbodiment").boolValue = false;

            var mirror = bootstrap.GetComponent<MirrorBodySetup>();
            if (mirror == null)
                mirror = Undo.AddComponent<MirrorBodySetup>(handsOrigin);
            var mirrorSo = new SerializedObject(mirror);
            mirrorSo.FindProperty("birdBodyRoot").objectReferenceValue = pigeonRoot;
            mirrorSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("mirrorSetup").objectReferenceValue = mirror;

            so.ApplyModifiedPropertiesWithoutUndo();
            ApplyParrotSceneOverrides();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[AvianVoice] Pigeon FPV body wired on '" + handsOrigin.name +
                      "'. Save scene (Ctrl+S), then Play and hold Space for breath.");
        }

        [MenuItem("Avian Voice/Restore Parrot Body In Open Scene")]
        public static void RestoreParrotBodyInOpenScene()
        {
            EnsureSettingsAssets();
            var scene = SceneManager.GetActiveScene();
            var handsOrigin = FindHandsXrOrigin(scene);
            if (handsOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] No 'XR Origin Hands' found in open scene.");
                return;
            }

            DisablePigeonInScene();

            Transform parrot = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Parrot")
                    parrot = t;
            }

            if (parrot == null)
            {
                Debug.LogWarning("[AvianVoice] No 'Parrot' found in scene. Add Assets/Parrot/Prefabs/Parrot.prefab first.");
                return;
            }

            parrot.gameObject.SetActive(true);
            EnsureParrotComponents(parrot.gameObject);

            var bootstrap = handsOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[AvianVoice] AvianAppBootstrap missing on " + handsOrigin.name);
                return;
            }

            var headAnchor = FindCameraOffset(handsOrigin.transform) ?? handsOrigin.transform;
            var transformSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.TransformSourceName);
            var assistiveSource = FindOrCreateEditorAudioSource(
                handsOrigin.transform, AvianAudioSourceUtility.AssistiveSourceName);
            var library = handsOrigin.GetComponent<BirdCallLibrary>();
            var profile = AssetDatabase.LoadAssetAtPath<PlatformProfile>(PlatformPath);

            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("headAnchor").objectReferenceValue = headAnchor;
            bootstrapSo.FindProperty("transformBirdSource").objectReferenceValue = transformSource;
            bootstrapSo.FindProperty("assistiveMonitorSource").objectReferenceValue = assistiveSource;
            bootstrapSo.FindProperty("callLibrary").objectReferenceValue = library;
            bootstrapSo.FindProperty("platformProfile").objectReferenceValue = profile;
            WireBirdEmbodimentBones(bootstrapSo, parrot);
            bootstrapSo.FindProperty("autoWireParrotEmbodiment").boolValue = true;

            var mirror = bootstrap.GetComponent<MirrorBodySetup>();
            if (mirror == null)
                mirror = Undo.AddComponent<MirrorBodySetup>(handsOrigin);
            var mirrorSo = new SerializedObject(mirror);
            mirrorSo.FindProperty("birdBodyRoot").objectReferenceValue = parrot;
            mirrorSo.ApplyModifiedPropertiesWithoutUndo();
            bootstrapSo.FindProperty("mirrorSetup").objectReferenceValue = mirror;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            ApplyParrotSceneOverrides();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AvianVoice] Parrot body restored. Save scene (Ctrl+S), then Play.");
        }

        static void DisablePigeonInScene()
        {
            foreach (var setup in Object.FindObjectsByType<PigeonFpvSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (setup != null)
                    setup.gameObject.SetActive(false);
            }

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!PigeonEmbodimentWiring.IsPigeonRoot(t))
                    continue;
                t.gameObject.SetActive(false);
            }
        }

        static void EnsureParrotComponents(GameObject parrotRoot)
        {
            ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(parrotRoot.GetComponent<Animator>());
            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(parrotRoot.transform);

            if (parrotRoot.GetComponent<WorldPlacedBird>() == null)
                parrotRoot.AddComponent<WorldPlacedBird>();
            if (parrotRoot.GetComponent<ParrotBreathEmbodiment>() == null)
                parrotRoot.AddComponent<ParrotBreathEmbodiment>();
            if (parrotRoot.GetComponent<ParrotMainCtrlPin>() == null)
                parrotRoot.AddComponent<ParrotMainCtrlPin>();
        }

        [MenuItem("Avian Voice/Add Pigeon FPV Components To Prefab")]
        public static void AddPigeonFpvComponentsToPrefab()
        {
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PigeonPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning("[AvianVoice] Pigeon prefab not found at " + PigeonPrefabPath);
                return;
            }

            var path = AssetDatabase.GetAssetPath(prefabRoot);
            var root = PrefabUtility.LoadPrefabContents(path);
            EnsurePigeonComponents(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[AvianVoice] Added Pigeon FPV components to prefab: " + PigeonPrefabPath);
        }

        [MenuItem("Avian Voice/Fix Duplicate XR Rigs In Open Scene")]
        public static void FixDuplicateXrRigsInOpenScene()
        {
            if (!FixDuplicateXrRigs(SceneManager.GetActiveScene()))
            {
                Debug.LogWarning("[AvianVoice] No duplicate XR rigs fixed (scene may already be OK).");
                return;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AvianVoice] Duplicate XR rigs fixed in open scene. Save the scene (Ctrl+S).");
        }

        [MenuItem("Avian Voice/Fix Duplicate XR Rigs In All Avian Scenes")]
        public static void FixDuplicateXrRigsInAllScenes()
        {
            var scenePaths = new[]
            {
                LabScenePath,
                Root + "/Scenes/Task_PitchMatching.unity",
                Root + "/Scenes/Task_FlightControl.unity",
                Root + "/Scenes/Task_SongPerformance.unity"
            };

            var fixedCount = 0;
            var activePath = SceneManager.GetActiveScene().path;
            foreach (var path in scenePaths)
            {
                if (!File.Exists(path))
                    continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (FixDuplicateXrRigs(scene))
                {
                    EditorSceneManager.SaveScene(scene);
                    fixedCount++;
                }
            }

            if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            Debug.Log($"[AvianVoice] Fixed duplicate XR rigs in {fixedCount} scene(s).");
        }

        [MenuItem("Avian Voice/Create Task Scenes")]
        public static void CreateTaskScenes()
        {
            EnsureFolders();
            CreateSceneFromTemplate(Root + "/Scenes/Task_PitchMatching.unity", "PitchMatching");
            CreateSceneFromTemplate(Root + "/Scenes/Task_FlightControl.unity", "FlightControl");
            CreateSceneFromTemplate(Root + "/Scenes/Task_SongPerformance.unity", "SongPerformance");
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[AvianVoice] Task scenes created.");
        }

        static void EnsureFolders()
        {
            var folders = new[]
            {
                Root,
                Root + "/Scenes",
                Root + "/Scripts",
                Root + "/Settings",
                Root + "/Prefabs",
                Root + "/Audio",
                Root + "/Animations",
                Root + "/Materials",
                Root + "/Editor"
            };
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                    var name = Path.GetFileName(folder);
                    if (!string.IsNullOrEmpty(parent))
                        AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        static void EnsureSettingsAssets()
        {
            if (!File.Exists(SettingsPath))
            {
                var settings = ScriptableObject.CreateInstance<AvianVoiceSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            if (!File.Exists(PlatformPath))
            {
                var profile = ScriptableObject.CreateInstance<PlatformProfile>();
                AssetDatabase.CreateAsset(profile, PlatformPath);
            }
        }

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            SetLayerName(layers, 8, "BirdFPV");
            SetLayerName(layers, 9, "BirdMirror");
            SetLayerName(layers, 10, "MirrorDisplay");
            SetLayerName(layers, 11, "AvianUI");
            tagManager.ApplyModifiedProperties();
        }

        static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            var prop = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(prop.stringValue))
                prop.stringValue = name;
        }

        static void ConfigureAudioLatency()
        {
            var audioManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            audioManager.FindProperty("m_DSPBufferSize").intValue = 512;
            audioManager.FindProperty("m_RequestedDSPBufferSize").intValue = 512;
            audioManager.ApplyModifiedProperties();
        }

        static void ConfigureAndroidPermissions()
        {
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var androidSettings = playerSettings.FindProperty("Android");
            if (androidSettings != null)
            {
                // Microphone permission for Quest / Android
                PlayerSettings.Android.forceInternetPermission = false;
            }
        }

        static void CreateLabScene()
        {
            if (File.Exists(LabScenePath))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var setupGo = new GameObject("AvianLabRuntimeSetup");
            var setup = setupGo.AddComponent<AvianLabRuntimeSetup>();
            var settings = AssetDatabase.LoadAssetAtPath<AvianVoiceSettings>(SettingsPath);
            var profile = AssetDatabase.LoadAssetAtPath<PlatformProfile>(PlatformPath);
            var so = new SerializedObject(setup);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("platformProfile").objectReferenceValue = profile;
            so.ApplyModifiedProperties();

            var handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/XR Interaction Toolkit/3.4.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab");
            if (handsPrefab != null)
                PrefabUtility.InstantiatePrefab(handsPrefab);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            EditorSceneManager.SaveScene(scene, LabScenePath);
        }

        static void CreateSceneFromTemplate(string path, string taskName)
        {
            if (File.Exists(path))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var setupGo = new GameObject("AvianLabRuntimeSetup");
            setupGo.AddComponent<AvianLabRuntimeSetup>();

            var handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/XR Interaction Toolkit/3.4.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab");
            if (handsPrefab != null)
                PrefabUtility.InstantiatePrefab(handsPrefab);

            var taskRoot = new GameObject(taskName + "TaskRoot");
            switch (taskName)
            {
                case "PitchMatching":
                    taskRoot.AddComponent<PitchMatchingTask>();
                    break;
                case "FlightControl":
                    taskRoot.AddComponent<FlightControlTask>();
                    taskRoot.AddComponent<CharacterController>();
                    break;
                case "SongPerformance":
                    taskRoot.AddComponent<SongPerformanceTask>();
                    break;
            }

            taskRoot.AddComponent<TaskManager>();
            EditorSceneManager.SaveScene(scene, path);
        }

        static void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                LabScenePath,
                Root + "/Scenes/Task_PitchMatching.unity",
                Root + "/Scenes/Task_FlightControl.unity",
                Root + "/Scenes/Task_SongPerformance.unity"
            };

            var existing = EditorBuildSettings.scenes;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
            foreach (var path in scenes)
            {
                if (!File.Exists(path))
                    continue;
                var already = false;
                foreach (var s in list)
                {
                    if (s.path == path)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                    list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        static bool FixDuplicateXrRigs(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            GameObject handsOrigin = null;
            var legacyOrigins = new System.Collections.Generic.List<GameObject>();

            foreach (var root in roots)
            {
                if (!root.name.Contains("XR Origin"))
                    continue;
                if (root.name.Contains("Hands"))
                    handsOrigin = root;
                else
                    legacyOrigins.Add(root);
            }

            if (legacyOrigins.Count == 0)
                return false;

            if (handsOrigin == null)
            {
                Debug.LogWarning($"[AvianVoice] Scene '{scene.name}' has legacy XR Origin but no 'XR Origin Hands'. Add Hands prefab first.");
                return false;
            }

            var headAnchor = FindCameraOffset(handsOrigin.transform);
            if (headAnchor == null)
                headAnchor = handsOrigin.transform;

            var changed = false;
            foreach (var legacy in legacyOrigins)
            {
                if (legacy.activeSelf)
                {
                    legacy.SetActive(false);
                    changed = true;
                    Debug.Log($"[AvianVoice] Disabled legacy rig: {legacy.name}");
                }
            }

            changed |= ReparentCrowToHandsRig(legacyOrigins, headAnchor);
            changed |= FixBootstrapHeadAnchor(handsOrigin, headAnchor);

            return changed;
        }

        static Transform FindCameraOffset(Transform xrOrigin)
        {
            foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Camera Offset")
                    return t;
            }
            return null;
        }

        static bool ReparentCrowToHandsRig(
            System.Collections.Generic.List<GameObject> legacyOrigins,
            Transform headAnchor)
        {
            var changed = false;
            foreach (var legacy in legacyOrigins)
            {
                foreach (var t in legacy.GetComponentsInChildren<Transform>(true))
                {
                if (t.name != "Crow" && t.name != "Parrot" && !t.name.Contains("AvianBody"))
                    continue;
                if (t.name == "Parrot" || t.GetComponent<WorldPlacedBird>() != null)
                    continue;
                    if (t.parent == headAnchor)
                        continue;
                    t.SetParent(headAnchor, false);
                    changed = true;
                    Debug.Log($"[AvianVoice] Reparented '{t.name}' under '{headAnchor.name}'.");
                }
            }
            return changed;
        }

        static GameObject FindHandsXrOrigin(Scene scene) => FindPreferredXrOrigin(scene);

        static GameObject FindPreferredXrOrigin(Scene scene)
        {
            GameObject hands = null;
            GameObject fallback = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.Contains("XR Origin"))
                    continue;
                if (root.name.Contains("Hands"))
                    hands = root;
                else if (fallback == null)
                    fallback = root;
            }

            return hands != null ? hands : fallback;
        }

        static void EnsureTaskStartPoses(GameObject xrOrigin)
        {
            if (xrOrigin.GetComponent<XrRigTaskStartPoses>() == null)
                Undo.AddComponent<XrRigTaskStartPoses>(xrOrigin);
        }

        static AudioSource FindOrCreateEditorAudioSource(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
                go.transform.SetParent(parent, false);
                child = go.transform;
            }

            if (!child.TryGetComponent<AudioSource>(out var source))
            {
                source = Undo.AddComponent<AudioSource>(child.gameObject);
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.volume = 0.8f;
            }

            return source;
        }

        static Transform FindActiveBirdRoot(Transform xrOrigin)
        {
            Transform parrot = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name == "Parrot")
                    parrot = t;
            }

            if (parrot != null)
                return parrot;

            var pigeon = PigeonFpvSetup.FindPigeonInScene();
            if (pigeon != null)
                return pigeon;

            Transform crow = null;
            Transform placeholder = null;

            foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.activeInHierarchy)
                    continue;
                if (t.name == "Crow")
                    crow = t;
                else if (t.name.Contains("AvianBody"))
                    placeholder = t;
            }

            if (crow != null)
                return crow;
            return placeholder;
        }

        static Transform EnsurePigeonInScene(Transform headAnchor)
        {
            var pigeon = PigeonFpvSetup.FindPigeonInScene();
            if (pigeon == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PigeonPrefabPath);
                if (prefab == null)
                    return null;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create Pigeon FPV");
                pigeon = instance.transform;
            }

            var setup = pigeon.GetComponent<PigeonFpvSetup>();
            var followCamera = setup != null && setup.PlacementMode == PigeonPlacementMode.FollowCamera;

            if (followCamera && pigeon.parent != headAnchor)
                Undo.SetTransformParent(pigeon, headAnchor, "Parent Pigeon under Camera Offset");

            if (followCamera)
            {
                pigeon.localPosition = new Vector3(0f, -0.2f, 0.25f);
                pigeon.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            if (pigeon.localScale.sqrMagnitude < 0.01f)
                pigeon.localScale = Vector3.one;

            return pigeon;
        }

        static void EnsurePigeonComponents(GameObject pigeonRoot)
        {
            if (pigeonRoot.GetComponent<PigeonFpvSetup>() == null)
                Undo.AddComponent<PigeonFpvSetup>(pigeonRoot);
            if (pigeonRoot.GetComponent<PigeonBreathEmbodiment>() == null)
                Undo.AddComponent<PigeonBreathEmbodiment>(pigeonRoot);
            if (pigeonRoot.GetComponent<ParrotMatchVisuals>() == null)
                Undo.AddComponent<ParrotMatchVisuals>(pigeonRoot);

            PigeonEmbodimentWiring.DisablePackAnimations(pigeonRoot.GetComponent<Animator>());
        }

        static void DisableConflictingBirds(Transform headAnchor)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Parrot" && t.GetComponentInParent<PigeonFpvSetup>() == null)
                    t.gameObject.SetActive(false);
                if (t.name == "Crow" && t.parent == headAnchor)
                    t.gameObject.SetActive(false);
            }
        }

        static void WireBirdEmbodimentBones(SerializedObject bootstrapSo, Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            if (PigeonEmbodimentWiring.TryResolve(birdRoot, out var pigeonRefs))
            {
                bootstrapSo.FindProperty("leftWingBone").objectReferenceValue = pigeonRefs.leftWing;
                bootstrapSo.FindProperty("rightWingBone").objectReferenceValue = pigeonRefs.rightWing;
                bootstrapSo.FindProperty("chestTransform").objectReferenceValue = pigeonRefs.chest;
                bootstrapSo.FindProperty("secondaryChestTransform").objectReferenceValue = pigeonRefs.secondaryChest;
                bootstrapSo.FindProperty("beakTransform").objectReferenceValue = pigeonRefs.head;
                bootstrapSo.FindProperty("beakMesh").objectReferenceValue = pigeonRefs.skinMesh;
                bootstrapSo.FindProperty("beakBlendShapeIndex").intValue = -1;
                if (pigeonRefs.animator != null)
                    bootstrapSo.FindProperty("birdAnimator").objectReferenceValue = pigeonRefs.animator;
                return;
            }

            if (ParrotEmbodimentWiring.TryResolve(birdRoot, out var parrotRefs))
            {
                bootstrapSo.FindProperty("leftWingBone").objectReferenceValue = parrotRefs.leftWing;
                bootstrapSo.FindProperty("rightWingBone").objectReferenceValue = parrotRefs.rightWing;
                bootstrapSo.FindProperty("chestTransform").objectReferenceValue = parrotRefs.chest;
                bootstrapSo.FindProperty("secondaryChestTransform").objectReferenceValue = parrotRefs.secondaryChest;
                if (parrotRefs.animator != null)
                    bootstrapSo.FindProperty("birdAnimator").objectReferenceValue = parrotRefs.animator;

                var headBone = parrotRefs.neck != null
                    ? parrotRefs.neck.Find("parrot_Head_bone")
                    : null;
                if (headBone == null)
                    headBone = FindBoneByName(birdRoot, "parrot_Head_bone");
                if (headBone != null)
                    bootstrapSo.FindProperty("beakTransform").objectReferenceValue = headBone;
                return;
            }

            var leftProp = bootstrapSo.FindProperty("leftWingBone");
            var rightProp = bootstrapSo.FindProperty("rightWingBone");
            var chestProp = bootstrapSo.FindProperty("chestTransform");
            var secondaryChestProp = bootstrapSo.FindProperty("secondaryChestTransform");

            var leftWing = FindWingBoneByName(birdRoot, true);
            var rightWing = FindWingBoneByName(birdRoot, false);
            if (leftWing != null)
                leftProp.objectReferenceValue = leftWing;
            if (rightWing != null)
                rightProp.objectReferenceValue = rightWing;

            var spine3 = FindBoneByName(birdRoot, "parrot_Spine3_bone");
            var spine2 = FindBoneByName(birdRoot, "parrot_Spine2_bone");
            if (spine3 != null)
                chestProp.objectReferenceValue = spine3;
            else if (spine2 != null)
                chestProp.objectReferenceValue = spine2;

            if (spine2 != null)
                secondaryChestProp.objectReferenceValue = spine2;
        }

        static Transform FindBoneByName(Transform root, string boneName)
        {
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == boneName)
                    return bone;
            }

            return null;
        }

        static void ApplyParrotSceneOverrides()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.Contains("AvianLabRuntimeSetup"))
                    continue;
                var setup = root.GetComponent<AvianLabRuntimeSetup>();
                if (setup == null)
                    continue;
                var setupSo = new SerializedObject(setup);
                setupSo.FindProperty("autoSetup").boolValue = false;
                setupSo.FindProperty("settings").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AvianVoiceSettings>(SettingsPath);
                setupSo.FindProperty("platformProfile").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PlatformProfile>(PlatformPath);
                setupSo.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var parrot in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (parrot.name is not ("Parrot" or BirdSceneRoles.FriendName or BirdSceneRoles.PlayerName))
                    continue;

                ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(parrot.GetComponent<Animator>());
                ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(parrot);

                if (parrot.GetComponent<WorldPlacedBird>() == null)
                    parrot.gameObject.AddComponent<WorldPlacedBird>();

                var rb = parrot.GetComponent<Rigidbody>();
                if (rb != null)
                    Object.DestroyImmediate(rb, true);

                foreach (var behaviour in parrot.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && behaviour.GetType().Name == "Parrot")
                        Object.DestroyImmediate(behaviour, true);
                }
            }
        }

        static Transform FindOrCreateSceneParrot(GameObject prefab, string objectName, BirdSceneRole.RoleKind role)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                EnsureBirdSceneRole(existing.transform, role);
                return existing.transform;
            }

            if (role == BirdSceneRole.RoleKind.Friend)
            {
                var legacy = GameObject.Find("Parrot");
                if (legacy != null)
                {
                    Undo.RecordObject(legacy, "Rename Parrot");
                    legacy.name = objectName;
                    EnsureBirdSceneRole(legacy.transform, role);
                    return legacy.transform;
                }
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(instance, "Place " + objectName);
            instance.name = objectName;
            EnsureBirdSceneRole(instance.transform, role);
            return instance.transform;
        }

        static void EnsureBirdSceneRole(Transform bird, BirdSceneRole.RoleKind role)
        {
            var marker = bird.GetComponent<BirdSceneRole>();
            if (marker == null)
                marker = Undo.AddComponent<BirdSceneRole>(bird.gameObject);
            marker.SetRole(role);
        }

        static void PositionParrotsOnPerch(Transform friend, Transform player, Transform perch)
        {
            if (!AvianLabSceneLayout.TryResolveXrOrigin(out var xrOrigin, out var viewForward))
                return;

            PositionPerchParrotsAndMirror(
                SceneManager.GetActiveScene(),
                perch,
                friend,
                player);
        }

        static void PersistFixedWorldPlacement(WorldPlacedBird guard)
        {
            guard.ConfigureForFixedWorldPlacement();
            var so = new SerializedObject(guard);
            so.FindProperty("repositionInFrontOfXrOnPlay").boolValue = false;
            so.FindProperty("alignToGroundOnPlay").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(guard);
        }

        static void SetupSceneParrot(Transform parrot, bool isFriend)
        {
            ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(parrot.GetComponent<Animator>());
            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(parrot);

            var guard = parrot.GetComponent<WorldPlacedBird>();
            if (guard == null)
                guard = Undo.AddComponent<WorldPlacedBird>(parrot.gameObject);
            PersistFixedWorldPlacement(guard);

            if (isFriend)
            {
                var playerBreath = parrot.GetComponent<ParrotBreathEmbodiment>();
                if (playerBreath != null)
                    Undo.DestroyObjectImmediate(playerBreath);
                if (parrot.GetComponent<FriendBreathVisual>() == null)
                    Undo.AddComponent<FriendBreathVisual>(parrot.gameObject);
                if (parrot.GetComponent<FriendTeachingCue>() == null)
                    Undo.AddComponent<FriendTeachingCue>(parrot.gameObject);
            }
            else
            {
                var friendVisual = parrot.GetComponent<FriendBreathVisual>();
                if (friendVisual != null)
                    Undo.DestroyObjectImmediate(friendVisual);
                if (parrot.GetComponent<ParrotBreathEmbodiment>() == null)
                    Undo.AddComponent<ParrotBreathEmbodiment>(parrot.gameObject);
            }

            if (parrot.GetComponent<ParrotMainCtrlPin>() == null)
                Undo.AddComponent<ParrotMainCtrlPin>(parrot.gameObject);

            var rb = parrot.GetComponent<Rigidbody>();
            if (rb != null)
                Undo.DestroyObjectImmediate(rb);
        }

        static void RemoveExtraParrotCopies(Transform friend, Transform player)
        {
            foreach (var parrot in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (parrot == friend || parrot == player)
                    continue;
                if (parrot.name is not ("Parrot" or BirdSceneRoles.FriendName or BirdSceneRoles.PlayerName))
                    continue;
                Undo.DestroyObjectImmediate(parrot.gameObject);
            }
        }

        static Transform FindWingBoneByName(Transform birdRoot, bool isLeft)
        {
            foreach (var bone in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                var n = bone.name.ToLowerInvariant();
                if (!n.Contains("wing") && !n.Contains("humerus") && !n.Contains("ulna") && !n.Contains("feather"))
                    continue;
                if (isLeft && (n.Contains("_l_") || n.Contains("left") || n.EndsWith("_l")))
                    return bone;
                if (!isLeft && (n.Contains("_r_") || n.Contains("right") || n.EndsWith("_r")))
                    return bone;
            }
            return null;
        }

        static bool FixBootstrapHeadAnchor(GameObject handsOrigin, Transform headAnchor)
        {
            var bootstrap = handsOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap == null)
                return false;

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("headAnchor");
            if (prop.objectReferenceValue == headAnchor)
                return false;

            prop.objectReferenceValue = headAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[AvianVoice] AvianAppBootstrap.headAnchor -> Camera Offset on Hands rig.");
            return true;
        }
    }
}
#endif
