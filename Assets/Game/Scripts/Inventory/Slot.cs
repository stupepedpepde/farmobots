using System;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Scripts.Inventory {
    public class Slot : VisualElement {
        private Image icon;
        private Label stackLabel;
        public Guid itemID { get; private set; } = Guid.Empty;
        public int index { get; set; }
        public int inventoryIndex { get; set; } = -1;

        public event Action<Slot> OnDragStarted;
        public event Action<Slot> OnShiftClicked;
        public event Action<Slot> OnPointerDown;

        public Slot() {
            BuildSlot();
        }

        private void BuildSlot() {
            var builder = new UIBuilder(this)
                .WithName("slot")
                .WithSize(64, 64)
                .WithBackgroundColor(new Color(0.15f, 0.15f, 0.15f, 0.8f))
                .WithBorders(2, new Color(0.3f, 0.3f, 0.3f), 8)
                .WithPaddings(4)
                .WithMargins(0, 4, 4, 0);

            icon = UIBuilder.CreateImage()
                .WithName("slot-icon")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .Build<Image>();

            stackLabel = UIBuilder.CreateLabel()
                .WithName("stack-label")
                .WithText("")
                .WithPosition(UIPosition.ABSOLUTE)
                .WithPosition(right: -4, bottom: -4)
                .WithColor(Color.white)
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .WithTextShadow(new Color(0, 0, 0, 0.8f), new Vector2(1, 1))
                .WithVisibility(false)
                .Build<Label>();

            Add(icon);
            Add(stackLabel);

            this.WithHoverEffects(
                onEnter: () => {
                    style.borderTopColor = new Color(0.5f, 0.5f, 0.5f);
                    style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
                    style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
                    style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
                    style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                },
                onLeave: () => {
                    style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
                    style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
                    style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
                    style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
                    style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
                }
            );

            RegisterCallback<PointerDownEvent>(OnPointerDownEvent);
        }

        public void Set(Item item) {
            if (item == null || item.details == null) {
                Clear();
                return;
            }

            itemID = item.details.ID;

            if (item.details.Icon != null) {
                icon.style.backgroundImage = new StyleBackground(item.details.Icon);
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }

            if (item.quantity > 1) {
                stackLabel.text = item.quantity.ToString();
                stackLabel.style.visibility = Visibility.Visible;
            } else {
                stackLabel.text = "";
                stackLabel.style.visibility = Visibility.Hidden;
            }

            // Highlight slot with item
            style.borderTopColor = new Color(0.4f, 0.4f, 0.2f);
            style.borderRightColor = new Color(0.4f, 0.4f, 0.2f);
            style.borderBottomColor = new Color(0.4f, 0.4f, 0.2f);
            style.borderLeftColor = new Color(0.4f, 0.4f, 0.2f);

            // Attach tooltip if item has description
            if (!string.IsNullOrEmpty(item.details.Description)) {
                UITooltip.Attach(this, $"{item.details.name}\n{item.details.Description}", 0.1f);
            }
        }

        public void Clear() {
            itemID = Guid.Empty;
            icon.style.backgroundImage = null;
            stackLabel.text = "";
            stackLabel.style.visibility = Visibility.Hidden;

            style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            UITooltip.Detach(this);
        }

        private void OnPointerDownEvent(PointerDownEvent evt) {
            if (itemID.Equals(Guid.Empty)) return;

            OnPointerDown?.Invoke(this);

            if (evt.button == 0) { // lmb
                if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed) {
                    OnShiftClicked?.Invoke(this);
                } else {
                    OnDragStarted?.Invoke(this);
                }
                evt.StopPropagation();
            }
        }

        public Slot WithHoverEffects(Action onEnter, Action onLeave) {
            RegisterCallback<PointerEnterEvent>(evt => onEnter?.Invoke());
            RegisterCallback<PointerLeaveEvent>(evt => onLeave?.Invoke());
            return this;
        }
    }
}