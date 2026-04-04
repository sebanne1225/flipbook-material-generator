using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookGeneratorLog
    {
        private const string Prefix = "[FlipbookMaterialGenerator]";

        internal static bool Enabled;

        internal static void Info(string message)
        {
            if (!Enabled) return;
            Debug.Log($"{Prefix} {message}");
        }

        internal static void Warn(string message)
        {
            if (!Enabled) return;
            Debug.LogWarning($"{Prefix} {message}");
        }

        internal static void Error(string message) => Debug.LogError($"{Prefix} {message}");
    }
}
