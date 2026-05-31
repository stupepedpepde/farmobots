// UIMenu.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.UI {
    public class UIMenu {
        public VisualElement Root { get; private set; }
        public VisualElement ContentContainer { get; private set; }
        private Action onClose;

        private UIMenu() { }

        public static UIMenu Create(float backgroundOpacity = 0.85f) {
            var menu = new UIMenu();

            // Full-screen overlay
            menu.Root = UIBuilder.CreateContainer()
                .WithName("menu-overlay")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithPosition(UIPosition.ABSOLUTE)
                .WithBackgroundColor(new Color(0, 0, 0, backgroundOpacity))
                .WithDisplay(UIDisplay.FLEX)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .Build();

            // Main content panel (centered, styled)
            var panel = UIBuilder.CreateContainer()
                .WithName("menu-panel")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f, 0.95f))
                .WithBorderRadius(20)
                .WithPaddings(20)
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.FlexEnd)
                .WithJustifyContent(Justify.Center)
                .Build();

            menu.ContentContainer = UIBuilder.CreateContainer()
                .WithName("menu-content")
                .WithSize(Length.Percent(100), Length.Auto())
                .WithMargins(0, 20, 0, 0)
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.FlexEnd)
                .Build();

            panel.Add(menu.ContentContainer);
            menu.Root.Add(panel);

            return menu;
        }

        public UIMenu WithTitle(string title) {
            var titleLabel = UIBuilder.CreateLabel()
                .WithName("menu-title")
                .WithText(title)
                .WithColor(Color.white)
                .WithFontSize(90)
                .WithFontStyle(FontStyle.Bold)
                .WithTextAlign(TextAnchor.MiddleRight)
                .WithMargins(0, 0, 24, 0)
                .Build<Label>();

            ContentContainer.Add(titleLabel);
            return this;
        }

        public UIMenu AddButton(string text, Action onClick, float hoverScale = 1.2f, float hoverRotate = 10f) {
            var button = UIBuilder.CreateButton()
                .WithName($"menu-button-{text}")
                .WithText(text)
                .WithClass("menu-button")
                .WithFontSize(40)
                .WithFontStyle(FontStyle.Bold)
                .WithColor(Color.white)
                .WithBackgroundColor(Color.clear)
                .WithMargins(2, 0)
                .WithPaddings(0)
                .WithBorders(0, Color.black)
                .OnClick(onClick)
                .Build<Button>();

            button.style.transitionDuration = new List<TimeValue> { new TimeValue(0.2f, TimeUnit.Second) };
            button.style.transitionProperty = new List<StylePropertyName> {
                new StylePropertyName("color"),
                new StylePropertyName("scale"),
                new StylePropertyName("rotate")
            };

            button.style.transitionTimingFunction = new List<EasingFunction> {
                EasingMode.EaseOut
            };

            button.RegisterCallback<PointerEnterEvent>(_ => {
                button.style.color = new Color(0.584f, 0.584f, 0.584f);
                button.style.scale = new Scale(new Vector3(hoverScale, hoverScale, 1));
                button.style.rotate = new Rotate(Angle.Degrees(hoverRotate));
            });

            button.RegisterCallback<PointerLeaveEvent>(_ => {
                button.style.color = Color.white;
                button.style.scale = Scale.None();
                button.style.rotate = Rotate.None();
            });

            ContentContainer.Add(button);
            return this;
        }

        public UIMenu OnClose(Action callback) {
            onClose = callback;
            return this;
        }

        public void Show(VisualElement parent) {
            if (parent == null) return;
            parent.Add(Root);
            Root.BringToFront();
        }

        public void Close() {
            onClose?.Invoke();
            Root?.RemoveFromHierarchy();
        }
    }
}