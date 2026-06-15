#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fine3D.Editor
{
    /// <summary>
    /// Converts Fine3D Built-in tree/vegetation materials to URP Lit.
    /// The asset pack embeds Nature/Tree shaders inside prefabs, which Render Pipeline Converter skips.
    /// </summary>
    public static class Fine3dUrpMaterialFixer
    {
        const string SummerRoot = "Assets/Fine3D/Environment_Pond/Summer";

        [MenuItem("Fine3D/Fix URP Materials (Trees & Vegetation)")]
        public static void FixSummerMaterials()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[Fine3D] URP Lit shader not found. Is Universal RP installed and active?");
                return;
            }

            var converted = new HashSet<Material>();
            var count = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { SummerRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && TryConvert(material, urpLit))
                {
                    EditorUtility.SetDirty(material);
                    converted.Add(material);
                    count++;
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SummerRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                var prefabChanged = false;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        var material = materials[i];
                        if (material == null || converted.Contains(material))
                            continue;

                        if (TryConvert(material, urpLit))
                        {
                            EditorUtility.SetDirty(material);
                            converted.Add(material);
                            count++;
                            prefabChanged = true;
                        }
                    }

                    if (prefabChanged)
                        EditorUtility.SetDirty(renderer);
                }

                if (prefabChanged)
                    PrefabUtility.SavePrefabAsset(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Fine3D] Converted {count} material(s) under {SummerRoot}. Re-open the scene to verify trees.");
        }

        static bool TryConvert(Material material, Shader urpLit)
        {
            if (material.shader == null)
                return false;

            var shaderName = material.shader.name;
            if (!IsBuiltInVegetationShader(shaderName))
                return false;

            var mainTex = material.GetTexture("_MainTex");
            var baseColor = material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;
            var cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
            var isLeaf = shaderName.Contains("Leaves");

            material.shader = urpLit;
            material.SetTexture("_BaseMap", mainTex);
            material.SetColor("_BaseColor", baseColor);

            if (isLeaf)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", cutoff);
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_Cull", 2f);
                material.DisableKeyword("_ALPHATEST_ON");
            }

            return true;
        }

        static bool IsBuiltInVegetationShader(string shaderName)
        {
            return shaderName.Contains("Nature/Tree Soft Occlusion Bark")
                || shaderName.Contains("Nature/Tree Soft Occlusion Leaves")
                || shaderName.Contains("Nature/Tree Creator Bark")
                || shaderName.Contains("Nature/Tree Creator Leaves");
        }
    }
}
#endif
