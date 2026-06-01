using System;
using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Building.Buildings;
using JetBrains.Annotations;
using UnityEngine;

namespace Game.Scripts.Inventory {
    public enum InventoryMode {
        SINGLE,
        DUAL,
        CRAFTING
    }

    public class InventoryManager : MonoBehaviour, IInitializable {
        public static InventoryManager instance { get; private set; }

        [SerializeField] private bool debugMode = false;

        private InventoryMode currentMode;
        private InventoryComponent primaryInventory;
        private InventoryComponent secondaryInventory;
        private InventoryPopup inventoryPopup;
        private bool isInventoryOpen = false;

        public event Action OnInventoryOpened;
        public event Action OnInventoryClosed;
        public event Action<InventoryComponent, InventoryComponent> OnInventoriesShouldDisplay;

        public int InitializationOrder => 45;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            GameEvents.OnInventoryRequested += HandleInventoryRequest;
        }

        private void OnDestroy() {
            GameEvents.OnInventoryRequested -= HandleInventoryRequest;
            GameManager.instance?.Unregister(this as IInitializable);
        }

        private void HandleInventoryRequest([CanBeNull] InventoryComponent inventory) {
            var playerInventory = InventoryService.PlayerInventory;

            if (inventory == null) {
                ToggleInventory();
                return;
            }

            if (debugMode) Debug.Log($"Inventory requested: {inventory.GetDisplayName()}");

            if (isInventoryOpen && primaryInventory == inventory) {
                ToggleInventory();
                return;
            }

            ShowInventory(playerInventory, inventory);
        }

        public void ShowInventory(InventoryComponent inventory, [CanBeNull] InventoryComponent secondary = null) {
            if (inventory == null) return;

            if (debugMode) Debug.Log($"ShowInventory called: {inventory.GetDisplayName()}");

            currentMode = secondary == null ? InventoryMode.SINGLE : InventoryMode.DUAL;
            primaryInventory = inventory;
            secondaryInventory = secondary;

            if (inventoryPopup == null)
                inventoryPopup = new InventoryPopup();

            inventoryPopup.DisplayInventories(primaryInventory, secondaryInventory);
            inventoryPopup.Show();
            isInventoryOpen = inventoryPopup.IsOpen;

            if (isInventoryOpen)
                OnInventoryOpened?.Invoke();
            else
                OnInventoryClosed?.Invoke();
        }

        public void ShowCraftingInventory(InventoryComponent playerInventory, LabTable labTable)
        {
            if (playerInventory == null || labTable == null) return;

            currentMode = InventoryMode.CRAFTING;
            primaryInventory = playerInventory;
            secondaryInventory = labTable.CraftingInventory; // crafting grid inventory

            if (inventoryPopup == null)
                inventoryPopup = new InventoryPopup();

            inventoryPopup.SetCraftingData(labTable.Recipes, labTable.CraftingInventory);
            inventoryPopup.DisplayInventories(primaryInventory, secondaryInventory);
            inventoryPopup.Show();
            isInventoryOpen = inventoryPopup.IsOpen;

            if (isInventoryOpen)
                OnInventoryOpened?.Invoke();
            else
                OnInventoryClosed?.Invoke();
        }

        public void ToggleInventory() {
            if (inventoryPopup == null)
                inventoryPopup = new InventoryPopup();

            if (!inventoryPopup.IsOpen) {
                var playerInventory = InventoryService.PlayerInventory;
                if (playerInventory == null) return;

                primaryInventory = playerInventory;
                secondaryInventory = null;
                currentMode = InventoryMode.SINGLE;
                inventoryPopup.DisplayInventories(primaryInventory, null);
            }

            inventoryPopup.Toggle();
            isInventoryOpen = inventoryPopup.IsOpen;

            if (isInventoryOpen)
                OnInventoryOpened?.Invoke();
            else
                OnInventoryClosed?.Invoke();
        }

        public void SetVisible(bool visible) {
            if (!visible && isInventoryOpen) {
                inventoryPopup?.Toggle();
                isInventoryOpen = false;
                OnInventoryClosed?.Invoke();
            }
        }

        public InventoryMode GetCurrentMode() => currentMode;
    }
}