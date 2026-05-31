using System;
using Game.Scripts.Plants;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Scripts.Core.Environment {
    public class AtmosphereManager : MonoBehaviour, IInitializable {
        public static AtmosphereManager instance { get; private set; }

        [Header("Oxygen Settings")]
        [SerializeField] private float maxOxygen = 100f;
        [SerializeField] private Vector2 oxygenStartRange = new Vector2(1f, 5f);   // 1-5% O₂ – extreme

        [Header("Carbon Dioxide Settings")]
        [SerializeField] private Vector2 carbonStartRange = new Vector2(5f, 15f);   // 5-15% CO₂ – high

        [Header("Nitrogen Settings")]
        [SerializeField] private Vector2 nitrogenStartRange = new Vector2(50f, 70f); // 50-70% N₂ – low

        [Header("Temperature Settings")]
        [SerializeField] private Vector2 baseTemperatureRange = new Vector2(-10f, 10f); // cold start
        [SerializeField] private float nightTemperatureOffset = -5f;
        [SerializeField] private float rainTemperatureOffset = -3f;

        [Header("Habitability Thresholds")]
        [SerializeField] private float minOxygenForHabitable = 18f;    // 18% O₂
        [SerializeField] private float maxOxygenForHabitable = 25f;
        [SerializeField] private float maxCarbonForHabitable = 0.5f;   // <0.5% CO₂
        [SerializeField] private float minNitrogenForHabitable = 70f;  // 70% N₂
        [SerializeField] private float maxNitrogenForHabitable = 80f;
        [SerializeField] private float minTempForHabitable = 10f;
        [SerializeField] private float maxTempForHabitable = 35f;

        private float currentOxygen;
        private float currentTemperature;
        private float currentCarbon;
        private float currentNitrogen;
        private float baseTemperature;
        private bool isHabitable;

        public event Action<float> OnOxygenChanged;
        public event Action<float> OnTemperatureChanged;
        public event Action<float> OnCarbonChanged;
        public event Action<float> OnNitrogenChanged;
        public event Action<bool> OnHabitabilityChanged;

        public float OxygenPercentage => Mathf.Clamp01(currentOxygen / maxOxygen) * 100f;
        public float Temperature => currentTemperature;
        public float CarbonPercentage => currentCarbon;
        public float NitrogenPercentage => currentNitrogen;
        public bool IsHabitable => isHabitable;

        public int InitializationOrder => 10;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            // Extreme uninhabitable start
            currentOxygen = Random.Range(oxygenStartRange.x, oxygenStartRange.y);
            currentCarbon = Random.Range(carbonStartRange.x, carbonStartRange.y);
            currentNitrogen = Random.Range(nitrogenStartRange.x, nitrogenStartRange.y);
            baseTemperature = Random.Range(baseTemperatureRange.x, baseTemperatureRange.y);
            UpdateTemperature();

            CheckHabitability();

            OnOxygenChanged?.Invoke(OxygenPercentage);
            OnTemperatureChanged?.Invoke(currentTemperature);
            OnCarbonChanged?.Invoke(currentCarbon);
            OnNitrogenChanged?.Invoke(currentNitrogen);

            if (TimeManager.instance != null)
                TimeManager.instance.OnTimeChanged += _ => UpdateTemperature();
            if (WeatherManager.instance != null)
                WeatherManager.instance.OnWeatherChanged += _ => UpdateTemperature();
        }

        private void UpdateTemperature() {
            if (TimeManager.instance == null || WeatherManager.instance == null) {
                currentTemperature = baseTemperature;
                OnTemperatureChanged?.Invoke(currentTemperature);
                CheckHabitability();
                return;
            }

            float temp = baseTemperature;
            DayStage stage = TimeManager.instance.GetCurrentDayStage;
            if (stage == DayStage.NIGHT || stage == DayStage.DAWN || stage == DayStage.DUSK)
                temp += nightTemperatureOffset;

            WeatherType weather = WeatherManager.instance.GetCurrentWeather;
            switch (weather) {
                case WeatherType.RAIN: temp += rainTemperatureOffset; break;
            }

            currentTemperature = temp;
            OnTemperatureChanged?.Invoke(currentTemperature);
            CheckHabitability();
        }

        private void CheckHabitability() {
            bool oxygenOk = currentOxygen >= minOxygenForHabitable && currentOxygen <= maxOxygenForHabitable;
            bool tempOk = currentTemperature >= minTempForHabitable && currentTemperature <= maxTempForHabitable;
            bool carbonOk = currentCarbon <= maxCarbonForHabitable;
            bool nitrogenOk = currentNitrogen >= minNitrogenForHabitable && currentNitrogen <= maxNitrogenForHabitable;

            bool newHabitable = oxygenOk && tempOk && carbonOk && nitrogenOk;
            if (newHabitable != isHabitable) {
                isHabitable = newHabitable;
                OnHabitabilityChanged?.Invoke(isHabitable);
                if (isHabitable)
                    Debug.Log("ATMOSPHERE HABITABLE! You win!");
            }
        }

        public bool IsAtmosphereWithin(PlantSO plant) {
            if (plant == null) return false;

            float o2 = OxygenPercentage;
            float co2 = CarbonPercentage;
            float n2 = NitrogenPercentage;

            return (o2 >= plant.oxygenMin && o2 <= plant.oxygenMax && co2 >= plant.carbonMin && co2 <= plant.carbonMax && n2 >= plant.nitrogenMin && n2 <= plant.nitrogenMax);
        }

        // --- Oxygen & Carbon (Plants) ---
        public void AddOxygen(float amount) {
            if (amount <= 0) return;
            currentOxygen = Mathf.Clamp(currentOxygen + amount, 0, maxOxygen);
            OnOxygenChanged?.Invoke(OxygenPercentage);
            CheckHabitability();
        }

        public void RemoveCarbon(float amount) {
            currentCarbon = Mathf.Clamp(currentCarbon - amount, 0f, 100f);
            OnCarbonChanged?.Invoke(currentCarbon);
            CheckHabitability();
        }

        // --- Nitrogen (Generators or special plants) ---
        public void AddNitrogen(float amount) {
            if (amount <= 0) return;
            currentNitrogen = Mathf.Clamp(currentNitrogen + amount, 0f, 100f);
            OnNitrogenChanged?.Invoke(currentNitrogen);
            CheckHabitability();
        }

        // --- Manual overrides ---
        public void SetOxygen(float value) {
            currentOxygen = Mathf.Clamp(value, 0, maxOxygen);
            OnOxygenChanged?.Invoke(OxygenPercentage);
            CheckHabitability();
        }

        public void SetCarbon(float value) {
            currentCarbon = Mathf.Clamp(value, 0f, 100f);
            OnCarbonChanged?.Invoke(currentCarbon);
            CheckHabitability();
        }

        public void SetNitrogen(float value) {
            currentNitrogen = Mathf.Clamp(value, 0f, 100f);
            OnNitrogenChanged?.Invoke(currentNitrogen);
            CheckHabitability();
        }

        public float GetCurrentOxygen() => currentOxygen;
        public float GetMaxOxygen() => maxOxygen;
    }
}