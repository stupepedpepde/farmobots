using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Planting;
using UnityEngine;

namespace Game.Scripts.Plants {
    public class Plant : MonoBehaviour, IInitializable, IInteractable {
        [SerializeField] private PlantSO plant;

        private int currentStage;
        private float currentTime;
        private GameObject currentPlant;

        private float currentWater;
        private float maxWater;
        private bool requiresWatering;
        private float waterDrainRate;

        public string GetInteractionPrompt() {
            if (!requiresWatering) return "Fully watered (no water needed)";
            if (currentWater >= maxWater) return "Fully watered ✓";
            float percent = (currentWater / maxWater) * 100f;
            return $"Water needed ({percent:F0}%)\nPress E to water (requires Ice)";
        }

        public float GetInteractionRange() => 1.5f;
        public void OnInteract() {
            if (!requiresWatering || currentWater >= maxWater) {
                Debug.Log("Plant doesn't need water.");
                return;
            }

            var playerInv = InventoryService.PlayerInventory;
            if (playerInv == null) return;

            Item ice = null;
            for (int i = 0; i < playerInv.GetCapacity(); i++) {
                var item = playerInv.GetItem(i);
                if (item != null && item.details.ItemName.ToLower() == "ice") {
                    ice = item;
                    break;
                }
            }

            if (ice != null && ice.quantity >= 1) {
                if (playerInv.TryConsumeItem(ice, 1)) {
                    Water(100f);
                    Debug.Log("Watered plant with ice.");
                }
            } else {
                Debug.Log("No ice in inventory to water plant.");
            }
        }

        public float GetCurrentWater() => currentWater;
        public float GetWaterPercentage() => requiresWatering ? currentWater / maxWater : 1f;
        public bool IsWaterDeficit() => requiresWatering && (currentWater / maxWater) < 0.1f;
        public float GetMaxWater() => maxWater;
        public bool NeedsWater => requiresWatering && currentWater < maxWater;

        public int InitializationOrder => 40;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            PlantManager.instance?.RegisterPlant(this);

            if (plant != null) {
                maxWater = plant.maxWater;
                requiresWatering = plant.requiresWatering;
                waterDrainRate = plant.waterDrainRate;
                currentWater = 0;
                currentStage = 0;
                currentTime = 0;

                if (plant.GetPlantByStage(currentStage) != null)
                    currentPlant = Instantiate(plant.GetPlantByStage(currentStage), transform);
            }
        }

        private void OnDestroy() {
            PlantManager.instance?.UnregisterPlant(this);
            GameManager.instance?.Unregister(this as IInitializable);
        }

        public void SetPlantSO(PlantSO plantSO) {
            plant = plantSO;
        }

        public void CheckPlant(float deltaTime) {
            if (requiresWatering) {
                DepleteWater(deltaTime);
            }

            if (currentStage >= plant.MaxStage - 1) return;

            if (!CanGrow()) return;

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

        private void DepleteWater(float deltaTime) {
            if (currentWater <= 0) return;
            currentWater -= waterDrainRate * deltaTime;
            if (currentWater < 0) currentWater = 0;
        }

        private bool CanGrow() {
            if (requiresWatering && (currentWater / maxWater) < 0.1f) return false;

            if (AtmosphereManager.instance != null) {
                float o2 = AtmosphereManager.instance.OxygenPercentage;
                float co2 = AtmosphereManager.instance.CarbonPercentage;
                float n2 = AtmosphereManager.instance.NitrogenPercentage;

                if (o2 < plant.oxygenMin || o2 > plant.oxygenMax) return false;
                if (co2 < plant.carbonMin || co2 > plant.carbonMax) return false;
                if (n2 < plant.nitrogenMin || n2 > plant.nitrogenMax) return false;
            }
            return true;
        }

        public void Water(float amount) {
            if (!requiresWatering) return;
            currentWater = Mathf.Min(maxWater, currentWater + amount);
        }

        public bool IsGrown() => currentStage == plant.MaxStage - 1;
        public bool IsAlive() => !IsGrown();

        public float GetOxygenProduction() {
            if (requiresWatering && (currentWater / maxWater) < 0.1f) return 0f;
            return plant != null ? plant.oxygenProduction * 0.1f : 0f;
        }

        public float GetCarbonConsumption() {
            if (requiresWatering && (currentWater / maxWater) < 0.1f) return 0f;
            return plant != null ? plant.carbonConsumption * 0.1f : 0f;
        }

        public float GetHarvestTime() => plant != null ? plant.harvestTime : 1.5f;
        public List<HarvestDrop> GetHarvestDrops() => plant != null ? plant.harvestDrops : new List<HarvestDrop>();
    }
}