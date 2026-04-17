using UnityEngine;

namespace Game.Scripts.Inventory {
    public class InventoryBuilder {
        private InventoryComponent inventory;
        private InventoryConfiguration configuration;

        private InventoryBuilder(InventoryComponent inventory,  InventoryConfiguration configuration) {
            this.inventory = inventory;
            this.configuration = configuration;
        }

        public static InventoryBuilder Create(GameObject owner, string name = "Inventory") {
            var inventory = owner.AddComponent<InventoryComponent>();
            var configuration = ScriptableObject.CreateInstance<InventoryConfiguration>();

            configuration.name = $"{owner.name}_InventoryConfig";
            inventory.name = name;

            configuration.inventoryName = name;

            return new InventoryBuilder(inventory, configuration);
        }

        public InventoryBuilder WithCapacity(int capacity) {
            configuration.capacity = Mathf.Max(1, capacity);
            return this;
        }

        public InventoryBuilder WithRows(int rows) {
            configuration.rows = Mathf.Max(1, rows);
            return this;
        }

        public InventoryBuilder AutoSort(bool autoSort = true) {
            configuration.autoSort = autoSort;
            return this;
        }

        public InventoryBuilder PersistAcrossScenes(bool persist = true) {
            configuration.persistAcrossScenes = persist;
            return this;
        }

        public InventoryComponent Build() {
            inventory.configuration = configuration;
            inventory.Initialize();

            return inventory;
        }
    }
}