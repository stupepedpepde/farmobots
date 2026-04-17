using System;
using UnityEngine;

namespace Game.Scripts.Core.Environment {
    public enum DayStage {
        NIGHT,
        DAWN,
        DAY,
        DUSK
    }

    [Serializable]
    public struct DayStageSettings {
        public DayStage stage;
        [Range(0, 1)] public float startTime;
        [Range(0, 1)] public float endTime;

        public bool ContainsTime(float time) {
            if (startTime <= endTime)
                return time >= startTime && time < endTime;

            return time >= startTime || time < endTime;
        }

        public float GetProgress(float time) {
            if (!ContainsTime(time)) return 0.0f;

            if (startTime <= endTime)
                return (time - startTime) / (endTime - startTime);
            else
                if (endTime >= startTime)
                    return (time - startTime) / (1.0f - startTime + endTime);
                else
                    return (time + 1.0f - startTime) / (1.0f - startTime + endTime);
        }
    }

    public class TimeManager : MonoBehaviour, IInitializable, IUpdatable {
        [Header("Time Settings")]
        [SerializeField] private float dayDurationInSeconds = 300.0f;
        [SerializeField] private float currentTimeOfDay = 0.5f;
        [Space]
        [Header("Day Stage Settings")]
        [SerializeField] private DayStageSettings[] dayStages = new DayStageSettings[] {
            new DayStageSettings { stage = DayStage.NIGHT, startTime = 0.85f, endTime = 0.15f },
            new DayStageSettings { stage = DayStage.DAWN, startTime = 0.15f, endTime = 0.25f },
            new DayStageSettings { stage = DayStage.DAY, startTime = 0.25f, endTime = 0.75f },
            new DayStageSettings { stage = DayStage.DUSK, startTime = 0.75f, endTime = 0.85f }
        };
        [Space]
        [Header("Light References")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private Gradient lightColor;
        [SerializeField] private AnimationCurve lightIntensity = AnimationCurve.Linear(0, 0.1f, 1, 1.0f);
        [Space]
        [Header("Skybox Control")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private AnimationCurve skyExposure = AnimationCurve.Linear(0, 0.1f, 1, 1.0f);

        private int currentDay = 1;
        private DayStage currentDayStage;

        public event Action<float> OnTimeChanged;
        public event Action<DayStage> OnDayStageChanged;
        public event Action<int> OnDayAdvanced;

        public int InitializationOrder => 1;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);

            OnDayStageChanged = null;
            OnDayAdvanced = null;
        }

        public void Initialize() {
            GameManager.instance?.Register(this as IUpdatable);

            OnDayStageChanged += stage => Debug.Log($"Day stage changed to: {stage}");;
            OnDayAdvanced += advanced => Debug.Log($"New day started: Day {advanced}");

            currentDayStage = GetDayStage(currentTimeOfDay);

            UpdateLighting();
            UpdateSkybox();
        }

        public void OnUpdate(float deltaTime) {
            if (GameManager.instance?.GetGameState() != GameState.PLAYING) return;

            var previousTime = currentTimeOfDay;
            currentTimeOfDay += deltaTime / dayDurationInSeconds;

            if (currentTimeOfDay >= 1.0f) {
                currentTimeOfDay = 0.0f;
                currentDay++;
                OnDayAdvanced?.Invoke(currentDay);
            }

            var previousStage = GetDayStage(previousTime);
            var currentStage = GetCurrentDayStage;

            if (previousStage != currentStage)
                OnDayStageChanged?.Invoke(currentStage);

            UpdateLighting();
            UpdateSkybox();

            OnTimeChanged?.Invoke(currentTimeOfDay);
        }

        private void UpdateLighting() {
            if (directionalLight == null) return;

            directionalLight.color = lightColor.Evaluate(currentTimeOfDay);
            directionalLight.intensity = lightIntensity.Evaluate(currentTimeOfDay);

            var sunAngle = currentTimeOfDay * 360.0f - 90.0f;
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170.0f, 0.0f);
        }

        private void UpdateSkybox() {
            if (skyboxMaterial == null) return;

            skyboxMaterial.SetVector("_MainLightDirection", directionalLight.transform.forward);

            skyboxMaterial.SetFloat("_StarIntensity", CalculateNightIntensity(currentTimeOfDay, 50.0f));
            skyboxMaterial.SetFloat("_AuroraIntensity", CalculateNightIntensity(currentTimeOfDay, 4.5f));
        }

        public DayStage GetDayStage(float time) {
            foreach (var stage in dayStages)
                if (stage.ContainsTime(time))
                    return stage.stage;
            return DayStage.NIGHT; // fallback
        }

        public DayStageSettings GetStageSettings(DayStage stage) {
            foreach (var settings in dayStages)
                if (settings.stage == stage)
                    return settings;
            return dayStages[0]; // fallback
        }

        public float GetStageProgress(float time) {
            var settings = GetStageSettings(GetDayStage(time));

            return settings.GetProgress(time);
        }

        public void SetTimeOfDay(float time) {
            currentTimeOfDay = Mathf.Clamp01(time);

            UpdateLighting();
            UpdateSkybox();
        }

        public void SetDayDuration(float seconds) {
            dayDurationInSeconds = Mathf.Max(1f, seconds);
        }

        private float CalculateNightIntensity(float time, float amplitude) {
            var nightSettings = GetStageSettings(DayStage.NIGHT);
            if (!nightSettings.ContainsTime(time))
                return 0f;

            float phase;
            if (nightSettings.startTime > nightSettings.endTime) {
                float nightDuration = 1f - nightSettings.startTime + nightSettings.endTime;
                if (time >= nightSettings.startTime)
                    phase = (time - nightSettings.startTime) / nightDuration * Mathf.PI;
                else
                    phase = (time + 1f - nightSettings.startTime) / nightDuration * Mathf.PI;
            } else {
                float nightDuration = nightSettings.endTime - nightSettings.startTime;
                phase = (time - nightSettings.startTime) / nightDuration * Mathf.PI;
            }

            float intensity = amplitude * Mathf.Sin(phase); // intensity(t) = A * sin(π * (t - start) / duration)

            return Mathf.Max(0f, intensity);
        }

        public float GetCurrentTimeOfDay => currentTimeOfDay;
        public int GetCurrentDay => currentDay;
        public DayStage GetCurrentDayStage => GetDayStage(currentTimeOfDay);
        public float GetCurrentStageProgress => GetStageProgress(currentTimeOfDay);

        public float GetStageStartTime(DayStage stage) => GetStageSettings(stage).startTime;
        public float GetStageEndTime(DayStage stage) => GetStageSettings(stage).endTime;
        public DayStageSettings[] GetAllStageSettings => dayStages;
    }
}