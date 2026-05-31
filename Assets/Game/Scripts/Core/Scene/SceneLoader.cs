using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.Core.Scene {
    public class SceneLoader : MonoBehaviour, IInitializable, IUpdatable {
        [SerializeField] private Image loadingBar;
        [SerializeField] private float fillSpeed = 0.5f;
        [SerializeField] private Canvas loadingCanvas;
        [SerializeField] private Camera loadingCamera;

        [SerializeField] private SceneGroup[] sceneGroups;

        private float targetProgress;
        private bool isLoading;

        public readonly SceneGroupManager manager = new SceneGroupManager();

        public int InitializationOrder => 20;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            manager.OnSceneLoaded += sceneName => Debug.Log("Loaded: " + sceneName);
            manager.OnSceneUnloaded += sceneName => Debug.Log("Unloaded: " + sceneName);
            manager.OnSceneGroupLoaded += () => Debug.Log("Scene group loaded");

            GameEvents.OnSceneGroupRequested += HandleLoadRequest;
            GameEvents.OnSceneGroupAddRequested += HandleAddRequest;

            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            GameEvents.OnSceneGroupRequested -= HandleLoadRequest;
            GameEvents.OnSceneGroupAddRequested -= HandleAddRequest;

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
        }

        public void OnUpdate(float deltaTime) {
            if (!isLoading) return;

            float currentFillAmount = loadingBar.fillAmount;
            float progressDifference = Mathf.Abs(currentFillAmount - targetProgress);
            float dynamicFillSpeed = progressDifference * fillSpeed;

            loadingBar.fillAmount = Mathf.Lerp(currentFillAmount, targetProgress, deltaTime * dynamicFillSpeed);
        }

        public async Task LoadSceneGroup(int index) {
            loadingBar.fillAmount = 0.0f;
            targetProgress = 1.0f;

            if (index < 0 || index >= sceneGroups.Length) {
                Debug.LogError("Invalid scene group index: " + index);
                return;
            }

            LoadingProgress progress = new LoadingProgress();
            progress.Progressed += target => targetProgress = Mathf.Max(target, targetProgress);
            
            EnableLoadingCanvas();
            await manager.LoadScenes(sceneGroups[index], progress);
            EnableLoadingCanvas(false);
        }

        public async Task LoadSceneGroup(string groupName) {
            var group = Array.Find(sceneGroups, g => g.groupName == groupName);

            if (group != null) {
                int index = Array.IndexOf(sceneGroups, group);
                await LoadSceneGroup(index);
            } else
                Debug.LogWarning($"Scene group '{groupName}' not found!");
        }

        public async Task AddToLoadedGroup(string groupName) {
            var group = Array.Find(sceneGroups, g => g.groupName == groupName);
            if (group != null) await manager.AddToLoadedGroup(group);
            else Debug.LogWarning($"Scene group '{groupName}' not found!");
        }

        public async Task RemoveFromLoadedGroup(string groupName) {
            var group = Array.Find(sceneGroups, g => g.groupName == groupName);
            if (group != null) {
                await manager.RemoveFromLoadedGroup(group);
                manager.SetActiveSceneByType(SceneType.ACTIVE);
            } else Debug.LogWarning($"Scene group '{groupName}' not found!");
        }

        public SceneGroup GetSceneGroup(string groupName) {
            return Array.Find(sceneGroups, g => g.groupName == groupName);
        }

        public bool IsSceneGroupLoaded(string groupName) {
            var group = GetSceneGroup(groupName);
            if (group == null) return false;

            foreach (var sceneData in group.scenes)
                if (!manager.IsSceneLoaded(sceneData.name)) return false;

            return true;
        }

        private void EnableLoadingCanvas(bool enable = true) {
            isLoading = enable;
            loadingCanvas.gameObject.SetActive(enable);
            loadingCamera.gameObject.SetActive(enable);
        }


        # region Events
        private async void HandleLoadRequest(object request) {
            switch (request) {
                case int index:
                    await LoadSceneGroup(index);
                    break;
                case string groupName:
                    await LoadSceneGroup(groupName);
                    break;
                default:
                    Debug.LogError($"Unsupported scene group request type: {request?.GetType()}. Expected string or int.");
                    break;
            }
        }

        private async void HandleAddRequest(object request) {
            switch (request) {
                case int index:
                    Debug.LogWarning($"Index additive loading not implemented yet."); // todo
                    break;
                case string groupName:
                    await AddToLoadedGroup(groupName);
                    break;
                default:
                    Debug.LogError($"Unsupported scene group request type: {request?.GetType()}. Expected string or int.");
                    break;
            }
        }

        private async void HandleUISceneRequest(object request, GameState state) {
            switch (request) {
                case int index:
                    Debug.LogWarning($"To be implemented"); // todo
                    break;
                case string groupName:
                    if (!IsSceneGroupLoaded(groupName)) {
                        Cursor.lockState = CursorLockMode.None;
                        await AddToLoadedGroup(groupName);
                        GameManager.instance?.SetGameState(state);
                    } else {
                        Cursor.lockState = CursorLockMode.Locked;
                        await RemoveFromLoadedGroup(groupName);
                        GameManager.instance?.SetGameState(GameState.PLAYING);
                    }
                    break;
                default:
                    Debug.LogError($"Unsupported scene group request type: {request?.GetType()}. Expected string or int.");
                    break;
            }
        }
        # endregion
    }
    
    public class LoadingProgress : IProgress<float> {
        public event Action<float> Progressed;
        private const float ratio = 1.0f;

        public void Report(float value) {
            Progressed?.Invoke(value / ratio);
        }
    }
}