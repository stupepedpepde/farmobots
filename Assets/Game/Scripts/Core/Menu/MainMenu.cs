using Game.Scripts.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.Menu {
    public class MainMenu {
        private UIMenu menu;

        public static MainMenu CreateAndShow() {
            var mainMenu = new MainMenu();
            mainMenu.Build();
            return mainMenu;
        }

        private void Build() {
            menu = UIMenu.Create(0.9f)
                .WithTitle("Farmobots")
                .AddButton("PLAY", OnPlayClicked)
                .AddButton("OPTIONS", OnOptionsClicked)
                .AddButton("QUIT", OnQuitClicked)
                .OnClose(() => { /* cleanup */ });
        }

        private void OnPlayClicked() {
            Close();

            GameEvents.RequestSceneGroup("planet");
            GameManager.instance?.SetGameState(GameState.PLAYING);
        }

        private void OnOptionsClicked() {
            Debug.Log("Options clicked");
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