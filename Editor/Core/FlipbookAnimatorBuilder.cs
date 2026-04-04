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
            AnimationClip resetClip = null)
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
            controller.AddParameter(FlipbookConstants.EnabledParameterName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FlipbookConstants.LoopParameterName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FlipbookConstants.ResetParameterName, AnimatorControllerParameterType.Bool);

            // Set Loop default to true
            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == FlipbookConstants.LoopParameterName)
                {
                    parameters[i].defaultBool = true;
                    break;
                }
            }
            controller.parameters = parameters;

            // 4. Idle state: all Pages off, default state
            var idleClip = new AnimationClip { frameRate = 1f };
            for (var p = 0; p < clips.Length; p++)
            {
                var pagePath = FlipbookConstants.PagePath(p);
                var offCurve = new AnimationCurve(new Keyframe(0f, 0f));
                idleClip.SetCurve(pagePath, typeof(GameObject), FlipbookConstants.GameObjectActiveProperty, offCurve);
            }
            // Audio GameObject OFF in Idle (m_IsActive=0)
            if (resetClip != null)
            {
                var audioOffCurve = new AnimationCurve(new Keyframe(0f, 0f));
                idleClip.SetCurve(FlipbookConstants.AudioObjectName, typeof(GameObject), FlipbookConstants.GameObjectActiveProperty, audioOffCurve);
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
            AnimatorState lastState = null;

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
            lastState = previousState;

            // 6. Loop transition: last page -> first page (conditional on Loop=true)
            if (clips.Length > 1 && lastState != null)
            {
                var loopTransition = lastState.AddTransition(page1State);
                loopTransition.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.LoopParameterName);
                loopTransition.hasExitTime = true;
                loopTransition.exitTime = 1f;
                loopTransition.duration = 0f;
                loopTransition.hasFixedDuration = true;
            }
            else if (clips.Length == 1 && lastState != null)
            {
                var selfTransition = lastState.AddTransition(lastState);
                selfTransition.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.LoopParameterName);
                selfTransition.hasExitTime = true;
                selfTransition.exitTime = 1f;
                selfTransition.duration = 0f;
                selfTransition.hasFixedDuration = true;
            }

            // 7. Idle → Page1: Enabled=true
            var idleToPage1 = idleState.AddTransition(page1State);
            idleToPage1.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.EnabledParameterName);
            idleToPage1.hasExitTime = false;
            idleToPage1.duration = 0f;
            idleToPage1.hasFixedDuration = true;

            // 8. Any State → Idle: Enabled=false
            var anyToIdle = rootStateMachine.AddAnyStateTransition(idleState);
            anyToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, FlipbookConstants.EnabledParameterName);
            anyToIdle.hasExitTime = false;
            anyToIdle.duration = 0f;
            anyToIdle.hasFixedDuration = true;
            anyToIdle.canTransitionToSelf = false;

            // 9. Any State → Reset target: Reset=true AND Enabled=true
            if (page1State != null)
            {
                AnimatorState resetTarget;

                if (resetClip != null)
                {
                    // ResetPage1 state with Audio OFF→ON
                    var resetState = rootStateMachine.AddState("ResetPage1");
                    resetState.motion = resetClip;
                    resetState.writeDefaultValues = true;

                    // ResetPage1 → Page2 (or self-loop if single page)
                    if (clips.Length > 1)
                    {
                        // Find Page2 state
                        AnimatorState page2State = null;
                        foreach (var cs in rootStateMachine.states)
                        {
                            if (cs.state.name == "Page2")
                            {
                                page2State = cs.state;
                                break;
                            }
                        }
                        if (page2State != null)
                        {
                            var resetToPage2 = resetState.AddTransition(page2State);
                            resetToPage2.hasExitTime = true;
                            resetToPage2.exitTime = 1f;
                            resetToPage2.duration = 0f;
                            resetToPage2.hasFixedDuration = true;
                        }
                    }
                    else
                    {
                        // Single page: self-loop with Loop=true
                        var selfLoop = resetState.AddTransition(resetState);
                        selfLoop.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.LoopParameterName);
                        selfLoop.hasExitTime = true;
                        selfLoop.exitTime = 1f;
                        selfLoop.duration = 0f;
                        selfLoop.hasFixedDuration = true;
                    }

                    resetTarget = resetState;
                }
                else
                {
                    resetTarget = page1State;
                }

                var anyToReset = rootStateMachine.AddAnyStateTransition(resetTarget);
                anyToReset.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.ResetParameterName);
                anyToReset.AddCondition(AnimatorConditionMode.If, 0, FlipbookConstants.EnabledParameterName);
                anyToReset.hasExitTime = false;
                anyToReset.duration = 0f;
                anyToReset.hasFixedDuration = true;
                anyToReset.canTransitionToSelf = true;
            }

            // 10. Persist
            EditorUtility.SetDirty(rootStateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            FlipbookGeneratorLog.Info(
                $"AnimatorController saved: {outputPath} ({clips.Length} state(s))");

            return controller;
        }
    }
}
