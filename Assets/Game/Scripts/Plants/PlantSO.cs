using System.Collections.Generic;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Plants {
    [System.Serializable]
    public struct HarvestDrop {
        public ItemDetails itemDetails;
        public int minQuantity;
        public int maxQuantity;
    }

    [CreateAssetMenu(fileName = "PlantSO", menuName = "ScriptableObjects/PlantSO")]
    public class PlantSO : ScriptableObject {
        public string plantName;
        public List<GameObject> plantPrefabs;
        public float growthTime;

        [Header("Atmospheric Requirements (%)")]
        [Range(0, 30)] public float oxygenMin = 2f;
        [Range(0, 30)] public float oxygenMax = 5f;
        [Range(0, 30)] public float carbonMin = 5f;
        [Range(0, 30)] public float carbonMax = 9f;
        [Range(0, 100)] public float nitrogenMin = 30f;
        [Range(0, 100)] public float nitrogenMax = 50f;
        [Space]
        [Header("Oxygen & Carbon Exchange")]
        public float oxygenProduction = 0.01f;
        public float carbonConsumption = 0.005f;
        [Space]
        [Header("Planting / Harvest")]
        public float plantingTime = 2.0f;
        public float harvestTime = 1.5f;
        [Space]
        [Header("Watering")]
        public bool requiresWatering = true;
        public float maxWater = 100f;
        public float waterDrainRate = 1f; // per second (new)
        [Space]
        [Header("Size & Placement")]
        [Range(1, 5)] public int plantSize = 1;
        public Vector2Int footprintOffset = Vector2Int.zero;
        public List<HarvestDrop> harvestDrops;

        public int MaxStage => plantPrefabs.Count;

        public GameObject GetPlantByStage(int stage) {
            return (stage >= 0 && stage < plantPrefabs.Count) ? plantPrefabs[stage] : null;
        }
    }
}