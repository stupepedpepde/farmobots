using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.Core.Environment {
    public enum WeatherType {
        CLEAR,
        RAIN,
        STORM,
        WIND,
        AURORA,
        SANDSTORM,
        FOG,
        SNOW
    }

    [Serializable]
    public struct WeatherData {
        public WeatherType type;
        [Range(0, 1)] public float probability;
    }

    public class WeatherManager : MonoBehaviour, IInitializable, IUpdatable {
        [Header("Weather Settings")]
        [SerializeField] private WeatherType initialWeather = WeatherType.CLEAR;
        [SerializeField] private float weatherChangeInterval = 150.0f;
        [SerializeField] private AnimationCurve weatherTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Space]
        [SerializeField] private List<WeatherData> weatherData = new List<WeatherData> {
            new WeatherData { type = WeatherType.CLEAR, probability = 0.6f },
            new WeatherData { type = WeatherType.RAIN, probability = 0.15f },
            new WeatherData { type = WeatherType.STORM, probability = 0.05f },
            new WeatherData { type = WeatherType.WIND, probability = 0.15f },
            new WeatherData { type = WeatherType.AURORA, probability = 0.05f }
        };
        [Space]
        [Header("Skybox Control")]
        [SerializeField] private Material skyboxMaterial;

        private WeatherType currentWeather;
        private WeatherType previousWeather;
        private WeatherType nextWeather;
        private float weatherTransitonTimer;
        private float weatherChangeTimer;
        private bool isTransitioning;
        private float transitionDuration = 10.0f;

        public event Action<WeatherType> OnWeatherChanged;
        public event Action<WeatherType, WeatherType> OnWeatherTransitionStarted;
        public event Action<WeatherType, WeatherType> OnWeatherTransitionCompleted;

        public int InitializationOrder => 2;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);

            OnWeatherChanged = null;
            OnWeatherTransitionStarted = null;
            OnWeatherTransitionCompleted = null;
        }

        public void Initialize() {
            GameManager.instance?.Register(this as IUpdatable);

            currentWeather = initialWeather;
            nextWeather = initialWeather;
            weatherChangeTimer = weatherChangeInterval;

            OnWeatherChanged += type => Debug.Log($"Weather changed to: {type}");
            OnWeatherTransitionStarted += (type1, type2) => Debug.Log($"Weather transition started: {type1} -> {type2}");
            OnWeatherTransitionCompleted += (type1, type2) => Debug.Log($"Weather transition completed: {type1} -> {type2}");
        }

        public void OnUpdate(float deltaTime) {
            UpdateWeatherTimers(deltaTime);
            UpdateWeatherEffects();
        }

        private void ScheduleNextWeather() {
            if (isTransitioning) return;

            WeatherType newWeather = GetRandomWeather();
            if (newWeather != currentWeather)
                StartWeatherTransition(newWeather);
        }

        private WeatherType GetRandomWeather() {
            float totalProbability = 0.0f;
            foreach (var wd in weatherData)
                totalProbability += wd.probability;

            float randomValue = UnityEngine.Random.Range(0.0f, totalProbability);
            float currentSum = 0.0f;

            foreach (var wd in weatherData) {
                currentSum += wd.probability;
                if (randomValue <= currentSum)
                    return wd.type;
            }

            return WeatherType.CLEAR;
        }

        private void UpdateWeatherTimers(float deltaTime) {
            if (isTransitioning) {
                weatherChangeTimer += deltaTime;
                float progress = Mathf.Clamp01(weatherChangeTimer / transitionDuration);
                float curvedProgress = weatherTransitionCurve.Evaluate(progress);

                UpdateTransitionEffects(curvedProgress);

                if (progress >= 1.0f) {
                    currentWeather = nextWeather;
                    isTransitioning = false;
                    OnWeatherChanged?.Invoke(currentWeather);
                    OnWeatherTransitionCompleted?.Invoke(currentWeather, previousWeather);
                }

                return;
            }

            weatherChangeTimer -= deltaTime;
            if (weatherChangeTimer <= 0.0f) {
                ScheduleNextWeather();
                weatherChangeTimer = weatherChangeInterval;
            }
        }

        private void UpdateWeatherEffects() {

        }

        private void UpdateTransitionEffects(float progress) {
            switch (nextWeather) {
                case WeatherType.AURORA:

                    break;
            }
        }

        private void StartWeatherTransition(WeatherType newWeather) {
            previousWeather = currentWeather;
            nextWeather = newWeather;
            isTransitioning = true;
            weatherTransitonTimer = 0.0f;

            OnWeatherTransitionStarted?.Invoke(previousWeather, nextWeather);
        }

        public void SetWeather(WeatherType weather, bool immediate = false) {
            if (immediate) {
                currentWeather = weather;
                nextWeather = weather;
                isTransitioning = false;
                OnWeatherChanged?.Invoke(currentWeather);
            } else StartWeatherTransition(weather);
        }

        public WeatherType GetCurrentWeather => currentWeather;
        public bool IsTransitioning => isTransitioning;
        public float TransitionProgress => isTransitioning ? weatherTransitonTimer / transitionDuration : 0f;
    }
}