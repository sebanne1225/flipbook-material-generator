using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookGeneratorLog
    {
        private const string Prefix = "[FlipbookMaterialGenerator]";

        internal static void Info(string message) => Debug.Log($"{Prefix} {message}");
        internal static void Warn(string message) => Debug.LogWarning($"{Prefix} {message}");
        internal static void Error(string message) => Debug.LogError($"{Prefix} {message}");
    }
}
