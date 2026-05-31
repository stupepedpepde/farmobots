using Game.Scripts.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.Menu {
    public class PauseMenu {
        private UIMenu menu;

        public static PauseMenu CreateAndShow() {
            var pauseMenu = new PauseMenu();
            pauseMenu.Build();
            return pauseMenu;
        }

        private void Build() {
            menu = UIMenu.Create(0.88f)
                .WithTitle("Paused")
                .AddButton("RESUME", OnResumeClicked)
                .AddButton("OPTIONS", OnOptionsClicked)
                .AddButton("QUIT", OnQuitClicked)
                .OnClose(() => { /* cleanup */ });
        }

        private void OnResumeClicked() {
            Close();
            GameManager.instance?.SetGameState(GameState.PLAYING);
        }

        private void OnOptionsClicked() {
            Debug.Log("Options from pause");
        }

        private void OnQuitClicked() {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        public void Show(VisualElement parent) {
            menu?.Show(parent);
        }

        public void Close() {
            menu?.Close();
        }
    }
}