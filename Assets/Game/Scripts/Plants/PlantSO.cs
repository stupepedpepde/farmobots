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

        public float oxygenProduction = 0.01f;

        public float plantingTime = 2.0f;   // seconds
        public float harvestTime = 1.5f;    // seconds

        [Range(1, 5)] public int plantSize = 1;

        public Vector2Int footprintOffset = Vector2Int.zero;
        public List<HarvestDrop> harvestDrops;

        public int MaxStage => plantPrefabs.Count;

        public GameObject GetPlantByStage(int stage) {
            if (stage >= MaxStage)
                return null;

            return plantPrefabs[stage];
        }
    }
}