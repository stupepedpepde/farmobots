using Game.Scripts.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Art.UI.Menu.PauseMenu {
    public class PauseMenu : MonoBehaviour {
        private UIDocument doc;
        private Button resumeButton;
        private Button optionsButton;
        private Button menuButton;
        private Button quitButton;

        private void Awake() {
            doc = GetComponent<UIDocument>();

            resumeButton = doc.rootVisualElement.Q<Button>("resume-button");
            optionsButton = doc.rootVisualElement.Q<Button>("options-button");
            menuButton = doc.rootVisualElement.Q<Button>("menu-button");
            quitButton = doc.rootVisualElement.Q<Button>("quit-button");

            resumeButton.clicked += OnResumeButtonClicked;
            optionsButton.clicked += OnOptionsButtonClicked;
            menuButton.clicked += OnMenuButtonClicked;
            quitButton.clicked += OnQuitButtonClicked;
        }

        private void OnDestroy() {
            if (resumeButton != null) resumeButton.clicked -= OnResumeButtonClicked;
            if (optionsButton != null) optionsButton.clicked -= OnOptionsButtonClicked;
            if (menuButton != null) menuButton.clicked -= OnMenuButtonClicked;
            if (quitButton != null) quitButton.clicked -= OnQuitButtonClicked;
        }

        private void OnResumeButtonClicked() {
            Debug.Log("resume-button clicked");

            resumeButton.SetEnabled(false);
            GameEvents.RequestUIElement("pause", GameState.PAUSED);
            gameObject.SetActive(false);
        }

        private void OnOptionsButtonClicked() {
            Debug.Log("options-button clicked");
        }

        private async void OnMenuButtonClicked() {
            Debug.Log("main-button clicked");

            // menuButton.SetEnabled(false);
            // GameEvents.RequestSceneGroup("main-menu");
            // gameObject.SetActive(false);
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