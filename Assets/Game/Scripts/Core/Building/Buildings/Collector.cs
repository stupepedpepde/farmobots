using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Collector
{
    public class Collector : MonoBehaviour, IInitializable, IUpdatable, IInteractable
    {
        [Header("Collector Settings")]
        [SerializeField] private string collectorName = "Rain Collector";
        [SerializeField] private float maxCapacity = 100f;
        [SerializeField] private float fillRateRain = 0.2f;
        [SerializeField] private float fillRateClear = 0f;
        [SerializeField] private float waterPerFlask = 10f;
        [SerializeField] private float interactionRange = 2f;

        [Header("Water Item")]
        [SerializeField] private ItemDetails waterItemDetails;   // assign "Water" asset

        private float currentWater;
        private bool isRaining;

        public int InitializationOrder => 45;

        private void Awake()
        {
            GameManager.instance?.Register(this as IInitializable);
        }

        private void OnDestroy()
        {
            if (WeatherManager.instance != null)
                WeatherManager.instance.OnWeatherChanged -= OnWeatherChanged;
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
            GameManager.instance?.Unregister(this as IInteractable);
        }

        public void Initialize()
        {
            currentWater = 0f;

            if (WeatherManager.instance != null)
            {
                WeatherManager.instance.OnWeatherChanged += OnWeatherChanged;
                OnWeatherChanged(WeatherManager.instance.GetCurrentWeather);
            }

            GameManager.instance?.Register(this as IUpdatable);
            GameManager.instance?.Register(this as IInteractable);
        }

        private void OnWeatherChanged(WeatherType weather)
        {
            isRaining = (weather == WeatherType.RAIN);
        }

        public void OnUpdate(float deltaTime)
        {
            float fillRate = isRaining ? fillRateRain : fillRateClear;
            if (fillRate > 0 && currentWater < maxCapacity)
            {
                currentWater += fillRate * deltaTime;
                if (currentWater > maxCapacity) currentWater = maxCapacity;
            }
        }

        public void OnInteract()
        {
            if (currentWater >= waterPerFlask)
            {
                int flasksToGive = Mathf.FloorToInt(currentWater / waterPerFlask);
                float waterToConsume = flasksToGive * waterPerFlask;
                currentWater -= waterToConsume;

                if (waterItemDetails == null)
                {
                    Debug.LogError("Water ItemDetails not assigned on Collector!");
                    return;
                }

                Item waterItem = waterItemDetails.Create(flasksToGive);
                InventoryService.PlayerInventory?.TryAddItem(waterItem);
                Debug.Log($"Collected {flasksToGive} water from {collectorName}");
            }
            else
            {
                Debug.Log($"Not enough water in {collectorName}. Need {waterPerFlask}, have {currentWater:F1}");
            }
        }

        public float GetInteractionRange() => interactionRange;

        public string GetInteractionPrompt()
        {
            int flasksReady = GetAvailableFlasks();

            if (flasksReady > 0)
                return $"Press E to collect {flasksReady} water";
            else
                return $"Collecting water...\n1 flask per 20 water";
        }

        // --- Public accessors for HUDManager ---
        public float GetFillPercentage() => currentWater / maxCapacity;
        public int GetAvailableFlasks() => Mathf.FloorToInt(currentWater / waterPerFlask);
        public float GetCurrentWater() => currentWater;
        public float GetMaxWater() => maxCapacity;
        public float GetWaterPerFlask() => waterPerFlask;
    }
}