using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Player {
    public class PlayerInventory : MonoBehaviour {
        [SerializeField] private InventoryComponent inventoryComponent;
        [SerializeField] private Item item;

        public void Initialize() {
            if (inventoryComponent == null) {
                inventoryComponent = InventoryBuilder.Create(gameObject, "Player Inventory")
                    .WithCapacity(18)
                    .WithRows(3)
                    .AutoSort()
                    .Build();
            }

            inventoryComponent.TryAddItem(item);

            InventoryService.PlayerInventory = inventoryComponent;
        }
    }
}