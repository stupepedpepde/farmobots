using Game.Scripts.Core;
using Game.Scripts.Core.Scene;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Art.UI.Menu.MainMenu {
    public class MainMenu : MonoBehaviour {
        private SceneLoader sceneLoader;

        private UIDocument doc;
        private Button playButton;
        private Button optionsButton;
        private Button quitButton;

        private void Awake() {
            doc = GetComponent<UIDocument>();

            // find buttons
            playButton = doc.rootVisualElement.Q<Button>("play-button");
            optionsButton = doc.rootVisualElement.Q<Button>("options-button");
            quitButton = doc.rootVisualElement.Q<Button>("quit-button");

            // subscribe events
            playButton.clicked += OnPlayButtonClicked;
            optionsButton.clicked += OnOptionsButtonClicked;
            quitButton.clicked += OnQuitButtonClicked;
        }

        private void OnDestroy() {
            if (playButton != null) playButton.clicked -= OnPlayButtonClicked;
            if (optionsButton != null) optionsButton.clicked -= OnOptionsButtonClicked;
            if (quitButton != null) quitButton.clicked -= OnQuitButtonClicked;
        }

        private async void OnPlayButtonClicked() {
            Debug.Log("play-button clicked");

            playButton.SetEnabled(false);
            GameEvents.RequestSceneGroup("planet");
            GameManager.instance?.SetGameState(GameState.PLAYING);
            gameObject.SetActive(false);
        }

        private void OnOptionsButtonClicked() {
            Debug.Log("options-button clicked");
        }

        private void OnQuitButtonClicked() {
            Debug.Log("quit-button clicked");

            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}