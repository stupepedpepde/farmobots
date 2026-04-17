using UnityEngine;
using UnityEngine.UIElements;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Core.UI;

namespace Game.Scripts.Core.HUD {
    public class HUDManager : MonoBehaviour, IInitializable {
        public static HUDManager instance { get; private set; }

        private VisualElement hudContainer;
        private VisualElement oxygenBarFill;
        private Label oxygenLabel;

        private VisualElement crosshair;
        private VisualElement interactionPrompt;
        private Label interactionKeyLabel;
        private Label interactionTextLabel;

        public int InitializationOrder => 100;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GameManager.instance?.Register(this);
        }

        public void Initialize() {
            if (UIManager.instance == null) return;

            CreateHUD();

            if (AtmosphereManager.instance != null) {
                AtmosphereManager.instance.OnOxygenChanged += UpdateOxygenDisplay;
                UpdateOxygenDisplay(AtmosphereManager.instance.OxygenPercentage);
            } else
                Debug.LogWarning("AtmosphereManager not found. HUD will not update.");
        }

        private void CreateHUD() {
            VisualElement root = UIManager.instance.GetRoot();

            hudContainer = UIBuilder.CreateContainer()
                .WithName("atmosphere-hud")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.TOP_CENTER)
                .WithSize(300, 50)
                .WithMargins(20, 0, 0, 0)
                .WithBackgroundColor(new Color(0, 0, 0, 0.6f))
                .WithBorders(2, Color.gray, 8)
                .WithPaddings(5)
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .Build();

            var label = UIBuilder.CreateLabel()
                .WithText("O₂:")
                .WithColor(Color.white)
                .WithFontSize(18)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();
            hudContainer.Add(label);

            var barBg = UIBuilder.CreateContainer()
                .WithName("oxygen-bar-bg")
                .WithSize(200, 20)
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithBorders(1, Color.gray, 4)
                .WithMargins(0, 5, 0, 5)
                .Build();
            hudContainer.Add(barBg);

            oxygenBarFill = UIBuilder.CreateContainer()
                .WithName("oxygen-bar-fill")
                .WithSize(0, 20)
                .WithBackgroundColor(new Color(0.2f, 0.8f, 0.2f))
                .WithBorderRadius(4, 4, 4, 4)
                .Build();
            barBg.Add(oxygenBarFill);

            oxygenLabel = UIBuilder.CreateLabel()
                .WithName("oxygen-percent")
                .WithText("0%")
                .WithColor(Color.white)
                .WithFontSize(16)
                .Build<Label>();
            hudContainer.Add(oxygenLabel);

            root.Add(hudContainer);

            crosshair = UIBuilder.CreateContainer()
                .WithName("crosshair")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.MIDDLE_CENTER)
                .WithSize(8, 8)
                .WithBackgroundColor(Color.white)
                .WithBorderRadius(4, 4, 4, 4)
                .WithBorders(1, Color.black, 4)
                .Build();
            root.Add(crosshair);

            interactionPrompt = UIBuilder.CreateContainer()
                .WithName("interaction-prompt")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.MIDDLE_CENTER)
                .WithMargins(100, 0, 0, 0)
                .WithSize(180, 80)
                .WithBackgroundColor(new Color(0, 0, 0, 0.7f))
                .WithBorders(2, new Color(0.8f, 0.8f, 0.8f), 8)
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithVisibility(false)
                .Build();

            interactionKeyLabel = UIBuilder.CreateLabel()
                .WithName("interaction-key")
                .WithText("E")
                .WithColor(Color.yellow)
                .WithFontSize(22)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();

            interactionTextLabel = UIBuilder.CreateLabel()
                .WithName("interaction-text")
                .WithText("Interact")
                .WithColor(Color.white)
                .WithFontSize(16)
                .WithMargins(8, 0, 0, 0)
                .Build<Label>();

            interactionPrompt.Add(interactionKeyLabel);
            interactionPrompt.Add(interactionTextLabel);
            root.Add(interactionPrompt);
        }

        private void UpdateOxygenDisplay(float percentage) {
            if (oxygenBarFill != null)
                oxygenBarFill.style.width = percentage * 200;

            if (oxygenLabel != null)
                oxygenLabel.text = $"{percentage * 100:F1}%";
        }

        public void ShowInteractionPrompt(string key, string actionText) {
            if (interactionPrompt == null) return;
            interactionKeyLabel.text = key;
            interactionTextLabel.text = actionText;
            interactionPrompt.style.visibility = Visibility.Visible;
        }

        public void HideInteractionPrompt() {
            if (interactionPrompt != null)
                interactionPrompt.style.visibility = Visibility.Hidden;
        }

        private void OnDestroy() {
            if (AtmosphereManager.instance != null)
                AtmosphereManager.instance.OnOxygenChanged -= UpdateOxygenDisplay;

            if (hudContainer != null && hudContainer.parent != null)
                hudContainer.RemoveFromHierarchy();

            GameManager.instance?.Unregister(this);
        }
    }
}