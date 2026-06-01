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

        // Harvest expiry
        private float harvestExpiryTime;
        private float harvestTimeRemaining;
        private bool isExpired;

        // Reference to the spot this plant occupies
        private PlantableSpot occupiedSpot;

        public string GetInteractionPrompt() {
            if (IsGrown()) {
                if (isExpired) return "Plant has died (no harvest)";
                return "Press E to harvest";
            }
            if (!requiresWatering) return "Growing (no water needed)";
            if (currentWater >= maxWater) return "Fully watered ✓";
            float percent = (currentWater / maxWater) * 100f;
            return $"Water needed ({percent:F0}%)\nPress E to water (requires water)";
        }

        public float GetInteractionRange() => 1.5f;

        public void OnInteract() {
            if (IsGrown() && !isExpired) {
                Harvest();
                return;
            }
            // If not fully grown, water if needed
            if (!requiresWatering || currentWater >= maxWater) {
                Debug.Log("Plant doesn't need water.");
                return;
            }

            var playerInv = InventoryService.PlayerInventory;
            if (playerInv == null) return;

            Item water = null;
            for (int i = 0; i < playerInv.GetCapacity(); i++) {
                var item = playerInv.GetItem(i);
                if (item != null && item.details.ItemName.ToLower() == "water") {
                    water = item;
                    break;
                }
            }

            if (water != null && water.quantity >= 1) {
                if (playerInv.TryConsumeItem(water, 1)) {
                    Water(100f);
                    Debug.Log("Watered plant.");
                }
            } else {
                Debug.Log("No water in inventory to water plant.");
            }
        }

        public float GetCurrentWater() => currentWater;
        public float GetWaterPercentage() => requiresWatering ? currentWater / maxWater : 1f;
        public bool IsWaterDeficit() => requiresWatering && (currentWater / maxWater) < 0.1f;
        public float GetMaxWater() => maxWater;
        public bool NeedsWater => requiresWatering && currentWater < maxWater && !IsGrown();

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
                harvestExpiryTime = plant.harvestExpiryTime;
                harvestTimeRemaining = harvestExpiryTime;
                isExpired = false;

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

        public void SetOccupiedSpot(PlantableSpot spot) {
            occupiedSpot = spot;
        }

        public void CheckPlant(float deltaTime) {
            // Deplete water over time
            if (requiresWatering && currentWater > 0) {
                currentWater -= waterDrainRate * deltaTime;
                if (currentWater < 0) currentWater = 0;
            }

            // Handle expiry if fully grown
            if (IsGrown() && !isExpired && harvestExpiryTime > 0) {
                harvestTimeRemaining -= deltaTime;
                if (harvestTimeRemaining <= 0f) {
                    Die();
                    return;
                }
                return; // No further growth once fully grown
            }

            if (currentStage >= plant.MaxStage - 1) return;

            if (!CanGrow()) return;

            currentTime += deltaTime;
            if (currentTime >= plant.growthTime) {
                currentStage++;
                currentTime = 0;

                Destroy(currentPlant);
                currentPlant = Instantiate(plant.GetPlantByStage(currentStage), transform);

                if (currentStage == plant.MaxStage - 1) {
                    harvestTimeRemaining = harvestExpiryTime;
                }
            }
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
        public bool IsAlive() => !IsGrown() && !isExpired;

        public float GetOxygenProduction() {
            if (requiresWatering && (currentWater / maxWater) < 0.1f) return 0f;
            if (IsGrown() && !isExpired) return plant != null ? plant.oxygenProduction * 0.1f : 0f;
            if (!IsGrown() && IsAlive()) return plant != null ? plant.oxygenProduction * 0.05f : 0f;
            return 0f;
        }

        public float GetCarbonConsumption() {
            if (requiresWatering && (currentWater / maxWater) < 0.1f) return 0f;
            if (IsGrown() && !isExpired) return plant != null ? plant.carbonConsumption * 0.1f : 0f;
            if (!IsGrown() && IsAlive()) return plant != null ? plant.carbonConsumption * 0.05f : 0f;
            return 0f;
        }

        public float GetHarvestTime() => plant != null ? plant.harvestTime : 1.5f;
        public List<HarvestDrop> GetHarvestDrops() => plant != null ? plant.harvestDrops : new List<HarvestDrop>();

        private void Harvest() {
            if (isExpired) {
                Debug.Log("Plant already died, nothing to harvest.");
                return;
            }
            if (!IsGrown()) {
                Debug.Log("Plant not fully grown yet.");
                return;
            }

            // Give drops to player's inventory
            var drops = GetHarvestDrops();
            var playerInv = InventoryService.PlayerInventory;
            if (playerInv != null) {
                foreach (var drop in drops) {
                    int qty = Random.Range(drop.minQuantity, drop.maxQuantity + 1);
                    Item item = drop.itemDetails.Create(qty);
                    playerInv.TryAddItem(item);
                    Debug.Log($"Harvested {qty} x {drop.itemDetails.ItemName}");
                }
            }

            // Atmospheric burst
            if (AtmosphereManager.instance != null) {
                AtmosphereManager.instance.AddOxygen(plant.harvestOxygenBoost);
                AtmosphereManager.instance.RemoveCarbon(plant.harvestCarbonReduction);
                Debug.Log($"Harvest burst: +{plant.harvestOxygenBoost} O₂, -{plant.harvestCarbonReduction} CO₂");
            }

            // Clear the plant and spot
            Die();
        }

        private void Die() {
            if (isExpired) return;

            isExpired = true;
            Debug.Log($"{name} has died.");

            if (occupiedSpot != null) {
                occupiedSpot.Clear();
            } else {
                var spot = GetComponentInParent<PlantableSpot>();
                if (spot != null) spot.Clear();
            }

            Destroy(gameObject);
        }
    }
}