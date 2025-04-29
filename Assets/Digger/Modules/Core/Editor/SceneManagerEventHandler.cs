using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Jobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Digger.Modules.Core.Editor
{
    [InitializeOnLoad]
    public class SceneManagerEventHandler
    {
        private static Scene currentScene;

        static SceneManagerEventHandler()
        {
            currentScene = SceneManager.GetActiveScene();
            EditorApplication.hierarchyChanged += HierarchyWindowChanged;
            SceneManager.sceneLoaded += (scene, mode) => {
                if (scene.IsValid() && scene.isLoaded) {
                    DiggerMasterEditor.LoadAllChunks(scene);
                }
            };
            EditorSceneManager.sceneSaving += OnBeforeSceneSave;
        }

        private static void HierarchyWindowChanged()
        {
            if (currentScene != SceneManager.GetActiveScene()) {
                Debug.Log($"[Digger] switched scene from {currentScene.name} to {SceneManager.GetActiveScene().name}");
                NativeCollectionsPool.Instance.Dispose();
                currentScene = SceneManager.GetActiveScene();
                if (currentScene.IsValid() && currentScene.isLoaded) {
                    DiggerMasterEditor.CheckDiggerVersion();
                    DiggerMasterEditor.LoadAllChunks(currentScene);
                }
            }
        }

        private static void OnBeforeSceneSave(Scene scene, string path)
        {
            var diggerMaster = Object.FindFirstObjectByType<DiggerMaster>();
            if (diggerMaster && diggerMaster.AutoSaveMeshesAsAssets)
            {
                DiggerMasterEditor.SaveMeshesAsAssets();
            }
        }
    }
}