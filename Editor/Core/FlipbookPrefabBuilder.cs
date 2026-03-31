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
            bool enableObjectToggle = false, string toggleName = "Flipbook",
            bool enableMergeAnimator = true, bool enableMenu = true)
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
                if (enableObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, root, toggleName, PlaybackMode.Loop, enableMenu);
                else if (enableMergeAnimator)
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
            bool enableObjectToggle = false,
            string toggleName = "Flipbook",
            bool enableMergeAnimator = true,
            bool enableMenu = true,
            PlaybackMode playbackMode = PlaybackMode.Loop)
        {
            var prefabName = $"{baseName}_FlipbookMultiPage";
            var root = new GameObject(prefabName);

            try
            {
                // Pages container
                var pagesObj = new GameObject("Pages");
                pagesObj.transform.SetParent(root.transform, false);
                pagesObj.SetActive(false);

                // Per-page child: Quad + Material
                for (var i = 0; i < materials.Length; i++)
                {
                    var pageObj = new GameObject($"Page{i + 1}");
                    pageObj.transform.SetParent(pagesObj.transform, false);

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
                if (enableObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, pagesObj, toggleName, playbackMode, enableMenu);
                else if (enableMergeAnimator)
                    TryAttachModularAvatar(root);
                if (enableMergeAnimator)
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

        private static void TryAttachObjectToggleAndMenuItem(GameObject root, GameObject toggleTarget, string toggleName, PlaybackMode playbackMode, bool enableMenu = true)
        {
            var objectToggleType = FindType(MAObjectToggleTypeName);
            var menuItemType = FindType(MAMenuItemTypeName);
            var menuInstallerType = FindType(MAMenuInstallerTypeName);

            if (objectToggleType == null || menuItemType == null)
            {
                FlipbookGeneratorLog.Info("MA ObjectToggle or MenuItem type not found. Skipping Object Toggle setup.");
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance;

            if (!enableMenu)
            {
                FlipbookGeneratorLog.Info("Menu disabled. Skipping ObjectToggle and Menu generation.");
                return;
            }

            // MA Menu — first child of root (MenuInstaller + SubMenu MenuItem)
            var maMenu = new GameObject("MA Menu");
            maMenu.transform.SetParent(root.transform, false);
            maMenu.transform.SetSiblingIndex(0);

            if (menuInstallerType != null)
                maMenu.AddComponent(menuInstallerType);

            var maMenuItemComp = maMenu.AddComponent(menuItemType);
            var maControlField = menuItemType.GetField("Control", flags);
            if (maControlField != null)
            {
                var control = maControlField.GetValue(maMenuItemComp);
                if (control == null)
                    control = Activator.CreateInstance(maControlField.FieldType);
                var typeField = maControlField.FieldType.GetField("type", flags);
                if (typeField != null)
                    typeField.SetValue(control, Enum.ToObject(typeField.FieldType, 103)); // SubMenu = 103
                maControlField.SetValue(maMenuItemComp, control);
            }
            menuItemType.GetField("label", flags)?.SetValue(maMenuItemComp, "Flipbook");
            var menuSourceField = menuItemType.GetField("MenuSource", flags);
            if (menuSourceField != null)
                menuSourceField.SetValue(maMenuItemComp, Enum.ToObject(menuSourceField.FieldType, 1)); // SubmenuSource.Children = 1

            // Toggle child — MA ObjectToggle + MA MenuItem (Toggle)
            var toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(maMenu.transform, false);

            // ObjectToggle component — Objects list: toggleTarget → Active=true
            var toggleComp = toggleObj.AddComponent(objectToggleType);
            var objectsProp = objectToggleType.GetProperty("Objects", flags);
            var objects = objectsProp?.GetValue(toggleComp);
            if (objects != null)
            {
                var listType = objects.GetType();
                var elemType = listType.GetGenericArguments()[0]; // ToggledObject
                var toggledObj = Activator.CreateInstance(elemType);

                var objRefType = FindType("nadena.dev.modular_avatar.core.AvatarObjectReference");
                if (objRefType != null)
                {
                    var objRefInst = Activator.CreateInstance(objRefType);
                    objRefType.GetField("referencePath", flags)?.SetValue(objRefInst, "Pages");
                    var nonPublicFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                    objRefType.GetField("targetObject", nonPublicFlags)?.SetValue(objRefInst, toggleTarget);
                    elemType.GetField("Object", flags)?.SetValue(toggledObj, objRefInst);
                }
                elemType.GetField("Active", flags)?.SetValue(toggledObj, true);
                listType.GetMethod("Add")?.Invoke(objects, new[] { toggledObj });
            }

            var menuItemComp = toggleObj.AddComponent(menuItemType);
            var controlField = menuItemType.GetField("Control", flags);
            if (controlField != null)
            {
                var control = controlField.GetValue(menuItemComp);
                if (control == null)
                    control = Activator.CreateInstance(controlField.FieldType);
                var typeField = controlField.FieldType.GetField("type", flags);
                if (typeField != null)
                    typeField.SetValue(control, Enum.ToObject(typeField.FieldType, 102)); // Toggle = 102
                var valueField = controlField.FieldType.GetField("value", flags);
                valueField?.SetValue(control, 1f);
                var paramField = controlField.FieldType.GetField("parameter", flags);
                if (paramField != null)
                {
                    var paramInst = Activator.CreateInstance(paramField.FieldType);
                    paramField.FieldType.GetField("name", flags)?.SetValue(paramInst, "FlipbookToggle");
                    paramField.SetValue(control, paramInst);
                }
                controlField.SetValue(menuItemComp, control);
            }
            menuItemType.GetField("label", flags)?.SetValue(menuItemComp, toggleName);
            menuItemType.GetField("isDefault", flags)?.SetValue(menuItemComp, false); // default OFF

            // ManualReset: Reset child with Button MenuItem
            if (playbackMode == PlaybackMode.ManualReset)
            {
                var resetObj = new GameObject("Reset");
                resetObj.transform.SetParent(maMenu.transform, false);
                var resetMenuItemComp = resetObj.AddComponent(menuItemType);
                var resetControlField = menuItemType.GetField("Control", flags);
                if (resetControlField != null)
                {
                    var control = resetControlField.GetValue(resetMenuItemComp);
                    if (control == null)
                        control = Activator.CreateInstance(resetControlField.FieldType);
                    var typeField = resetControlField.FieldType.GetField("type", flags);
                    if (typeField != null)
                        typeField.SetValue(control, Enum.ToObject(typeField.FieldType, 101)); // Button = 101
                    var valueField = resetControlField.FieldType.GetField("value", flags);
                    valueField?.SetValue(control, 1f);
                    var paramField = resetControlField.FieldType.GetField("parameter", flags);
                    if (paramField != null)
                    {
                        var paramInst = Activator.CreateInstance(paramField.FieldType);
                        paramField.FieldType.GetField("name", flags)?.SetValue(paramInst, "FlipbookReset");
                        paramField.SetValue(control, paramInst);
                    }
                    resetControlField.SetValue(resetMenuItemComp, control);
                }
                menuItemType.GetField("label", flags)?.SetValue(resetMenuItemComp, "Reset");
            }

            FlipbookGeneratorLog.Info($"MA Menu + ObjectToggle attached (toggle: '{toggleName}', default: OFF).");
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
