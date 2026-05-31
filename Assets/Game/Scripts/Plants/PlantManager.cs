using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using UnityEngine;
using IInitializable = Game.Scripts.Core.IInitializable;

namespace Game.Scripts.Plants {
    public class PlantManager : MonoBehaviour, IInitializable, IUpdatable {
        private List<Plant> plants = new List<Plant>();
        public static PlantManager instance { get; private set; }

        public int InitializationOrder => 4;
        
        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
    
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
        }

        public void OnUpdate(float deltaTime) {
            float totalOxygenAddition = 0f;
            float totalCarbonRemoval = 0f;
            bool isRaining = WeatherManager.instance != null && WeatherManager.instance.GetCurrentWeather == WeatherType.RAIN;
            float rainWaterRate = 1f;

            for (int i = plants.Count - 1; i >= 0; i--) {
                var plant = plants[i];
                plant.CheckPlant(deltaTime);

                if (isRaining && plant.NeedsWater)
                    plant.Water(rainWaterRate * deltaTime);

                if (plant.IsGrown()) {
                    totalOxygenAddition += plant.GetOxygenProduction() * deltaTime;
                    totalCarbonRemoval += plant.GetCarbonConsumption() * deltaTime;
                } else if (plant.IsAlive()) {
                    totalOxygenAddition += plant.GetOxygenProduction() * 0.5f * deltaTime;
                    totalCarbonRemoval += plant.GetCarbonConsumption() * 0.5f * deltaTime;
                }
            }

            if (totalOxygenAddition != 0f)
                AtmosphereManager.instance?.AddOxygen(totalOxygenAddition);
            if (totalCarbonRemoval != 0f)
                AtmosphereManager.instance?.RemoveCarbon(totalCarbonRemoval);
        }

        public void RegisterPlant(Plant plant) => plants.Add(plant);

        public void UnregisterPlant(Plant plant) => plants.Remove(plant);
    }
}