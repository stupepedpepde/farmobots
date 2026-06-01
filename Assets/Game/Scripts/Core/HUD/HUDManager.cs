using UnityEngine;
using UnityEngine.UIElements;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Core.Input;
using Game.Scripts.Core.UI;
using Game.Scripts.Plants;
using Game.Scripts.Player;
using UnityEngine.InputSystem;

namespace Game.Scripts.Core.HUD {
    public class HUDManager : MonoBehaviour, IInitializable, IUpdatable {
        public static HUDManager instance { get; private set; }

        // Habitability bar
        private VisualElement habitabilityBarFill;
        private Label habitabilityLabel;

        // Atmosphere composition panel (top left)
        private VisualElement compositionPanel;
        private Label oxygenCompLabel;
        private VisualElement oxygenBarFillComp;
        private Label carbonCompLabel;
        private VisualElement carbonBarFillComp;
        private Label nitrogenCompLabel;
        private VisualElement nitrogenBarFillComp;

        // Info panel
        private Label clockLabel;
        private Label weatherLabel;
        private Label tempLabel;

        private VisualElement crosshair;

        // Interaction prompt
        private VisualElement interactionPrompt;
        private Label interactionKeyLabel;
        private Label interactionActionLabel;
        private VisualElement interactionProgressContainer;
        private Label waterLabel;
        private VisualElement waterBarBg;
        private VisualElement waterBarFill;
        private IInteractable currentInteractable;

        // Cooldown display
        private Label scanCooldownLabel;
        private VisualElement scanCooldownContainer;

        public int InitializationOrder => 100;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            if (UIManager.instance == null) return;

            GameManager.instance?.Register(this as IUpdatable);
            CreateHUD();

            if (AtmosphereManager.instance != null) {
                AtmosphereManager.instance.OnOxygenChanged += UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnCarbonChanged += UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnNitrogenChanged += UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnTemperatureChanged += UpdateTemperatureDisplay;
                AtmosphereManager.instance.OnHabitabilityChanged += UpdateHabitabilityDisplay;
                UpdateAllAtmosphereDisplays(0);
                UpdateTemperatureDisplay(AtmosphereManager.instance.Temperature);
                UpdateHabitabilityDisplay(AtmosphereManager.instance.IsHabitable);
            } else
                Debug.LogWarning("AtmosphereManager not found.");

            if (TimeManager.instance != null) {
                TimeManager.instance.OnTimeChanged += UpdateClockDisplay;
                UpdateClockDisplay(TimeManager.instance.GetCurrentTimeOfDay);
            } else
                Debug.LogWarning("TimeManager not found.");

            if (WeatherManager.instance != null) {
                WeatherManager.instance.OnWeatherChanged += UpdateWeatherDisplay;
                UpdateWeatherDisplay(WeatherManager.instance.GetCurrentWeather);
            } else
                Debug.LogWarning("WeatherManager not found.");
        }

        private void CreateHUD() {
            VisualElement root = UIManager.instance.GetRoot();

            // === Habitability bar (top center) ===
            var habitabilityContainer = UIBuilder.CreateContainer()
                .WithName("habitability-container")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.TOP_CENTER)
                .WithSize(300, 40)
                .WithMargins(20, 0, 0, 0)
                .WithBackgroundColor(new Color(0, 0, 0, 0.6f))
                .WithBorders(2, Color.gray, 8)
                .WithPaddings(5)
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .Build();

            var habitabilityText = UIBuilder.CreateLabel()
                .WithText("Habitability:")
                .WithColor(Color.white)
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();
            habitabilityContainer.Add(habitabilityText);

            var barBg = UIBuilder.CreateContainer()
                .WithSize(200, 20)
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithBorders(1, Color.gray, 4)
                .WithMargins(0, 5, 0, 5)
                .Build();
            habitabilityContainer.Add(barBg);

            habitabilityBarFill = UIBuilder.CreateContainer()
                .WithSize(0, 20)
                .WithBackgroundColor(new Color(0.2f, 0.8f, 0.2f))
                .WithBorderRadius(4, 4, 4, 4)
                .Build();
            barBg.Add(habitabilityBarFill);

            habitabilityLabel = UIBuilder.CreateLabel()
                .WithText("0%")
                .WithColor(Color.white)
                .WithFontSize(14)
                .Build<Label>();
            habitabilityContainer.Add(habitabilityLabel);
            root.Add(habitabilityContainer);

            // === Atmosphere composition panel (top left) ===
            compositionPanel = UIBuilder.CreateContainer()
                .WithName("composition-panel")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.TOP_LEFT)
                .WithMargins(20, 0, 0, 20)
                .WithBackgroundColor(new Color(0, 0, 0, 0.6f))
                .WithPaddings(10, 15)
                .WithBorders(2, Color.gray, 8)
                .WithMinSize(200, Length.Auto())
                .WithFlexDirection(FlexDirection.Column)
                .Build();

            var oxygenRow = CreateCompositionRow("O₂:", new Color(0.3f, 0.6f, 0.9f));
            oxygenCompLabel = oxygenRow.label;
            oxygenBarFillComp = oxygenRow.barFill;
            compositionPanel.Add(oxygenRow.container);

            var carbonRow = CreateCompositionRow("CO₂:", new Color(0.8f, 0.4f, 0.1f));
            carbonCompLabel = carbonRow.label;
            carbonBarFillComp = carbonRow.barFill;
            compositionPanel.Add(carbonRow.container);

            var nitrogenRow = CreateCompositionRow("N₂:", new Color(0.2f, 0.8f, 0.2f));
            nitrogenCompLabel = nitrogenRow.label;
            nitrogenBarFillComp = nitrogenRow.barFill;
            compositionPanel.Add(nitrogenRow.container);
            root.Add(compositionPanel);

            // === Info panel (top right) ===
            var infoPanel = UIBuilder.CreateContainer()
                .WithName("info-panel")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.TOP_RIGHT)
                .WithMargins(20, 20, 0, 0)
                .WithBackgroundColor(new Color(0, 0, 0, 0.6f))
                .WithPaddings(10, 15)
                .WithBorders(2, Color.gray, 8)
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.FlexEnd)
                .Build();

            clockLabel = UIBuilder.CreateLabel()
                .WithText("--:-- --")
                .WithColor(new Color(1f, 0.85f, 0.2f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();

            weatherLabel = UIBuilder.CreateLabel()
                .WithText("Weather: Clear")
                .WithColor(Color.white)
                .WithFontSize(16)
                .Build<Label>();

            tempLabel = UIBuilder.CreateLabel()
                .WithText("Temp: --°C")
                .WithFontSize(16)
                .Build<Label>();

            infoPanel.Add(clockLabel);
            infoPanel.Add(weatherLabel);
            infoPanel.Add(tempLabel);
            root.Add(infoPanel);

            // === Crosshair ===
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

            // === Interaction Prompt ===
            interactionPrompt = UIBuilder.CreateContainer()
                .WithName("interaction-prompt")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.MIDDLE_CENTER)
                .WithMargins(120, 0, 0, 0)
                .WithBackgroundColor(new Color(0, 0, 0, 0.85f))
                .WithBorders(2, new Color(0.8f, 0.8f, 0.8f), 12)
                .WithPaddings(12, 20)
                .WithMinSize(240, Length.Auto())
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.Center)
                .WithVisibility(false)
                .Build();

            interactionKeyLabel = UIBuilder.CreateLabel()
                .WithText("Press E")
                .WithColor(Color.yellow)
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .WithTextAlign(TextAnchor.MiddleCenter)
                .Build<Label>();

            interactionActionLabel = UIBuilder.CreateLabel()
                .WithText("Interact")
                .WithColor(Color.white)
                .WithFontSize(16)
                .WithTextAlign(TextAnchor.MiddleCenter)
                .WithMargins(0, 4, 0, 0)
                .Build<Label>();

            interactionProgressContainer = UIBuilder.CreateContainer()
                .WithName("progress-container")
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.Stretch)
                .WithMargins(5, 0, 0, 0)
                .Build();

            waterLabel = UIBuilder.CreateLabel()
                .WithText("Water: --/-- (--%)")
                .WithColor(Color.cyan)
                .WithFontSize(12)
                .WithTextAlign(TextAnchor.MiddleCenter)
                .Build<Label>();

            // Custom water bar (background + fill)
            waterBarBg = UIBuilder.CreateContainer()
                .WithName("water-bar-bg")
                .WithHeight(24)
                .WithWidth(Length.Percent(100))
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithBorders(1, Color.gray, 4)
                .WithMargins(4, 0, 0, 0)
                .Build();

            waterBarFill = UIBuilder.CreateContainer()
                .WithName("water-bar-fill")
                .WithHeight(24)
                .WithWidth(0)
                .WithBackgroundColor(Color.cyan)
                .WithBorderRadius(4, 0, 0, 4)
                .Build();

            // === Scan Cooldown Panel (bottom right) ===
            scanCooldownContainer = UIBuilder.CreateContainer()
                .WithName("scan-cooldown-container")
                .WithPosition(UIPosition.ABSOLUTE)
                .Anchor(UIAnchor.BOTTOM_RIGHT)
                .WithMargins(0, 20, 20, 0)
                .WithBackgroundColor(new Color(0, 0, 0, 0.6f))
                .WithPaddings(10, 15)
                .WithBorders(2, Color.gray, 8)
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .Build();

            string scanKey = "";
            if (InputManager.instance != null && InputManager.instance.inputActions != null)
                scanKey = InputManager.instance.inputActions.General.Scan.GetBindingDisplayString();
            else
                scanKey = "F";

            scanCooldownLabel = UIBuilder.CreateLabel()
                .WithText($"{scanKey} Scan - Ready")
                .WithColor(Color.white)
                .WithFontSize(16)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();

            scanCooldownContainer.Add(scanCooldownLabel);
            root.Add(scanCooldownContainer);

            waterBarBg.Add(waterBarFill);
            interactionProgressContainer.Add(waterLabel);
            interactionProgressContainer.Add(waterBarBg);

            interactionPrompt.Add(interactionKeyLabel);
            interactionPrompt.Add(interactionActionLabel);
            interactionPrompt.Add(interactionProgressContainer);

            root.Add(interactionPrompt);
        }

        private (VisualElement container, Label label, VisualElement barFill) CreateCompositionRow(string name, Color barColor) {
            var row = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithMargins(0, 0, 5, 0)
                .Build();

            var label = UIBuilder.CreateLabel()
                .WithText(name)
                .WithColor(Color.white)
                .WithWidth(40)
                .WithFontSize(14)
                .Build<Label>();
            row.Add(label);

            var barBg = UIBuilder.CreateContainer()
                .WithSize(120, 12)
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithBorders(1, Color.gray, 2)
                .WithMargins(0, 5, 0, 5)
                .Build();
            row.Add(barBg);

            var barFill = UIBuilder.CreateContainer()
                .WithSize(0, 12)
                .WithBackgroundColor(barColor)
                .WithBorderRadius(2, 2, 2, 2)
                .Build();
            barBg.Add(barFill);

            var valueLabel = UIBuilder.CreateLabel()
                .WithText("0.0%")
                .WithColor(Color.white)
                .WithWidth(45)
                .WithFontSize(12)
                .Build<Label>();
            row.Add(valueLabel);

            return (row, valueLabel, barFill);
        }

        public void UpdateInteractionPrompt(IInteractable interactable, string key = "E") {
            if (interactable == null) {
                HideInteractionPrompt();
                return;
            }

            currentInteractable = interactable;
            interactionKeyLabel.text = $"Press {key}";
            interactionActionLabel.text = interactable.GetInteractionPrompt();

            if (interactable is Plant plant) {
                float maxWater = plant.GetMaxWater();
                float currentWater = plant.GetCurrentWater();
                float percent = plant.GetWaterPercentage();
                waterLabel.text = $"Water: {currentWater:F0}/{maxWater:F0} ({percent * 100:F0}%)";
                float widthPercent = Mathf.Clamp01(percent) * 100f;
                waterBarFill.style.width = new Length(widthPercent, LengthUnit.Percent);
                waterBarFill.style.backgroundColor = percent < 0.3f ? Color.red : Color.cyan;
                interactionProgressContainer.style.display = DisplayStyle.Flex;
            } else if (interactable is Collector.Collector collector) {
                float fillPercent = collector.GetFillPercentage();
                int flasks = collector.GetAvailableFlasks();
                waterLabel.text = $"Water: {collector.GetCurrentWater():F0}/{collector.GetMaxWater():F0} ({fillPercent * 100:F0}%)\n{flasks} flask(s) ready";
                float widthPercent = Mathf.Clamp01(fillPercent) * 100f;
                waterBarFill.style.width = new Length(widthPercent, LengthUnit.Percent);
                waterBarFill.style.backgroundColor = fillPercent > 0.8f ? Color.green : Color.cyan;
                interactionProgressContainer.style.display = DisplayStyle.Flex;
            } else {
                interactionProgressContainer.style.display = DisplayStyle.None;
            }

            interactionPrompt.style.visibility = Visibility.Visible;
        }

        public void HideInteractionPrompt() {
            interactionPrompt.style.visibility = Visibility.Hidden;
            currentInteractable = null;
        }

        public void OnUpdate(float deltaTime) {
            if (currentInteractable != null && interactionPrompt.style.visibility == Visibility.Visible) {
                if (currentInteractable is Plant plant) {
                    float maxWater = plant.GetMaxWater();
                    float currentWater = plant.GetCurrentWater();
                    float percent = plant.GetWaterPercentage();
                    waterLabel.text = $"Water: {currentWater:F0}/{maxWater:F0} ({percent * 100:F0}%)";
                    float widthPercent = Mathf.Clamp01(percent) * 100f;
                    waterBarFill.style.width = new Length(widthPercent, LengthUnit.Percent);
                    waterBarFill.style.backgroundColor = percent < 0.3f ? Color.red : Color.cyan;
                    interactionActionLabel.text = plant.GetInteractionPrompt();
                } else if (currentInteractable is Collector.Collector collector) {
                    float fillPercent = collector.GetFillPercentage();
                    int flasks = collector.GetAvailableFlasks();
                    waterLabel.text = $"Water: {collector.GetCurrentWater():F0}/{collector.GetMaxWater():F0} ({fillPercent * 100:F0}%)\n{flasks} flask(s) ready";
                    float widthPercent = Mathf.Clamp01(fillPercent) * 100f;
                    waterBarFill.style.width = new Length(widthPercent, LengthUnit.Percent);
                    waterBarFill.style.backgroundColor = fillPercent > 0.8f ? Color.green : Color.cyan;
                    interactionActionLabel.text = collector.GetInteractionPrompt();
                }
            }

            if (scanCooldownLabel != null && scanCooldownContainer != null)
            {
                PlayerScanner scanner = FindObjectOfType<PlayerScanner>();
                if (scanner != null)
                {
                    float remaining = scanner.CooldownRemaining;
                    string key = "";
                    if (InputManager.instance?.inputActions != null)
                        key = InputManager.instance.inputActions.General.Scan.GetBindingDisplayString();
                    else
                        key = "F";

                    if (remaining <= 0f)
                        scanCooldownLabel.text = $"[{key}] Scan - Ready";
                    else
                        scanCooldownLabel.text = $"[{key}] Scan - {remaining:F1}s";

                    // Optional: change color when on cooldown
                    scanCooldownLabel.style.color = remaining > 0f ? new Color(1f, 0.5f, 0.2f) : Color.white;
                }
                else
                {
                    scanCooldownLabel.text = "Scan - Unavailable";
                }
            }
        }

        private void UpdateAllAtmosphereDisplays(float _) {
            if (AtmosphereManager.instance == null) return;

            float oxygenPercent = AtmosphereManager.instance.OxygenPercentage;
            float carbonPercent = AtmosphereManager.instance.CarbonPercentage;
            float nitrogenPercent = AtmosphereManager.instance.NitrogenPercentage;

            oxygenCompLabel.text = $"{oxygenPercent:F1}%";
            oxygenBarFillComp.style.width = Mathf.Clamp01(oxygenPercent / 100f) * 120;

            carbonCompLabel.text = $"{carbonPercent:F2}%";
            carbonBarFillComp.style.width = Mathf.Clamp01(carbonPercent / 100f) * 120;

            nitrogenCompLabel.text = $"{nitrogenPercent:F1}%";
            nitrogenBarFillComp.style.width = Mathf.Clamp01(nitrogenPercent / 100f) * 120;

            float habitabilityProgress = CalculateHabitabilityProgress();
            habitabilityBarFill.style.width = habitabilityProgress * 200;
            habitabilityLabel.text = $"{habitabilityProgress * 100:F0}%";
        }

        private float CalculateHabitabilityProgress() {
            var atm = AtmosphereManager.instance;
            if (atm == null) return 0f;

            float oxygenTarget = 21f;
            float oxygenTolerance = 5f;
            float oxygenScore = 1f - Mathf.Clamp01(Mathf.Abs(atm.OxygenPercentage - oxygenTarget) / oxygenTolerance);

            float carbonTarget = 0.04f;
            float carbonTolerance = 0.5f;
            float carbonScore = 1f - Mathf.Clamp01(atm.CarbonPercentage / carbonTolerance);

            float nitrogenTarget = 78f;
            float nitrogenTolerance = 10f;
            float nitrogenScore = 1f - Mathf.Clamp01(Mathf.Abs(atm.NitrogenPercentage - nitrogenTarget) / nitrogenTolerance);

            float tempTarget = 20f;
            float tempTolerance = 15f;
            float tempScore = 1f - Mathf.Clamp01(Mathf.Abs(atm.Temperature - tempTarget) / tempTolerance);

            return Mathf.Clamp01((oxygenScore + carbonScore + nitrogenScore + tempScore) / 4f);
        }

        private void UpdateHabitabilityDisplay(bool habitable) {
            habitabilityBarFill.style.backgroundColor = habitable ? new Color(0.2f, 1f, 0.2f) : new Color(0.2f, 0.8f, 0.2f);
        }

        private void UpdateTemperatureDisplay(float temperature) {
            if (tempLabel == null) return;
            tempLabel.text = $"Temp: {temperature:F1}°C";
            float t = Mathf.Clamp01((temperature - 0f) / 40f);
            tempLabel.style.color = Color.Lerp(Color.cyan, Color.red, t);
        }

        private void UpdateClockDisplay(float timeOfDay) {
            if (clockLabel == null) return;
            float hour24 = timeOfDay * 24f;
            int hour = Mathf.FloorToInt(hour24);
            int minute = Mathf.FloorToInt((hour24 - hour) * 60);
            string ampm = hour < 12 ? "AM" : "PM";
            int hour12 = hour % 12;
            if (hour12 == 0) hour12 = 12;
            clockLabel.text = $"{hour12:00}:{minute:00} {ampm}";
        }

        private void UpdateWeatherDisplay(WeatherType weather) {
            if (weatherLabel != null)
                weatherLabel.text = $"Weather: {weather.ToString()}";
        }

        private void OnDestroy() {
            if (AtmosphereManager.instance != null) {
                AtmosphereManager.instance.OnOxygenChanged -= UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnCarbonChanged -= UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnNitrogenChanged -= UpdateAllAtmosphereDisplays;
                AtmosphereManager.instance.OnTemperatureChanged -= UpdateTemperatureDisplay;
                AtmosphereManager.instance.OnHabitabilityChanged -= UpdateHabitabilityDisplay;
            }
            if (TimeManager.instance != null)
                TimeManager.instance.OnTimeChanged -= UpdateClockDisplay;
            if (WeatherManager.instance != null)
                WeatherManager.instance.OnWeatherChanged -= UpdateWeatherDisplay;

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
        }
    }
}