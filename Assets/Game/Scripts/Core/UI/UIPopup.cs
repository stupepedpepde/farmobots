using System;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Game.Scripts.Core.UI {
    public class UIPopup {
        public VisualElement root { get; private set; }
        public VisualElement content { get; private set; }
        public VisualElement header { get; private set; }
        public Label titleLabel { get; private set; }
        public Button closeButton { get; private set; }

        private string popupId;
        public string Id {
            get => popupId;
            set => popupId = value;
        }

        private bool isDraggable;
        private bool isDragging;
        private Vector2 dragStart;
        private Vector2 elementStart;
        private Action onClose;

        private bool isClosing = false;

        public static UIPopup Create(string title, bool draggable = true, bool closable = true, string id = null) {
            var popup = new UIPopup();
            popup.Id = id;
            popup.Build(title, draggable, closable);

            return popup;
        }

        private void Build(string title, bool draggable = true, bool closable = true) {
            isDraggable = draggable;

            root = UIBuilder.CreateContainer()
                .WithName("popup-overlay")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithPosition(UIPosition.ABSOLUTE)
                .WithBackgroundColor(new Color(0, 0, 0, 0.3f))
                .WithDisplay(UIDisplay.FLEX)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .Build();

            content = UIBuilder.CreateContainer()
                .WithName("popup-container")
                .WithSize(400, 500)
                .WithBackgroundColor(new Color(0.1f, 0.1f, 0.1f, 0.95f))
                .WithBorders(2, new Color(0.3f, 0.3f, 0.3f), 12)
                .WithPaddings(0)
                .WithFlexDirection(FlexDirection.Column)
                .Build();

            header = UIBuilder.CreateContainer()
                .WithName("popup-header")
                .WithSize(Length.Percent(100), 50)
                .WithBackgroundColor(new Color(0.2f, 0.15f, 0.1f, 1f))
                .WithBorders(0, 0, 2, 0, Color.clear, Color.clear, new Color(0.4f, 0.3f, 0.2f), Color.clear)
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.SpaceBetween)
                .WithPaddings(10, 15, 10, 15)
                .Build();

            titleLabel = UIBuilder.CreateLabel()
                .WithName("popup-title")
                .WithText(title)
                .WithColor(new Color(1f, 0.9f, 0.7f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .WithTextShadow(new Color(0.2f, 0.15f, 0.1f), new Vector2(1, 1))
                .Build<Label>();

            header.Add(titleLabel);

            if (closable) {
                closeButton = UIBuilder.CreateButton()
                    .WithName("popup-close-button")
                    .WithText("×")
                    .WithSize(30, 30)
                    .WithBackgroundColor(Color.clear)
                    .WithColor(Color.white)
                    .WithFontSize(24)
                    .WithBorders(2, new Color(0.5f, 0.5f, 0.5f), 15)
                    .OnClick(() => {
                        if (!string.IsNullOrEmpty(Id) && UIManager.instance != null)
                            UIManager.instance.ClosePopup(Id);
                        else
                            Close();
                    })
                    .Build<Button>();
                header.Add(closeButton);
            }

            content.Add(header);
            root.Add(content);

            if (isDraggable)
                MakeDraggable();
        }

        private void MakeDraggable() {
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);
        }

        private void OnHeaderPointerDown(PointerDownEvent evt) {
            if (!isDraggable) return;

            isDragging = true;
            dragStart = evt.position;
            elementStart = new Vector2(content.style.left.value.value, content.style.top.value.value);
            header.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt) {
            if (!isDragging) return;

            // var delta = evt.position - dragStart;
            // content.style.left = elementStart.x + delta.x;
            // content.style.top = elementStart.y + delta.y;
            // evt.StopPropagation();
        }

        private void OnHeaderPointerUp(PointerUpEvent evt) {
            if (!isDragging) return;

            isDragging = false;
            header.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        public UIPopup SetContent(VisualElement content) {
            var body = this.content.Q<VisualElement>("popup-body");
            if (body == null) {
                body = UIBuilder.CreateContainer()
                    .WithName("popup-body")
                    .WithSize(Length.Percent(100), Length.Percent(100))
                    .WithFlexDirection(FlexDirection.Column)
                    .WithPaddings(15)
                    .Build();
                this.content.Add(body);
            }

            body.Clear();
            body.Add(content);
            return this;
        }


        public UIPopup SetSize(StyleLength width, StyleLength height) {
            content.style.width = width;
            content.style.height = height;
            return this;
        }

        public UIPopup SetPosition(StyleLength? left = null, StyleLength? top = null, StyleLength? right = null, StyleLength? bottom = null) {
            if (left.HasValue) content.style.left = left.Value;
            if (top.HasValue) content.style.top = top.Value;
            if (right.HasValue) content.style.right = right.Value;
            if (bottom.HasValue) content.style.bottom = bottom.Value;
            return this;
        }

        public UIPopup OnClose(Action callback) {
            onClose = callback;
            return this;
        }

        public void Show(VisualElement parent = null) {
            if (parent == null)
                parent = GameObject.FindObjectOfType<UIDocument>()?.rootVisualElement;

            if (parent != null) {
                parent.Add(root);
                root.BringToFront();

                Cursor.lockState = CursorLockMode.None;
                GameManager.instance?.SetGameState(GameState.INTERFACE);
            }
        }

        public void Close() {
            if (isClosing) return;
            isClosing = true;

            if (!string.IsNullOrEmpty(Id) && UIManager.instance != null && UIManager.instance.IsAnyPopupOpen)
                UIManager.instance.ClosePopup(Id);

            Cursor.lockState = CursorLockMode.Locked;
            GameManager.instance?.SetGameState(GameState.PLAYING);
            onClose?.Invoke();
            root?.RemoveFromHierarchy();
            isClosing = false;
        }
    }
}