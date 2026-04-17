using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Planting;
using UnityEngine;

namespace Game.Scripts.Plants {
    public class Plant : MonoBehaviour, IInitializable {
        [SerializeField] private PlantSO plant;

        private int currentStage;
        private float currentTime;

        private GameObject currentPlant;

        public int InitializationOrder => 40;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            PlantManager.instance?.RegisterPlant(this);

            if (plant != null && plant.GetPlantByStage(currentStage) != null)
                currentPlant = Instantiate(plant.GetPlantByStage(currentStage), transform);
        }

        private void OnDestroy() {
            PlantManager.instance?.UnregisterPlant(this);
            GameManager.instance?.Unregister(this as IInitializable);
        }

        public void SetPlantSO(PlantSO plantSO) {
            plant = plantSO;
        }

        public void CheckPlant(float deltaTime) {
            if (currentStage >= plant.MaxStage - 1) return;

            currentTime += deltaTime;

            if (currentTime >= plant.growthTime) {
                currentStage++;
                currentTime = 0;

                Destroy(currentPlant);
                currentPlant = Instantiate(plant.GetPlantByStage(currentStage), transform);

                if (currentStage == plant.MaxStage - 1)
                    PlantManager.instance?.UnregisterPlant(this);
            }
        }

        public bool IsGrown() => currentStage == plant.MaxStage - 1;

        public bool IsAlive() => !IsGrown();

        public float GetOxygenProduction() => plant != null ? plant.oxygenProduction * 0.1f : 0f;

        public float GetHarvestTime() => plant != null ? plant.harvestTime : 1.5f;

        public List<HarvestDrop> GetHarvestDrops() => plant != null ? plant.harvestDrops : new List<HarvestDrop>();
    }
}