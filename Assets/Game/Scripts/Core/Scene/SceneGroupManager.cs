using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Scripts.Core.Scene {
    public class SceneGroupManager {
        public event Action<string> OnSceneLoaded = delegate { };
        public event Action<string> OnSceneUnloaded = delegate { };
        public event Action OnSceneGroupLoaded = delegate { };

        private SceneGroup active;

        public async Task LoadScenes(SceneGroup group, IProgress<float> progress, bool reloadDuplicates = false) {
            active = group;
            var loadedScenes = new List<string>();

            await UnloadScenes();
            int sceneCount = SceneManager.sceneCount;

            for (var i = 0; i < sceneCount; i++)
                loadedScenes.Add(SceneManager.GetSceneAt(i).name);

            var totalScenesToLoad = active.scenes.Count;
            var operationGroup = new AsyncOperationGroup(totalScenesToLoad);

            for (var i = 0; i < totalScenesToLoad; i++) {
                var sceneData = group.scenes[i];
                if (reloadDuplicates == false && loadedScenes.Contains(sceneData.name)) continue;

                var operation = SceneManager.LoadSceneAsync(sceneData.reference.Path, LoadSceneMode.Additive);
                operationGroup.Operations.Add(operation);

                OnSceneLoaded.Invoke(sceneData.name);
            }

            while (!operationGroup.IsDone) {
                progress?.Report(operationGroup.Progress);
                await Task.Delay(100);
            }

            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetSceneByName(active.FindSceneNameByType(SceneType.ACTIVE));

            if (activeScene.IsValid()) SceneManager.SetActiveScene(activeScene);
            OnSceneGroupLoaded.Invoke();
        }

        public async Task UnloadScenes() {
            var scenes = new List<string>();
            // var activeScene = SceneManager.GetActiveScene().name;

            int sceneCount = SceneManager.sceneCount;

            for (var i = sceneCount - 1; i > 0; i--) {
                var sceneAt = SceneManager.GetSceneAt(i);
                if (!sceneAt.isLoaded) continue;

                var sceneName = sceneAt.name;
                if (/*sceneName.Equals(activeScene) || */sceneName == "Bootstrapper") continue;

                scenes.Add(sceneName);
            }

            var operationGroup = new AsyncOperationGroup(scenes.Count);

            foreach (var scene in scenes) {
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null) continue;

                operationGroup.Operations.Add(operation);

                OnSceneUnloaded.Invoke(scene);
            }

            while (!operationGroup.IsDone)
                await Task.Delay(100);
        }

        public async Task AddToLoadedGroup(SceneGroup group, IProgress<float> progress = null) {
            var totalScenesToLoad = group.scenes.Count;
            var operationGroup = new AsyncOperationGroup(totalScenesToLoad);

            for (int i = 0; i < totalScenesToLoad; i++) {
                var sceneData = group.scenes[i];

                if (IsSceneLoaded(sceneData.name)) continue;

                var operation = SceneManager.LoadSceneAsync(sceneData.name, LoadSceneMode.Additive);
                operationGroup.Operations.Add(operation);
                OnSceneLoaded.Invoke(sceneData.name);
            }

            while (!operationGroup.IsDone) {
                progress?.Report(operationGroup.Progress);
                await Task.Delay(100);
            }

            var interfaceScene = group.scenes.FirstOrDefault(s => s.type == SceneType.INTERFACE || s.type == SceneType.MENU);
            if (interfaceScene != null) {
                var scene = SceneManager.GetSceneByName(interfaceScene.name);
                if (scene.IsValid()) SceneManager.SetActiveScene(scene);
            }

            OnSceneGroupLoaded.Invoke();
        }

        public async Task RemoveFromLoadedGroup(SceneGroup group, IProgress<float> progress = null) {
            var scenesToUnload = new List<string>();
            var operationGroup = new AsyncOperationGroup(group.scenes.Count);

            foreach (var sceneData in group.scenes) {
                var scene = SceneManager.GetSceneByName(sceneData.name);
                if (scene.IsValid() && scene.isLoaded) {
                    scenesToUnload.Add(sceneData.name);
                    var operation = SceneManager.UnloadSceneAsync(scene);
                    operationGroup.Operations.Add(operation);
                }
            }

            while (!operationGroup.IsDone) {
                progress?.Report(operationGroup.Progress);
                await Task.Delay(100);
            }

            foreach (var sceneName in scenesToUnload)
                OnSceneUnloaded.Invoke(sceneName);
        }

        public bool IsSceneLoaded(string sceneName) {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == sceneName) return true;

            return false;
        }

        public void SetActiveSceneByType(SceneType type) {
            if (active == null) return;

            var sceneName = active.FindSceneNameByType(type);
            if (!string.IsNullOrEmpty(sceneName)) {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid()) SceneManager.SetActiveScene(scene);
            }
        }
    }


    public readonly struct AsyncOperationGroup {
        public readonly List<AsyncOperation> Operations;
        public float Progress => Operations.Count == 0 ? 0 : Operations.Average(o => o.progress);
        public bool IsDone => Operations.All(o => o.isDone);

        public AsyncOperationGroup(int initialCapacity) {
            Operations = new List<AsyncOperation>(initialCapacity);
        }
    }
}