using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.UI {
    public static class UITooltip {
        private static VisualElement currentTooltip;
        private static long showTime;
        private static VisualElement hoveredElement;

        // Store callbacks to detach later
        private static Dictionary<VisualElement, (EventCallback<PointerEnterEvent> enter,
                                                   EventCallback<PointerLeaveEvent> leave,
                                                   EventCallback<PointerMoveEvent> move)> registeredCallbacks
            = new Dictionary<VisualElement, (EventCallback<PointerEnterEvent>, EventCallback<PointerLeaveEvent>, EventCallback<PointerMoveEvent>)>();

        public static void Attach(VisualElement element, string text, float delay = 0.5f) {
            if (string.IsNullOrEmpty(text)) return;

            // Remove any existing tooltip on this element first
            Detach(element);

            EventCallback<PointerEnterEvent> enterHandler = evt => {
                showTime = DateTime.Now.Ticks + (long)(delay * TimeSpan.TicksPerSecond);
                hoveredElement = element;
            };

            EventCallback<PointerLeaveEvent> leaveHandler = evt => {
                showTime = 0;
                hoveredElement = null;
                HideTooltip();
            };

            EventCallback<PointerMoveEvent> moveHandler = evt => {
                if (showTime > 0 && DateTime.Now.Ticks >= showTime && hoveredElement == element) {
                    ShowTooltip(element, text, evt.position);
                    showTime = 0;
                }
            };

            element.RegisterCallback(enterHandler);
            element.RegisterCallback(leaveHandler);
            element.RegisterCallback(moveHandler);

            registeredCallbacks[element] = (enterHandler, leaveHandler, moveHandler);
        }

        public static void Detach(VisualElement element) {
            if (registeredCallbacks.TryGetValue(element, out var handlers)) {
                element.UnregisterCallback(handlers.enter);
                element.UnregisterCallback(handlers.leave);
                element.UnregisterCallback(handlers.move);
                registeredCallbacks.Remove(element);
            }

            // If the detached element is the one currently hovered, hide tooltip
            if (hoveredElement == element) {
                hoveredElement = null;
                showTime = 0;
                HideTooltip();
            }
        }

        public static void Hide() {
            HideTooltip();
        }

        private static void ShowTooltip(VisualElement element, string text, Vector2 position) {
            HideTooltip();

            currentTooltip = UIBuilder.CreateContainer()
                .WithName("tooltip")
                .WithPosition(UIPosition.ABSOLUTE)
                .WithBackgroundColor(new Color(0.1f, 0.1f, 0.1f, 0.95f))
                .WithColor(Color.white)
                .WithPaddings(8)
                .WithBorders(1, new Color(0.3f, 0.3f, 0.3f), 4)
                .WithFontSize(12)
                .WithFontStyle(FontStyle.Normal)
                .WithMaxSize(200, Length.Auto())
                .Build();

            var label = UIBuilder.CreateLabel()
                .WithText(text)
                .WithEllipsis()
                .Build<Label>();

            currentTooltip.Add(label);

            var root = element.panel.visualTree;
            root.Add(currentTooltip);

            // Calculate position (avoid going off screen)
            var tooltipWidth = currentTooltip.resolvedStyle.width;
            var tooltipHeight = currentTooltip.resolvedStyle.height;
            var screenWidth = root.resolvedStyle.width;
            var screenHeight = root.resolvedStyle.height;

            float x = position.x + 15;
            float y = position.y + 15;

            if (x + tooltipWidth > screenWidth)
                x = screenWidth - tooltipWidth - 10;
            if (y + tooltipHeight > screenHeight)
                y = position.y - tooltipHeight - 10;

            currentTooltip.style.left = x;
            currentTooltip.style.top = y;
            currentTooltip.BringToFront();
        }

        private static void HideTooltip() {
            currentTooltip?.RemoveFromHierarchy();
            currentTooltip = null;
        }

        public static void AttachToButton(VisualElement button, string tooltipText, float delay = 0.3f) {
            Attach(button, tooltipText, delay);
        }

        public static void AttachToImage(VisualElement image, string tooltipText, float delay = 0.3f) {
            Attach(image, tooltipText, delay);
        }
    }
}