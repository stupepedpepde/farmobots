using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Plants;
using UnityEngine;
using IInitializable = Game.Scripts.Core.IInitializable;

namespace Game.Scripts.Planting {
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

            for (int i = plants.Count - 1; i >= 0; i--) {
                var plant = plants[i];
                plant.CheckPlant(deltaTime);

                if (plant.IsGrown())
                    totalOxygenAddition += plant.GetOxygenProduction() * deltaTime;
                else if (plant.IsAlive())
                    totalOxygenAddition += plant.GetOxygenProduction() * 0.5f * deltaTime;

                AtmosphereManager.instance?.AddOxygen(totalOxygenAddition);
            }
        }

        public void RegisterPlant(Plant plant) => plants.Add(plant);

        public void UnregisterPlant(Plant plant) => plants.Remove(plant);
    }
}