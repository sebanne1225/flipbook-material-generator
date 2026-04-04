using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookVideoConverter
    {
        private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".mov", ".avi", ".mkv" };

        internal sealed class VideoInfo
        {
            internal float Fps;
            internal int Width;
            internal int Height;
            internal float Duration;
            internal bool HasAudioTrack;
        }

        internal static bool IsVideoFile(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var ext = Path.GetExtension(assetPath);
            return VideoExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsFFmpegAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("ffmpeg", "-version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var proc = Process.Start(psi))
                {
                    proc.StandardOutput.ReadToEnd();
                    proc.StandardError.ReadToEnd();
                    proc.WaitForExit(5000);
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static VideoInfo Probe(string fullPath)
        {
            try
            {
                var psi = new ProcessStartInfo("ffprobe",
                    $"-v error -select_streams v:0 -show_entries stream=r_frame_rate,width,height -show_entries format=duration -of json \"{fullPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var proc = Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);
                    if (proc.ExitCode != 0) return null;
                    var info = ParseProbeOutput(output);
                    info.HasAudioTrack = CheckAudioTrack(fullPath);
                    return info;
                }
            }
            catch (Exception e)
            {
                FlipbookGeneratorLog.Error($"ffprobe failed: {e.Message}");
                return null;
            }
        }

        internal static Texture2D[] ExtractFrames(string fullPath, float fps, int maxResolution,
            float startTime = 0f, float duration = 0f)
        {
            var mtime = File.GetLastWriteTimeUtc(fullPath).Ticks;
            var cacheKey = $"{fullPath}|{fps}|{maxResolution}|{startTime:F2}|{duration:F2}|{mtime}";
            var hash = ComputeShortHash(cacheKey);
            var tempDir = Path.Combine(Path.GetTempPath(), "FlipbookFrames", hash);

            try
            {
                // Cache hit: reuse existing PNGs
                var cached = Directory.Exists(tempDir) && Directory.GetFiles(tempDir, "*.png").Length > 0;
                if (cached)
                {
                    FlipbookGeneratorLog.Info("Using cached frames.");
                }
                else
                {
                    Directory.CreateDirectory(tempDir);
                    EditorUtility.DisplayProgressBar("Flipbook Material Generator", "動画を PNG に変換中...", 0.5f);

                    var scaleFilter = $"fps={fps},scale={maxResolution}:{maxResolution}:force_original_aspect_ratio=decrease";
                    var outputPattern = Path.Combine(tempDir, "frame_%04d.png").Replace('\\', '/');

                    var hasTrim = startTime > 0f || duration > 0f;
                    var ssArg = hasTrim ? $"-ss {startTime:F2} " : "";
                    var tArg = duration > 0f ? $"-t {duration:F2} " : "";

                    var psi = new ProcessStartInfo("ffmpeg",
                        $"{ssArg}-i \"{fullPath}\" {tArg}-vf \"{scaleFilter}\" \"{outputPattern}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = true,
                    };

                    using (var proc = Process.Start(psi))
                    {
                        var stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(300000); // 5 min timeout
                        if (proc.ExitCode != 0)
                        {
                            FlipbookGeneratorLog.Error($"FFmpeg failed (exit {proc.ExitCode}): {stderr}");
                            return Array.Empty<Texture2D>();
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Flipbook Material Generator", "フレームを読み込み中...", 0.8f);

                var pngFiles = Directory.GetFiles(tempDir, "*.png")
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (pngFiles.Length == 0)
                {
                    FlipbookGeneratorLog.Error("FFmpeg produced no frames.");
                    return Array.Empty<Texture2D>();
                }

                var frames = new List<Texture2D>(pngFiles.Length);
                foreach (var pngFile in pngFiles)
                {
                    var bytes = File.ReadAllBytes(pngFile);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        frames.Add(tex);
                    }
                    else
                    {
                        FlipbookGeneratorLog.Warn($"Failed to load frame: {pngFile}");
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }

                if (frames.Count == 0)
                {
                    FlipbookGeneratorLog.Error("No frames could be loaded from FFmpeg output.");
                    return Array.Empty<Texture2D>();
                }

                FlipbookGeneratorLog.Info($"Extracted {frames.Count} frame(s) from video.");
                return frames.ToArray();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        internal static AudioClip ExtractAudio(string videoFullPath, string outputAssetDir, string baseName,
            float startTime = 0f, float duration = 0f)
        {
            var wavFileName = $"{baseName}_audio.wav";
            var wavAssetPath = $"{outputAssetDir}/{wavFileName}";
            var wavFullPath = Path.GetFullPath(wavAssetPath);

            // Ensure output directory exists on disk
            var dirFullPath = Path.GetDirectoryName(wavFullPath);
            if (!string.IsNullOrEmpty(dirFullPath) && !Directory.Exists(dirFullPath))
                Directory.CreateDirectory(dirFullPath);

            // Cache check: skip extraction if WAV exists with matching settings
            var mtime = File.GetLastWriteTimeUtc(videoFullPath).Ticks;
            var cacheKey = $"{videoFullPath}|{startTime:F2}|{duration:F2}|{mtime}";
            var cacheKeyPath = Path.Combine(dirFullPath, "audio_cache_key.txt");
            if (File.Exists(wavFullPath) && File.Exists(cacheKeyPath) && File.ReadAllText(cacheKeyPath) == cacheKey)
            {
                var existingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavAssetPath);
                if (existingClip != null)
                {
                    FlipbookGeneratorLog.Info("Using cached audio.");
                    return existingClip;
                }
            }

            // Remove existing WAV files in Audio/ to prevent accumulation
            var existingWavs = AssetDatabase.FindAssets("t:AudioClip", new[] { outputAssetDir });
            foreach (var guid in existingWavs)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            try
            {
                EditorUtility.DisplayProgressBar("Flipbook Material Generator", "音声を抽出中...", 0.5f);

                var hasTrim = startTime > 0f || duration > 0f;
                var ssArg = hasTrim ? $"-ss {startTime:F2} " : "";
                var tArg = duration > 0f ? $"-t {duration:F2} " : "";

                var ffmpegArgs = $"-y {ssArg}-i \"{videoFullPath}\" {tArg}-vn -acodec pcm_s16le \"{wavFullPath}\"";
                var psi = new ProcessStartInfo("ffmpeg", ffmpegArgs)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                };

                using (var proc = Process.Start(psi))
                {
                    var stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(60000);
                    if (proc.ExitCode != 0)
                    {
                        FlipbookGeneratorLog.Error($"FFmpeg audio extraction failed (exit {proc.ExitCode}): {stderr}");
                        return null;
                    }
                }

                File.WriteAllText(cacheKeyPath, cacheKey);

                AssetDatabase.ImportAsset(wavAssetPath, ImportAssetOptions.ForceUpdate);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavAssetPath);
                if (clip == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to load extracted audio: {wavAssetPath}");
                    return null;
                }

                FlipbookGeneratorLog.Info($"Audio extracted: {wavAssetPath}");
                return clip;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool CheckAudioTrack(string fullPath)
        {
            try
            {
                var psi = new ProcessStartInfo("ffprobe",
                    $"-v error -select_streams a:0 -show_entries stream=codec_type -of csv=p=0 \"{fullPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var proc = Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.StandardError.ReadToEnd();
                    proc.WaitForExit(5000);
                    return proc.ExitCode == 0 && !string.IsNullOrEmpty(output);
                }
            }
            catch
            {
                return false;
            }
        }

        private static VideoInfo ParseProbeOutput(string json)
        {
            var info = new VideoInfo();

            // r_frame_rate: "30/1" or "30000/1001"
            var fpsMatch = Regex.Match(json, @"""r_frame_rate""\s*:\s*""(\d+)/(\d+)""");
            if (fpsMatch.Success)
            {
                var num = float.Parse(fpsMatch.Groups[1].Value);
                var den = float.Parse(fpsMatch.Groups[2].Value);
                if (den > 0) info.Fps = num / den;
            }

            var widthMatch = Regex.Match(json, @"""width""\s*:\s*(\d+)");
            if (widthMatch.Success)
                info.Width = int.Parse(widthMatch.Groups[1].Value);

            var heightMatch = Regex.Match(json, @"""height""\s*:\s*(\d+)");
            if (heightMatch.Success)
                info.Height = int.Parse(heightMatch.Groups[1].Value);

            var durationMatch = Regex.Match(json, @"""duration""\s*:\s*""([\d.]+)""");
            if (durationMatch.Success)
                info.Duration = float.Parse(durationMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            return info;
        }

        private static string ComputeShortHash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(16);
                for (var i = 0; i < 8; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
