using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookAnimationBuilder
    {
        internal static AnimationClip Build(
            int pageIndex,
            int frameCount,
            float fps,
            int materialIndex,
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
            var rendererPath = $"Page{pageIndex + 1}/Quad";
            var propertyName = $"material._CurrentFrame";
            clip.SetCurve(rendererPath, typeof(MeshRenderer), propertyName, curve);

            // Enable only this page's Quad; writeDefaultValues=true will disable all others
            var enabledCurve = AnimationCurve.Constant(0f, frameCount / fps, 1f);
            clip.SetCurve(rendererPath, typeof(MeshRenderer), "m_Enabled", enabledCurve);

            // Set clip to non-looping (Animator handles loop transitions)
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"AnimationClip saved: {outputPath} (Page {pageIndex + 1}, {frameCount} frames, {fps} FPS)");

            return clip;
        }
    }
}
