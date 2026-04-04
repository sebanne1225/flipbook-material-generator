using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookAnimationBuilder
    {
        internal static AnimationClip Build(
            int pageIndex,
            int pageCount,
            int frameCount,
            float fps,
            string outputPath,
            bool hasAudio = false)
        {
            var clip = new AnimationClip { frameRate = fps };

            var keyframes = new Keyframe[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var time = i / fps;
                keyframes[i] = new Keyframe(time, i);
                // Stepped tangent — no interpolation between frames
                keyframes[i].inTangent = float.PositiveInfinity;
                keyframes[i].outTangent = float.PositiveInfinity;
            }

            var curve = new AnimationCurve(keyframes);
            var rendererPath = FlipbookConstants.PageQuadPath(pageIndex);
            clip.SetCurve(rendererPath, typeof(MeshRenderer), FlipbookConstants.MaterialCurrentFrameProperty, curve);

            // Enable this page's Quad
            var clipDuration = frameCount / fps;
            var enabledCurve = AnimationCurve.Constant(0f, clipDuration, 1f);
            clip.SetCurve(rendererPath, typeof(MeshRenderer), FlipbookConstants.RendererEnabledProperty, enabledCurve);

            // Explicitly disable all other pages' Quads
            var disabledCurve = AnimationCurve.Constant(0f, clipDuration, 0f);
            for (var p = 0; p < pageCount; p++)
            {
                if (p == pageIndex) continue;
                clip.SetCurve(FlipbookConstants.PageQuadPath(p), typeof(MeshRenderer), FlipbookConstants.RendererEnabledProperty, disabledCurve);
            }

            // Keep Audio GameObject active during normal playback (m_IsActive=1 constant)
            // Using m_IsActive instead of AudioSource.m_Enabled because:
            // - GameObject activation with playOnAwake=true reliably starts audio
            // - Component enable/disable doesn't reliably restart audio
            if (hasAudio)
            {
                var audioOnCurve = AnimationCurve.Constant(0f, clipDuration, 1f);
                clip.SetCurve(FlipbookConstants.AudioObjectName, typeof(GameObject), FlipbookConstants.GameObjectActiveProperty, audioOnCurve);
            }

            // Set clip to non-looping (Animator handles loop transitions)
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateAsset(clip, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"AnimationClip saved: {outputPath} (Page {pageIndex + 1}, {frameCount} frames, {fps} FPS)");

            return clip;
        }

        /// <summary>
        /// Builds a Reset variant of Page1 clip with Audio OFF→ON keyframes
        /// to restart AudioSource from the beginning on manual reset.
        /// </summary>
        internal static AnimationClip BuildResetClip(
            int pageCount,
            int frameCount,
            float fps,
            string outputPath)
        {
            // Same content as Page1 (pageIndex=0)
            var clip = new AnimationClip { frameRate = fps };

            var keyframes = new Keyframe[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var time = i / fps;
                keyframes[i] = new Keyframe(time, i);
                keyframes[i].inTangent = float.PositiveInfinity;
                keyframes[i].outTangent = float.PositiveInfinity;
            }

            var curve = new AnimationCurve(keyframes);
            var rendererPath = FlipbookConstants.PageQuadPath(0);
            clip.SetCurve(rendererPath, typeof(MeshRenderer), FlipbookConstants.MaterialCurrentFrameProperty, curve);

            var clipDuration = frameCount / fps;
            var enabledCurve = AnimationCurve.Constant(0f, clipDuration, 1f);
            clip.SetCurve(rendererPath, typeof(MeshRenderer), FlipbookConstants.RendererEnabledProperty, enabledCurve);

            var disabledCurve = AnimationCurve.Constant(0f, clipDuration, 0f);
            for (var p = 0; p < pageCount; p++)
            {
                if (p == 0) continue;
                clip.SetCurve(FlipbookConstants.PageQuadPath(p), typeof(MeshRenderer), FlipbookConstants.RendererEnabledProperty, disabledCurve);
            }

            // Audio GameObject OFF→ON: restart AudioSource via playOnAwake
            var offKey = new Keyframe(0f, 0f);
            offKey.outTangent = float.PositiveInfinity;
            var onKey = new Keyframe(1f / fps, 1f);
            onKey.inTangent = float.PositiveInfinity;
            var audioOffOnCurve = new AnimationCurve(offKey, onKey);
            clip.SetCurve(FlipbookConstants.AudioObjectName, typeof(GameObject), FlipbookConstants.GameObjectActiveProperty, audioOffOnCurve);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateAsset(clip, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"ResetClip saved: {outputPath} (Page 1 + Audio restart, {frameCount} frames, {fps} FPS)");

            return clip;
        }
    }
}
