using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;

namespace Digger.Modules.Core.Editor
{
    [InitializeOnLoad]
    public class DiggerDefines
    {
        private const string DiggerDefine = "__DIGGER__";

        static DiggerDefines()
        {
            InitDefine(DiggerDefine);
        }

        public static void InitDefine(string def)
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target));
            if (defines.Contains(def))
                return;

            if (string.IsNullOrEmpty(defines)) {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target), def);
            }
            else {
                if (!defines[defines.Length - 1].Equals(';')) {
                    defines += ';';
                }

                defines += def;
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target), defines);
            }
        }

        [PostProcessScene(0)]
        public static void OnPostprocessScene()
        {
            InitDefine(DiggerDefine);
        }
    }
}