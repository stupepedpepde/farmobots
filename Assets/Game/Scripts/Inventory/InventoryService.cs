using System;
using System.Collections.Generic;
using Game.Scripts.Inventory.Helpers;
using Game.Scripts.Inventory.Items;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.Inventory {
    public static class InventoryService {
        private static readonly Dictionary<Guid, InventoryComponent> inventories = new Dictionary<Guid, InventoryComponent>();
        private static InventoryComponent playerInventory;

        public static event Action<InventoryComponent> OnInventoryRegistered;
        public static event Action<InventoryComponent> OnInventoryUnregistered;
        public static event Action<Guid> OnInventoryOpened;
        public static event Action<Guid> OnInventoryClosed;

        public static InventoryComponent PlayerInventory {
            get => playerInventory;
            set {
                playerInventory = value;
                if (value != null) Register(value);
            }
        }

        public static void Register(InventoryComponent inventory) {
            if (inventory == null || inventories.ContainsKey(inventory.GetID())) return;

            inventories[inventory.GetID()] = inventory;
            inventory.OnInventoryOpened += () => OnInventoryOpened?.Invoke(inventory.GetID());
            inventory.OnInventoryClosed += () => OnInventoryClosed?.Invoke(inventory.GetID());

            OnInventoryRegistered?.Invoke(inventory);
        }

        public static void Unregister(InventoryComponent inventory) {
            if (inventory == null) return;

            inventories.Remove(inventory.GetID());
            OnInventoryUnregistered?.Invoke(inventory);
        }

        // getters
        public static InventoryComponent GetInventory(Guid ID) => inventories.TryGetValue(ID, out var inventory) ? inventory : null;
        public static IEnumerable<InventoryComponent> GetAllInventories() => inventories.Values;
    }
}