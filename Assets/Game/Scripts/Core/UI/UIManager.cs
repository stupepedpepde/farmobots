using System;
using System.Collections.Generic;
using Game.Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Game.Scripts.Core.UI {
    public class UIManager : MonoBehaviour, IInitializable {
        public static UIManager instance { get; private set; }

        private UIDocument doc;
        [SerializeField] private PanelSettings panelSettings;
        private VisualElement root;
        private Dictionary<string, UIPopup> activePopups = new Dictionary<string, UIPopup>();
        private HashSet<UIPopup> registeredPopups = new HashSet<UIPopup>();
        private Stack<VisualElement> uiStack = new Stack<VisualElement>();

        public int InitializationOrder => 40;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureDocument();
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            GameEvents.OnUISceneRequested += HandleUISceneRequest;
        }

        private void OnDestroy() {
            GameEvents.OnUISceneRequested -= HandleUISceneRequest;
            GameManager.instance?.Unregister(this as IInitializable);
        }

        private void EnsureDocument() {
            if (doc == null) {
                doc = GetComponent<UIDocument>();
                if (doc == null) {
                    var go = new GameObject("UI Document");
                    doc = go.AddComponent<UIDocument>();
                    doc.panelSettings = panelSettings;
                }
            }

            root = doc.rootVisualElement;
            if (root == null) {
                root = new VisualElement();
                doc.visualTreeAsset = null;
                doc.rootVisualElement?.Clear();
            }
        }

        public void RegisterPopup(UIPopup popup) {
            if (popup != null) registeredPopups.Add(popup);
        }

        public void UnregisterPopup(UIPopup popup) {
            if (popup != null) registeredPopups.Remove(popup);
        }

        public UIPopup CreatePopup(string id, string title, bool draggable = true, bool closable = true) {
            if (activePopups.ContainsKey(id)) {
                activePopups[id].Close();
                activePopups.Remove(id);
            }

            var popup = UIPopup.Create(title, draggable, closable);
            activePopups[id] = popup;
            RegisterPopup(popup);
            return popup;
        }

        public bool ShowPopup(string id) {
            if (activePopups.TryGetValue(id, out var popup)) {
                popup.Show(root);
                uiStack.Push(popup.root);
                SetUIState(true);
                return true;
            }

            return false;
        }

        public void TogglePopup(string id, string title = null, bool draggable = true, bool closable = true) {
            if (activePopups.TryGetValue(id, out var existingPopup) && existingPopup.root.parent != null) {
                ClosePopup(id);
                return;
            }

            CloseAllPopups();

            if (!activePopups.ContainsKey(id)) {
                var popup = UIPopup.Create(title ?? id, draggable, closable);
                activePopups[id] = popup;
                RegisterPopup(popup);
            }

            ShowPopup(id);
        }

        public void TogglePopup(string id, UIPopup popup) {
            if (activePopups.TryGetValue(id, out var existing) && existing.root.parent != null) {
                ClosePopup(id);
                return;
            }

            CloseAllPopups();
            activePopups[id] = popup;
            RegisterPopup(popup);
            ShowPopup(id);
        }

        public bool ClosePopup(string id) {
            if (activePopups.TryGetValue(id, out var popup)) {
                popup.Close();
                activePopups.Remove(id);
                UnregisterPopup(popup);

                if (uiStack.Count > 0 && uiStack.Peek() == popup.root)
                    uiStack.Pop();

                if (uiStack.Count == 0)
                    SetUIState(false);

                return true;
            }

            return false;
        }

        public void CloseAllPopups() {
            foreach (var popup in activePopups.Values)
                popup.Close();
            activePopups.Clear();

            foreach (var popup in registeredPopups)
                popup.Close();
            registeredPopups.Clear();

            uiStack.Clear();
            SetUIState(false);
        }

        #region Elements

        public VisualElement CreateContainer(string name = "") =>
            UIBuilder.CreateContainer()
                .WithName(name)
                .Build();

        public Label CreateLabel(string text = "", string name = "") =>
            UIBuilder.CreateLabel()
                .WithName(name)
                .WithText(text)
                .Build<Label>();

        public Button CreateButton(string text = "", string name = "", Action onClick = null) {
            var builder = UIBuilder.CreateButton()
                .WithName(name)
                .WithText(text);

            if (onClick != null)
                builder.OnClick(onClick);

            return builder.Build<Button>();
        }

        public Image CreateImage(string name = "", Sprite sprite = null) {
            var builder = UIBuilder.CreateImage()
                .WithName(name);

            if (sprite != null)
                builder.WithSprite(sprite);

            return builder.Build<Image>();
        }

        public VisualElement CreateElement(string name = "") => CreateContainer(name);

        public void ShowElement(VisualElement element, bool pushToStack = true) {
            if (root == null) EnsureDocument();
            if (root == null) {
                Debug.LogError("UIManager: root VisualElement is null, cannot show element.");
                return;
            }

            root.Add(element);
            if (pushToStack) {
                uiStack.Push(element);
                SetUIState(true);
            }
        }

        public void HideElement(VisualElement element) {
            element.RemoveFromHierarchy();
            if (uiStack.Count > 0 && uiStack.Peek() == element) {
                uiStack.Pop();
                if (uiStack.Count == 0)
                    SetUIState(false);
            }
        }

        #endregion

        private void SetUIState(bool visibility) {
            if (visibility) {
                GameManager.instance?.SetGameState(GameState.INTERFACE);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            } else {
                GameManager.instance?.SetGameState(GameState.PLAYING);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HandleUISceneRequest(object request, GameState state) {

        }

        #region Getters

        public VisualElement GetRoot() => root;

        #endregion
    }
}