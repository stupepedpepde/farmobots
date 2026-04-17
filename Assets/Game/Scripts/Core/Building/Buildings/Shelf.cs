using Game.Scripts.Inventory;
using UnityEngine;

namespace Game.Scripts.Core.Building.Buildings {
    public class Shelf : MonoBehaviour, IInitializable, IInteractable {
        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private string shelfName = "Shelf";
        [SerializeField] private float interactionRange = 3.0f;
        [SerializeField] private int capacity = 20;
        [SerializeField] private int rows = 5;

        public int InitializationOrder => 20;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            GameManager.instance?.Register(this as IInteractable);

            if (inventory == null) {
                inventory = InventoryBuilder.Create(gameObject, shelfName)
                    .WithCapacity(capacity)
                    .WithRows(rows)
                    .Build();
            }

            InventoryService.Register(inventory);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IInteractable);

            InventoryService.Unregister(inventory);
        }

        public void OnInteract() {
            GameEvents.RequestInventory(inventory);
        }

        public float GetInteractionRange() => interactionRange;
        public string GetInteractionPrompt() => $"Open {shelfName}";
    }
}