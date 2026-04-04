namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookConstants
    {
        // GameObject names (Prefab structure)
        internal const string PagesObjectName = "Pages";
        internal const string QuadObjectName = "Quad";
        internal const string AudioObjectName = "Audio";
        internal const string PageObjectPrefix = "Page";

        // Path helpers
        internal static string PageObjectName(int pageIndex) => $"{PageObjectPrefix}{pageIndex + 1}";
        internal static string PageQuadPath(int pageIndex) => $"{PagesObjectName}/{PageObjectName(pageIndex)}/{QuadObjectName}";
        internal static string PagePath(int pageIndex) => $"{PagesObjectName}/{PageObjectName(pageIndex)}";

        // Animator parameter names
        internal const string EnabledParameterName = "FlipbookEnabled";
        internal const string LoopParameterName = "FlipbookLoop";
        internal const string ResetParameterName = "FlipbookReset";

        // Shader property names (cross-file references)
        internal const string ShaderCurrentFrame = "_CurrentFrame";
        internal const string MaterialCurrentFrameProperty = "material." + ShaderCurrentFrame;

        // Animation property names
        internal const string RendererEnabledProperty = "m_Enabled";
        internal const string GameObjectActiveProperty = "m_IsActive";
    }
}
