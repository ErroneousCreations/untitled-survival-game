using UnityEditor;
using UnityEngine;

namespace Digger.Modules.Core.Editor
{
    public interface IOperationEditor
    {
        void OnEnable();
        void OnDisable();
        void OnInspectorGUI();
        void OnSceneGUI();
        Awaitable OnScene(UnityEditor.Editor editor, SceneView sceneview);
    }
}