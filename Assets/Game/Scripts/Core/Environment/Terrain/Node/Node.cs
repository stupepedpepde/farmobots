using System;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Robot;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Scripts.Core.Environment.Terrain.Node
{
    public class Node : MonoBehaviour, IInteractable
    {
        [Header("Loot")]
        [SerializeField] private ItemDetails[] possibleDrops;
        [SerializeField] private int minQuantity = 1;
        [SerializeField] private int maxQuantity = 2;
        [SerializeField] private float interactionRange = 10.0f;
        [Header("Mining")]
        [SerializeField] private float miningTime = 2.0f;

        private ItemDetails selectedItem;
        private int quantity;
        private bool isMined = false;

        public float MiningTime => miningTime;

        public void Awake() {
            GameManager.instance?.Register(this as IInteractable);
            RobotManager.instance?.RegisterNode(this);

            if (possibleDrops != null && possibleDrops.Length > 0) {
                selectedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
                quantity = Random.Range(minQuantity, maxQuantity + 1);
            } else
                Debug.LogWarning($"Node {name} has no possible drops configured!");
        }

        public void OnDestroy() {
            GameManager.instance?.Unregister(this as IInteractable);
            RobotManager.instance?.UnregisterNode(this);
        }

        public void OnInteract() {
            if (!HasLoot()) return;
            Item item = CollectLoot();

            var playerInv = InventoryService.PlayerInventory;
            if (playerInv != null) playerInv.TryAddItem(item);
        }

        public Item CollectLoot() {
            if (!HasLoot()) return null;

            Item item = selectedItem.Create(quantity);

            Debug.Log($"Mined {quantity} x {selectedItem.ItemName} from node {name}");

            isMined = true;
            Destroy(gameObject);

            return item;
        }

        public bool HasLoot() => !isMined && selectedItem != null;

        public float GetInteractionRange() => interactionRange;

        public string GetInteractionPrompt() => $"Mine Node";
    }
}