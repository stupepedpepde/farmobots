using UnityEngine;
using Game.Scripts.Inventory.Items;

namespace Game.Scripts.Core.Environment.Terrain.Node {
    [CreateAssetMenu(fileName = "NodeType", menuName = "ScriptableObjects/NodeType")]
    public class NodeType : ScriptableObject {
        [Header("Visual & Prefab")]
        public GameObject prefab;
        [Space]
        [Header("Loot")]
        public ItemDetails[] possibleDrops;
        public int minQuantity = 1;
        public int maxQuantity = 2;
        [Space]
        [Header("Mining")]
        public float miningTime = 2.0f;
        [Space]
        [Header("Spawn Constraints")]
        public float minHeight = 0f;
        public float maxHeight = 0f;
        public float minDistanceFromSameType = 10f;
        [Space]
        [Header("Buried Node Settings")]
        public bool isBuried = false;
        public float revealRadius = 5f;
        public GameObject buriedVisualPrefab;
    }
}