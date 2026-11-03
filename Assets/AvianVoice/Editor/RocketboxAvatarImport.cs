#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Import settings for the Microsoft Rocketbox avatars under Resources/Avatars/Rocketbox:
    /// Humanoid rig (needed for body tracking retargeting), URP-friendly materials, normal maps.
    /// Based on the MIT-licensed FixRocketboxMaxImport.cs from the Microsoft Rocketbox repository.
    /// </summary>
    public class RocketboxAvatarImport : AssetPostprocessor
    {
        public const string RootFolder = "Assets/AvianVoice/Resources/Avatars/Rocketbox/";

        bool Applies => assetPath.Replace('\\', '/').StartsWith(RootFolder);

        void OnPreprocessModel()
        {
            if (!Applies)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.RecursiveUp);
        }

        void OnPreprocessTexture()
        {
            if (!Applies)
                return;

            var importer = (TextureImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (name.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
            }
            else if (name.Contains("opacity"))
            {
                importer.alphaIsTransparency = true;
            }

            importer.maxTextureSize = 2048;
        }

        void OnPostprocessMaterial(Material material)
        {
            if (!Applies || material == null)
                return;

            // 3ds Max exports assume the texture carries the colour; Unity multiplies by the flat colour.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.3f);

            var textureFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') + "/Textures/";
            var baseName = material.name.ToLowerInvariant();

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null)
            {
                var color = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + baseName + "_color.tga");
                if (color != null)
                    material.SetTexture("_BaseMap", color);
            }

            if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") == null)
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + baseName + "_normal.tga");
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }

            if (baseName.Contains("opacity"))
            {
                // Hair / eyelashes: alpha-clipped, double-sided, instead of a glassy transparent surface.
                if (material.HasProperty("_Surface"))
                    material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_AlphaClip"))
                    material.SetFloat("_AlphaClip", 1f);
                if (material.HasProperty("_Cutoff"))
                    material.SetFloat("_Cutoff", 0.4f);
                if (material.HasProperty("_Cull"))
                    material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
        }
    }
}
#endif
