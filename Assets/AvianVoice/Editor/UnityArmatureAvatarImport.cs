#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Import settings for the Unity "Starter Assets" Armature robot under Resources/Avatars/UnityArmature
    /// (the faceless mannequin Meta ships as the stylized character of its body-tracking samples; Unity
    /// Companion License, see LICENSE.md next to the model): Humanoid rig from the model's own bone names
    /// (the .meta carries the full finger mapping), readable mesh (the first-person head hider splits the
    /// head geometry at runtime), and matte URP materials with the albedo textures next to the model.
    /// </summary>
    public class UnityArmatureAvatarImport : AssetPostprocessor
    {
        public const string RootFolder = "Assets/AvianVoice/Resources/Avatars/UnityArmature/";

        bool Applies => assetPath.Replace('\\', '/').StartsWith(RootFolder);

        void OnPreprocessModel()
        {
            if (!Applies)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessTexture()
        {
            if (!Applies)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
        }

        void OnPostprocessModel(GameObject model)
        {
            if (!Applies || model == null)
                return;

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? RootFolder.TrimEnd('/');
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                    FixMaterial(material, folder);
            }
        }

        /// <summary>M_Arms / M_Body / M_Legs get Armature_Arms / _Body / _Legs _AlbedoTransparency.png; matte finish.</summary>
        static void FixMaterial(Material material, string folder)
        {
            if (material == null)
                return;

            var part = material.name.StartsWith("M_") ? material.name.Substring(2) : material.name;
            var texturePath = folder + "/Armature_" + part + "_AlbedoTransparency.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.35f);
        }
    }
}
#endif
