using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookAnimatorBuilder
    {
        internal static AnimatorController Build(
            AnimationClip[] clips,
            string outputPath)
        {
            // 1. Delete existing file to prevent stale asset reuse
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
            }

            // 2. Create controller file (rootStateMachine is auto-registered)
            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            // 3. Add states and transitions
            // Note: AddState/AddTransition register sub-assets internally; no manual AddObjectToAsset needed
            AnimatorState previousState = null;

            for (var i = 0; i < clips.Length; i++)
            {
                var state = rootStateMachine.AddState($"Page{i + 1}");
                state.motion = clips[i];
                state.writeDefaultValues = true;

                if (i == 0)
                {
                    rootStateMachine.defaultState = state;
                }

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
                var loopTransition = previousState.AddTransition(rootStateMachine.defaultState);
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
