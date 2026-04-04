using System.Collections.Generic;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal sealed class FlipbookResultInfo
    {
        internal bool IsDryRun;
        internal bool Success = true;
        internal string ModeName;
        internal List<string> Lines = new List<string>();
        internal string PingAssetPath;
    }
}
