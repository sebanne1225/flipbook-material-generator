using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookAnimatorBuilder
    {
        internal static AnimatorController Build(
            AnimationClip[] clips,
            string outputPath,
            PlaybackMode playbackMode = PlaybackMode.Loop)
        {
            // 1. Delete existing file to prevent stale asset reuse
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
            }

            // 2. Create controller file (rootStateMachine is auto-registered)
            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            // Rename layer
            var layers = controller.layers;
            layers[0].name = "Flipbook";
            controller.layers = layers;

            var rootStateMachine = controller.layers[0].stateMachine;

            // 3. Parameters
            controller.AddParameter(FlipbookConstants.ToggleParameterName, AnimatorControllerParameterType.Bool);
            if (playbackMode == PlaybackMode.ManualReset)
                controller.AddParameter(FlipbookConstants.ResetParameterName, AnimatorControllerParameterType.Bool);

            // 4. Idle state (both modes): all Pages off, default state
            var idleClip = new AnimationClip { frameRate = 1f };
            for (var p = 0; p < clips.Length; p++)
            {
                var pagePath = FlipbookConstants.PagePath(p);
                var offCurve = new AnimationCurve(new Keyframe(0f, 0f));
                idleClip.SetCurve(pagePath, typeof(GameObject), FlipbookConstants.GameObjectActiveProperty, offCurve);
            }
            var idleSettings = AnimationUtility.GetAnimationClipSettings(idleClip);
            idleSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(idleClip, idleSettings);

            var idleClipDir = System.IO.Path.GetDirectoryName(outputPath).Replace('\\', '/');
            var idleClipPath = $"{idleClipDir}/Idle.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(idleClipPath) != null)
            {
                AssetDatabase.DeleteAsset(idleClipPath);
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateAsset(idleClip, idleClipPath);
            AssetDatabase.ImportAsset(idleClipPath, ImportAssetOptions.ForceUpdate);

            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            idleState.writeDefaultValues = true;
            rootStateMachine.defaultState = idleState;

            // 5. Page states and sequential transitions
            AnimatorState previousState = null;
            AnimatorState page1State = null;

            for (var i = 0; i < clips.Length; i++)
            {
                var state = rootStateMachine.AddState($"Page{i + 1}");
                state.motion = clips[i];
                state.writeDefaultValues = true;

                if (i == 0)
                    page1State = state;

                if (previousState != null)
                {
                    var transition = previousState.AddTransition(state);
                    transition.hasExitTime = true;
                    transition.exitTime = 1f;
                    transition.duration = 0f;
                    transition.hasFixedDuration = true;
                }

                previousState = state;
            }

            // Loop: last page -> first page
            if (clips.Length > 1 && previousState != null)
            {
                var loopTransition = previousState.AddTransition(page1State);
                loopTransition.hasExitTime = true;
                loopTransition.exitTime = 1f;
                loopTransition.duration = 0f;
                loopTransition.hasFixedDuration = true;
            }
            else if (clips.Length == 1 && previousState != null)
            {
                var selfTransition = previousState.AddTransition(previousState);
                selfTransition.hasExitTime = true;
                selfTransition.exitTime = 1f;
                selfTransition.duration = 0f;
                selfTransition.hasFixedDuration = true;
            }

            // 6. Toggle transitions (both modes)
            // Idle → Page1: FlipbookToggle = true
            var idleToPage1 = idleState.AddTransition(page1State);
            idleToPage1.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.ToggleParameterName);
            idleToPage1.hasExitTime = false;
            idleToPage1.duration = 0f;
            idleToPage1.hasFixedDuration = true;

            // 7. ManualReset: AnyState → Page1 on FlipbookReset = true
            if (playbackMode == PlaybackMode.ManualReset && page1State != null)
            {
                var anyToPage1 = rootStateMachine.AddAnyStateTransition(page1State);
                anyToPage1.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.ResetParameterName);
                anyToPage1.hasExitTime = false;
                anyToPage1.duration = 0f;
                anyToPage1.hasFixedDuration = true;
                anyToPage1.canTransitionToSelf = false;
            }

            // 4. Persist
            EditorUtility.SetDirty(rootStateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            FlipbookGeneratorLog.Info(
                $"AnimatorController saved: {outputPath} ({clips.Length} state(s))");

            FlipbookGeneratorLog.Info(
                $"AnimatorBuilder return: controller={controller}, " +
                $"states={controller?.layers[0].stateMachine.states.Length}");

            return controller;
        }
    }
}
