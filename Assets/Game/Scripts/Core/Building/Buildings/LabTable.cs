using System.Collections.Generic;
using Game.Scripts.Inventory;
using UnityEngine;

namespace Game.Scripts.Core.Building.Buildings
{
    public class LabTable : MonoBehaviour, IInitializable, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string tableName = "Lab Table";
        [SerializeField] private float interactionRange = 2.5f;
        [Space]
        [Header("Crafting Inventory")]
        [SerializeField] private InventoryComponent craftingInventory;
        [SerializeField] private int craftingInventorySize = 3;
        [Space]
        [Header("Crafting Recipes")]
        [SerializeField] private List<CraftingRecipe> availableRecipes = new List<CraftingRecipe>();

        public string GetDisplayName() => tableName;
        public float GetInteractionRange() => interactionRange;
        public string GetInteractionPrompt() => $"Use {tableName}";
        public int InitializationOrder => 46;

        public InventoryComponent CraftingInventory => craftingInventory;
        public List<CraftingRecipe> Recipes => availableRecipes;

        private void Awake()
        {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize()
        {
            GameManager.instance?.Register(this as IInteractable);

            if (craftingInventory == null)
            {
                craftingInventory = InventoryBuilder.Create(gameObject, $"{tableName} Crafting")
                    .WithCapacity(craftingInventorySize)
                    .WithRows(1) // single row for simplicity
                    .Build();
            }
            InventoryService.Register(craftingInventory);
        }

        public void OnInteract()
        {
            var playerInv = InventoryService.PlayerInventory;
            if (playerInv == null)
            {
                Debug.LogError("LabTable: Player inventory not found!");
                return;
            }

            var invManager = InventoryManager.instance;
            if (invManager != null)
                invManager.ShowCraftingInventory(playerInv, this);
            else
                Debug.LogError("LabTable: InventoryManager not found!");
        }

        private void OnDestroy()
        {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IInteractable);
            InventoryService.Unregister(craftingInventory);
        }
    }
}