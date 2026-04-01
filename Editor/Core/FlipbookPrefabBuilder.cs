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
            bool enableMenu = true,
            bool enableAudioSource = false, AudioClip audioClip = null)
        {
            var prefabName = $"{baseName}_Flipbook";
            var root = new GameObject(prefabName);

            try
            {
                // Child Quad with material
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = FlipbookConstants.QuadObjectName;
                quad.transform.SetParent(root.transform, false);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                // Remove collider (Quad comes with MeshCollider by default)
                var collider = quad.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);

                quad.SetActive(false);

                // Audio container (optional)
                GameObject audioObj = null;
                if (enableAudioSource && audioClip != null)
                {
                    audioObj = new GameObject(FlipbookConstants.AudioObjectName);
                    audioObj.transform.SetParent(root.transform, false);
                    var source = audioObj.AddComponent<AudioSource>();
                    source.clip = audioClip;
                    source.playOnAwake = true;
                    source.loop = true;
                    audioObj.SetActive(false);
                }

                // MA optional integration
                if (enableObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, quad, toggleName, PlaybackMode.Loop, enableMenu, audioObj);

                // Ensure child order: [MA Menu(0)] → [Audio(1)] → Quad
                if (audioObj != null)
                    audioObj.transform.SetSiblingIndex(1);
                quad.transform.SetSiblingIndex(audioObj != null ? 2 : 1);

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
            bool enableAudioSource = false,
            AudioClip audioClip = null,
            PlaybackMode playbackMode = PlaybackMode.Loop)
        {
            var prefabName = $"{baseName}_FlipbookMultiPage";
            var root = new GameObject(prefabName);

            try
            {
                // Pages container
                var pagesObj = new GameObject(FlipbookConstants.PagesObjectName);
                pagesObj.transform.SetParent(root.transform, false);
                pagesObj.SetActive(false);

                // Per-page child: Quad + Material
                for (var i = 0; i < materials.Length; i++)
                {
                    var pageObj = new GameObject(FlipbookConstants.PageObjectName(i));
                    pageObj.transform.SetParent(pagesObj.transform, false);

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = FlipbookConstants.QuadObjectName;
                    quad.transform.SetParent(pageObj.transform, false);

                    var renderer = quad.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[i];
                    renderer.enabled = false; // Animator enables only the active page via m_Enabled keyframe

                    var collider = quad.GetComponent<Collider>();
                    if (collider != null)
                        UnityEngine.Object.DestroyImmediate(collider);
                }

                // Audio container (optional)
                GameObject audioObj = null;
                if (enableAudioSource && audioClip != null)
                {
                    audioObj = new GameObject(FlipbookConstants.AudioObjectName);
                    audioObj.transform.SetParent(root.transform, false);
                    var source = audioObj.AddComponent<AudioSource>();
                    source.clip = audioClip;
                    source.playOnAwake = true;
                    source.loop = true;
                    audioObj.SetActive(false);
                }

                // Animator on root
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                // MA optional integration
                if (enableObjectToggle)
                    TryAttachObjectToggleAndMenuItem(root, pagesObj, toggleName, playbackMode, enableMenu, audioObj);
                else if (enableMergeAnimator)
                    TryAttachModularAvatar(root);
                if (enableMergeAnimator)
                    TryAttachMergeAnimator(root, controller);

                // Ensure child order: MA Menu(0) → Audio(1) → Pages(2)
                if (audioObj != null)
                    audioObj.transform.SetSiblingIndex(1);
                pagesObj.transform.SetSiblingIndex(audioObj != null ? 2 : 1);

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

        private static void TryAttachObjectToggleAndMenuItem(GameObject root, GameObject toggleTarget, string toggleName, PlaybackMode playbackMode, bool enableMenu = true, GameObject audioObj = null)
        {
            var objectToggleType = FindType(MAObjectToggleTypeName);
            var menuItemType = FindType(MAMenuItemTypeName);
            var menuInstallerType = FindType(MAMenuInstallerTypeName);

            if (objectToggleType == null)
            {
                FlipbookGeneratorLog.Info("MA ObjectToggle type not found. Skipping Object Toggle setup.");
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance;

            // Determine which GameObject hosts the ObjectToggle component
            GameObject toggleHost;

            if (enableMenu && menuItemType != null && menuInstallerType != null)
            {
                try
                {
                // --- MA Menu hierarchy (first child of root) ---
                var maMenu = new GameObject("MA Menu");
                maMenu.transform.SetParent(root.transform, false);
                maMenu.transform.SetSiblingIndex(0);

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
                        typeField.SetValue(control, Enum.Parse(typeField.FieldType, "SubMenu"));
                    maControlField.SetValue(maMenuItemComp, control);
                }
                menuItemType.GetField("label", flags)?.SetValue(maMenuItemComp, "Flipbook");
                var menuSourceField = menuItemType.GetField("MenuSource", flags);
                if (menuSourceField != null)
                    menuSourceField.SetValue(maMenuItemComp, Enum.Parse(menuSourceField.FieldType, "Children"));

                // Toggle child — hosts ObjectToggle + MA MenuItem (Toggle)
                var toggleObj = new GameObject("Toggle");
                toggleObj.transform.SetParent(maMenu.transform, false);
                toggleHost = toggleObj;

                // MA MenuItem (Toggle)
                var menuItemComp = toggleObj.AddComponent(menuItemType);
                var controlField = menuItemType.GetField("Control", flags);
                if (controlField != null)
                {
                    var control = controlField.GetValue(menuItemComp);
                    if (control == null)
                        control = Activator.CreateInstance(controlField.FieldType);
                    var typeField = controlField.FieldType.GetField("type", flags);
                    if (typeField != null)
                        typeField.SetValue(control, Enum.Parse(typeField.FieldType, "Toggle"));
                    var valueField = controlField.FieldType.GetField("value", flags);
                    valueField?.SetValue(control, 1f);
                    var paramField = controlField.FieldType.GetField("parameter", flags);
                    if (paramField != null)
                    {
                        var paramInst = Activator.CreateInstance(paramField.FieldType);
                        paramField.FieldType.GetField("name", flags)?.SetValue(paramInst, FlipbookConstants.ToggleParameterName);
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
                            typeField.SetValue(control, Enum.Parse(typeField.FieldType, "Button"));
                        var valueField = resetControlField.FieldType.GetField("value", flags);
                        valueField?.SetValue(control, 1f);
                        var paramField = resetControlField.FieldType.GetField("parameter", flags);
                        if (paramField != null)
                        {
                            var paramInst = Activator.CreateInstance(paramField.FieldType);
                            paramField.FieldType.GetField("name", flags)?.SetValue(paramInst, FlipbookConstants.ResetParameterName);
                            paramField.SetValue(control, paramInst);
                        }
                        resetControlField.SetValue(resetMenuItemComp, control);
                    }
                    menuItemType.GetField("label", flags)?.SetValue(resetMenuItemComp, "Reset");
                }
                }
                catch (ArgumentException e)
                {
                    FlipbookGeneratorLog.Error(
                        $"MA または VRC SDK のバージョンが対応していない可能性があります: {e.Message}");
                    return;
                }
            }
            else
            {
                // No menu — attach ObjectToggle directly to root
                toggleHost = root;
            }

            // --- ObjectToggle (always created on toggleHost) ---
            var toggleComp = toggleHost.AddComponent(objectToggleType);
            var objectsProp = objectToggleType.GetProperty("Objects", flags);
            var objects = objectsProp?.GetValue(toggleComp);
            if (objects != null)
            {
                var listType = objects.GetType();
                var elemType = listType.GetGenericArguments()[0]; // ToggledObject
                var objRefType = FindType("nadena.dev.modular_avatar.core.AvatarObjectReference");
                var nonPublicFlags = BindingFlags.NonPublic | BindingFlags.Instance;

                var toggledObj = Activator.CreateInstance(elemType);
                if (objRefType != null)
                {
                    var objRefInst = Activator.CreateInstance(objRefType);
                    objRefType.GetField("referencePath", flags)?.SetValue(objRefInst, toggleTarget.name);
                    objRefType.GetField("targetObject", nonPublicFlags)?.SetValue(objRefInst, toggleTarget);
                    elemType.GetField("Object", flags)?.SetValue(toggledObj, objRefInst);
                }
                elemType.GetField("Active", flags)?.SetValue(toggledObj, true);
                listType.GetMethod("Add")?.Invoke(objects, new[] { toggledObj });

                // Audio object → Active=true (optional)
                if (audioObj != null && objRefType != null)
                {
                    var audioToggled = Activator.CreateInstance(elemType);
                    var audioRef = Activator.CreateInstance(objRefType);
                    objRefType.GetField("referencePath", flags)?.SetValue(audioRef, audioObj.name);
                    objRefType.GetField("targetObject", nonPublicFlags)?.SetValue(audioRef, audioObj);
                    elemType.GetField("Object", flags)?.SetValue(audioToggled, audioRef);
                    elemType.GetField("Active", flags)?.SetValue(audioToggled, true);
                    listType.GetMethod("Add")?.Invoke(objects, new[] { audioToggled });
                }
            }

            if (enableMenu)
                FlipbookGeneratorLog.Info($"MA Menu + ObjectToggle attached (toggle: '{toggleName}', default: OFF).");
            else
                FlipbookGeneratorLog.Info($"MA ObjectToggle attached (target: '{toggleTarget.name}').");
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

            try
            {
                // layerType field — enum value for FX
                var layerTypeField = mergeAnimatorType.GetField("layerType", flags);
                if (layerTypeField != null)
                    layerTypeField.SetValue(component, Enum.Parse(layerTypeField.FieldType, "FX"));

                // pathMode field — Relative
                var pathModeField = mergeAnimatorType.GetField("pathMode", flags);
                if (pathModeField != null)
                    pathModeField.SetValue(component, Enum.Parse(pathModeField.FieldType, "Relative"));
            }
            catch (ArgumentException e)
            {
                FlipbookGeneratorLog.Error(
                    $"MA または VRC SDK のバージョンが対応していない可能性があります: {e.Message}");
                return;
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
