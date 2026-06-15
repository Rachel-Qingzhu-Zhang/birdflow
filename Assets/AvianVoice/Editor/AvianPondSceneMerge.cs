#if UNITY_EDITOR
using System.Collections.Generic;
using AvianVoice.Embodiment;
using Fine3D.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Editor
{
    public static class AvianPondSceneMerge
    {
        const string DemoScenePath = "Assets/Fine3D/Environment_Pond/Summer/Scenes/scene_demo_pond_day.unity";
        const string LabScenePath = "Assets/AvianVoice/Scenes/Avian_Lab.unity";
        const string PondWaterMaterialPath = "Assets/AvianVoice/Materials/PondWater.mat";
        const string BumpMapPath =
            "Assets/Fine3D/Environment_Pond/Summer/Materials_Textures/Surface_detail/tile_water_surface_NM_01.png";

        public const string PondRootName = "AvianPond";
        static readonly Vector3 DefaultPondOffset = new(80f, 0f, 60f);

        [MenuItem("Avian Voice/Scene Setup/Merge Fine3D Pond into Lab")]
        public static void MergePondIntoLab()
        {
            var labScene = SceneManager.GetActiveScene();
            if (!labScene.name.StartsWith("Avian"))
            {
                Debug.LogWarning("[AvianVoice] Open Avian_Lab first, then run Merge Fine3D Pond into Lab.");
                return;
            }

            MergePondIntoActiveScene(labScene);
        }

        public static void MergePondIntoLabBatch()
        {
            var labScene = EditorSceneManager.OpenScene(LabScenePath);
            MergePondIntoActiveScene(labScene);
            EditorSceneManager.SaveScene(labScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AvianVoice] Batch pond merge complete for " + LabScenePath);
        }

        static void MergePondIntoActiveScene(Scene labScene)
        {
            if (!System.IO.File.Exists(DemoScenePath))
            {
                Debug.LogError("[AvianVoice] Demo pond scene not found at " + DemoScenePath);
                return;
            }

            var existing = GameObject.Find(PondRootName);
            if (existing != null)
            {
                if (!Application.isBatchMode &&
                    !EditorUtility.DisplayDialog(
                        "Replace AvianPond?",
                        "An AvianPond root already exists. Replace it with a fresh merge?",
                        "Replace",
                        "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(existing);
            }

            var pondRoot = new GameObject(PondRootName);
            Undo.RegisterCreatedObjectUndo(pondRoot, "Create AvianPond");
            pondRoot.transform.position = DefaultPondOffset;

            var demoScene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in demoScene.GetRootGameObjects())
                {
                    switch (root.name)
                    {
                        case "Scene_misc":
                            CopySceneMisc(root.transform, pondRoot.transform);
                            break;
                        case "Vegetation":
                        case "Wooden":
                            CopyRootHierarchy(root, pondRoot.transform);
                            break;
                        case "Particles":
                            CopyWaterRipples(root.transform, pondRoot.transform);
                            break;
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(demoScene, true);
            }

            Fine3dUrpMaterialFixer.FixSummerMaterials();
            ConfigureTerrains(pondRoot.transform);
            EnsurePondWater(pondRoot.transform);
            EnsureFishSpawner(pondRoot.transform);
            ConfigureFlyingStartPose(pondRoot.transform);

            EditorSceneManager.MarkSceneDirty(labScene);
            Debug.Log(
                "[AvianVoice] Fine3D pond merged into " + labScene.name +
                " at " + pondRoot.transform.position +
                ". Save scene (Ctrl+S), then Play task 3 (Flying) to see reflections.");
        }

        static void CopyRootHierarchy(GameObject sourceRoot, Transform parent)
        {
            var copy = Object.Instantiate(sourceRoot, parent);
            copy.name = sourceRoot.name;
            Undo.RegisterCreatedObjectUndo(copy, "Copy " + sourceRoot.name);
            copy.transform.localPosition = sourceRoot.transform.localPosition;
            copy.transform.localRotation = sourceRoot.transform.localRotation;
            copy.transform.localScale = sourceRoot.transform.localScale;
        }

        static void CopySceneMisc(Transform sourceRoot, Transform parent)
        {
            var misc = new GameObject("Scene_misc");
            Undo.RegisterCreatedObjectUndo(misc, "Create Scene_misc");
            misc.transform.SetParent(parent, false);

            foreach (Transform child in sourceRoot)
            {
                if (ShouldSkipSceneMiscChild(child.name))
                    continue;

                var copy = Object.Instantiate(child.gameObject, misc.transform);
                copy.name = child.name;
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;
            }
        }

        static bool ShouldSkipSceneMiscChild(string childName)
        {
            if (childName.Contains("Refelection") || childName.Contains("Reflection Probe"))
                return true;
            if (childName == "Timeline" || childName == "Light_Sun_Directional")
                return true;
            return false;
        }

        static void CopyWaterRipples(Transform sourceRoot, Transform parent)
        {
            var particles = new GameObject("Particles");
            Undo.RegisterCreatedObjectUndo(particles, "Create Particles");
            particles.transform.SetParent(parent, false);

            foreach (Transform child in sourceRoot)
            {
                if (!child.name.Contains("WaterRipples"))
                    continue;

                var copy = Object.Instantiate(child.gameObject, particles.transform);
                copy.name = child.name;
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;
            }
        }

        static void ConfigureTerrains(Transform pondRoot)
        {
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader == null)
                return;

            foreach (var terrain in pondRoot.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.materialTemplate == null ||
                    terrain.materialTemplate.shader.name.Contains("Nature/Terrain"))
                {
                    terrain.materialTemplate = new Material(terrainShader);
                    EditorUtility.SetDirty(terrain);
                }
            }
        }

        static void EnsurePondWater(Transform pondRoot)
        {
            var existing = pondRoot.Find(PondWaterReflection.WaterObjectName);
            GameObject water;
            if (existing != null)
            {
                water = existing.gameObject;
            }
            else
            {
                water = new GameObject(PondWaterReflection.WaterObjectName);
                Undo.RegisterCreatedObjectUndo(water, "Create PondWater");
                water.transform.SetParent(pondRoot, false);
                water.transform.localPosition = new Vector3(0f, 1.7f, 0f);
                water.transform.localRotation = Quaternion.identity;
                water.transform.localScale = Vector3.one;
            }

            water.transform.localScale = Vector3.one;

            if (water.GetComponent<MeshFilter>() == null)
                water.AddComponent<MeshFilter>();
            if (water.GetComponent<MeshRenderer>() == null)
                water.AddComponent<MeshRenderer>();

            var collider = water.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            var surface = water.GetComponent<PondWaterSurface>();
            if (surface == null)
                surface = Undo.AddComponent<PondWaterSurface>(water);
            surface.RebuildMesh();

            var material = EnsurePondWaterMaterial();
            var reflection = water.GetComponent<PondWaterReflection>();
            if (reflection == null)
                reflection = Undo.AddComponent<PondWaterReflection>(water);

            var renderer = water.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;

            var so = new SerializedObject(reflection);
            so.FindProperty("waterMaterial").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Avian Voice/Scene Setup/Upgrade Pond Water Surface (Open Scene)")]
        public static void UpgradePondWaterInOpenScene()
        {
            var pondRoot = GameObject.Find(PondRootName);
            if (pondRoot == null)
            {
                Debug.LogWarning("[AvianVoice] No AvianPond in scene. Run Merge Fine3D Pond into Lab first.");
                return;
            }

            EnsurePondWater(pondRoot.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AvianVoice] Pond water upgraded to organic mesh + ripple shader. Save scene (Ctrl+S).");
        }

        static Material EnsurePondWaterMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PondWaterMaterialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find("AvianVoice/PondWater");
            if (shader == null)
            {
                Debug.LogWarning("[AvianVoice] AvianVoice/PondWater shader not found. Reimport shaders, then re-run merge.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder("Assets/AvianVoice/Materials"))
                AssetDatabase.CreateFolder("Assets/AvianVoice", "Materials");

            var material = new Material(shader);
            var bump = AssetDatabase.LoadAssetAtPath<Texture2D>(BumpMapPath);
            if (bump != null && material.HasProperty("_BumpMap"))
                material.SetTexture("_BumpMap", bump);

            AssetDatabase.CreateAsset(material, PondWaterMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void EnsureFishSpawner(Transform pondRoot)
        {
            var fishRoot = pondRoot.Find("Fish");
            if (fishRoot == null)
            {
                var fishGo = new GameObject("Fish");
                Undo.RegisterCreatedObjectUndo(fishGo, "Create Fish root");
                fishGo.transform.SetParent(pondRoot, false);
                fishRoot = fishGo.transform;
            }

            PondFishSpawner spawner = fishRoot.GetComponent<PondFishSpawner>();
            if (spawner == null)
                spawner = Undo.AddComponent<PondFishSpawner>(fishRoot.gameObject);

            var fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PondFishSpawner.DefaultFishModelPath);
            if (fishPrefab != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("fishPrefab").objectReferenceValue = fishPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureFlyingStartPose(Transform pondRoot)
        {
            var xrOrigin = FindPreferredXrOrigin(SceneManager.GetActiveScene());
            if (xrOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] No XR Origin found; skipping flying pose setup.");
                return;
            }

            if (xrOrigin.GetComponent<XrRigTaskStartPoses>() == null)
                Undo.AddComponent<XrRigTaskStartPoses>(xrOrigin);

            var poses = xrOrigin.GetComponent<XrRigTaskStartPoses>();
            var rigPosition = pondRoot.position + new Vector3(0f, 6f, -4f);
            var lookTarget = pondRoot.position + new Vector3(0f, 1.7f, 2f);
            var eye = rigPosition + Vector3.up * AvianLabSceneLayout.EyeHeightMeters;
            var forward = lookTarget - eye;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = pondRoot.forward;
            forward.Normalize();
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            Undo.RecordObject(poses, "Configure flying pond pose");
            poses.SetTaskPose("flying", rigPosition, rotation);
            EditorUtility.SetDirty(poses);
        }

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
    }
}
#endif
