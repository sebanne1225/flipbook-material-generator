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
            string outputPath)
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
    }
}
