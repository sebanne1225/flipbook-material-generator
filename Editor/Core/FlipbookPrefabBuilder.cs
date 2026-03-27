using System;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookPrefabBuilder
    {
        private const string MAMenuInstallerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller, nadena.dev.modular_avatar.core";
        private const string MAObjectToggleTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle, nadena.dev.modular_avatar.core";

        internal static void Build(Material material, string outputFolderPath, string baseName)
        {
            var prefabName = $"{baseName}_Flipbook";
            var root = new GameObject(prefabName);

            try
            {
                // Child Quad with material
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Quad";
                quad.transform.SetParent(root.transform, false);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                // Remove collider (Quad comes with MeshCollider by default)
                var collider = quad.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);

                // MA optional integration
                TryAttachModularAvatar(root);

                // Save prefab
                var prefabPath = $"{outputFolderPath}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

                FlipbookGeneratorLog.Info($"Prefab saved: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void TryAttachModularAvatar(GameObject root)
        {
            var menuInstallerType = Type.GetType(MAMenuInstallerTypeName);
            var objectToggleType = Type.GetType(MAObjectToggleTypeName);

            if (menuInstallerType == null || objectToggleType == null)
            {
                FlipbookGeneratorLog.Info("Modular Avatar not detected. Skipping MA components.");
                return;
            }

            root.AddComponent(menuInstallerType);
            root.AddComponent(objectToggleType);

            FlipbookGeneratorLog.Info("Modular Avatar detected. MenuInstaller and ObjectToggle attached.");
        }
    }
}
