using Digger.Modules.Core.Editor;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Digger.Modules.AdvancedOperations.Sources.Editor
{
    [InitializeOnLoad]
    public class DiggerAdvancedOperationsDefines
    {
        private const string DiggerAdvancedOperationsDefine = "__DIGGER_ADVANCED_OPERATIONS__";

        static DiggerAdvancedOperationsDefines()
        {
            DiggerDefines.InitDefine(DiggerAdvancedOperationsDefine);
        }

        [PostProcessScene(0)]
        public static void OnPostprocessScene()
        {
            DiggerDefines.InitDefine(DiggerAdvancedOperationsDefine);
        }
    }
}