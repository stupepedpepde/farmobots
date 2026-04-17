using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Robot {
    public class RobotInventory : MonoBehaviour {
        [SerializeField] private InventoryComponent inventoryComponent;

        public void Initialize() {
            if (inventoryComponent == null) {
                inventoryComponent = InventoryBuilder.Create(gameObject, $"{transform.parent.name} Inventory")
                    .WithCapacity(12)
                    .WithRows(3)
                    .Build();
            }

            InventoryService.Register(inventoryComponent);
        }

        public InventoryComponent GetInventoryComponent() => inventoryComponent;
    }
}