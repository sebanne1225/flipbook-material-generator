using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookPrefabBuilder
    {
        private const string MAMenuInstallerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MAObjectToggleTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle";
        private const string MAMergeAnimatorTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator";
        private const string MAMenuItemTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName))
                .FirstOrDefault(t => t != null);
        }

        internal static void Build(Material material, string outputFolderPath, string baseName,
            bool addObjectToggle = false, string toggleName = "Flipbook", bool useMergeAnimator = true)
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
                if (addObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, toggleName);
                else if (useMergeAnimator)
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

        internal static void BuildMultiPage(
            Material[] materials,
            AnimatorController controller,
            string outputFolderPath,
            string baseName,
            bool addObjectToggle = false,
            string toggleName = "Flipbook",
            bool useMergeAnimator = true)
        {
            var prefabName = $"{baseName}_FlipbookMultiPage";
            var root = new GameObject(prefabName);

            try
            {
                // Per-page child: Quad + Material
                for (var i = 0; i < materials.Length; i++)
                {
                    var pageObj = new GameObject($"Page{i + 1}");
                    pageObj.transform.SetParent(root.transform, false);

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Quad";
                    quad.transform.SetParent(pageObj.transform, false);

                    var renderer = quad.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[i];
                    renderer.enabled = false; // Animator enables only the active page via m_Enabled keyframe

                    var collider = quad.GetComponent<Collider>();
                    if (collider != null)
                        UnityEngine.Object.DestroyImmediate(collider);
                }

                // Animator on root
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                // MA optional integration
                if (addObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, toggleName);
                else if (useMergeAnimator)
                    TryAttachModularAvatar(root);
                if (useMergeAnimator)
                    TryAttachMergeAnimator(root, controller);

                // Save prefab
                var prefabPath = $"{outputFolderPath}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

                FlipbookGeneratorLog.Info($"MultiPage Prefab saved: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void TryAttachObjectToggleAndMenuItem(GameObject root, string toggleName)
        {
            var objectToggleType = FindType(MAObjectToggleTypeName);
            var menuItemType = FindType(MAMenuItemTypeName);

            if (objectToggleType == null || menuItemType == null)
            {
                FlipbookGeneratorLog.Info("MA ObjectToggle or MenuItem type not found. Skipping Object Toggle setup.");
                return;
            }

            root.AddComponent(objectToggleType);

            var menuItemComp = root.AddComponent(menuItemType);

            var flags = BindingFlags.Public | BindingFlags.Instance;
            var controlField = menuItemType.GetField("Control", flags);
            if (controlField != null)
            {
                var control = controlField.GetValue(menuItemComp);
                var controlType = controlField.FieldType;

                var nameField = controlType.GetField("name", flags);
                nameField?.SetValue(control, toggleName);

                var typeField = controlType.GetField("type", flags);
                if (typeField != null)
                {
                    // VRCExpressionsMenu.Control.ControlType.Toggle = 2
                    var toggleValue = Enum.ToObject(typeField.FieldType, 2);
                    typeField.SetValue(control, toggleValue);
                }

                controlField.SetValue(menuItemComp, control);
            }

            // Default OFF: prefab starts inactive
            root.SetActive(false);

            FlipbookGeneratorLog.Info($"MA ObjectToggle and MenuItem attached (toggle: '{toggleName}', default: OFF).");
        }

        private static void TryAttachModularAvatar(GameObject root)
        {
            var menuInstallerType = FindType(MAMenuInstallerTypeName);
            var objectToggleType = FindType(MAObjectToggleTypeName);

            if (menuInstallerType == null || objectToggleType == null)
            {
                FlipbookGeneratorLog.Info("Modular Avatar not detected. Skipping MA components.");
                return;
            }

            root.AddComponent(menuInstallerType);
            root.AddComponent(objectToggleType);

            FlipbookGeneratorLog.Info("Modular Avatar detected. MenuInstaller and ObjectToggle attached.");
        }

        private static void TryAttachMergeAnimator(GameObject root, AnimatorController controller)
        {
            var mergeAnimatorType = FindType(MAMergeAnimatorTypeName);
            if (mergeAnimatorType == null)
            {
                FlipbookGeneratorLog.Info("MA MergeAnimator not detected. Skipping.");
                return;
            }

            var component = root.AddComponent(mergeAnimatorType);
            var flags = BindingFlags.Public | BindingFlags.Instance;

            // animator field
            var animatorField = mergeAnimatorType.GetField("animator", flags);
            if (animatorField != null)
            {
                animatorField.SetValue(component, controller);
            }

            // layerType field — enum value for FX
            var layerTypeField = mergeAnimatorType.GetField("layerType", flags);
            if (layerTypeField != null)
            {
                // VRCAvatarDescriptor.AnimLayerType.FX = 5
                var fxValue = Enum.ToObject(layerTypeField.FieldType, 5);
                layerTypeField.SetValue(component, fxValue);
            }

            // pathMode field — Relative
            var pathModeField = mergeAnimatorType.GetField("pathMode", flags);
            if (pathModeField != null)
            {
                // MergeAnimatorPathMode.Relative = 0
                var relativeValue = Enum.ToObject(pathModeField.FieldType, 0);
                pathModeField.SetValue(component, relativeValue);
            }

            // deleteAttachedAnimator field — Prefab上のAnimatorを削除させる
            var deleteField = mergeAnimatorType.GetField("deleteAttachedAnimator", flags);
            if (deleteField != null)
            {
                deleteField.SetValue(component, true);
            }

            // matchAvatarWriteDefaults field
            var wdField = mergeAnimatorType.GetField("matchAvatarWriteDefaults", flags);
            if (wdField != null)
            {
                wdField.SetValue(component, true);
            }

            FlipbookGeneratorLog.Info(
                "MA MergeAnimator attached (layerType=FX, pathMode=Relative, deleteAttachedAnimator=true).");
        }
    }
}
