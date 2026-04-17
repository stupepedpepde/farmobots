using System;
using System.Linq;
using Game.Scripts.Inventory.Helpers;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Inventory {
    public class InventoryComponent : MonoBehaviour {
        [SerializeField] private Guid ID = Guid.NewGuid();
        [SerializeField] public InventoryConfiguration configuration;

        private ObservableArray<Item> items;

        // events
        public event Action<Item, int> OnItemAdded;
        public event Action<Item, int> OnItemRemoved;
        public event Action<Item, int> OnItemChanged;
        public event Action<Item, int, int> OnItemMoved;
        public event Action OnInventoryOpened;
        public event Action OnInventoryClosed;

        private void Awake() {
            Initialize();
            InventoryService.Register(this);
        }

        private void OnDestroy() {
            InventoryService.Unregister(this);
            // Unsubscribe from all item events
            for (int i = 0; i < items.Length; i++) {
                if (items[i] != null) {
                    items[i].ValueChanged -= OnItemValueChanged;
                }
            }
        }

        public void Initialize() {
            if (configuration == null) {
                configuration = ScriptableObject.CreateInstance<InventoryConfiguration>();
                configuration.Initialize();
            }

            items = new ObservableArray<Item>(configuration.GetCapacity());

            items.ValueChanged += (item, index) => {
                // This handles when an item is added or removed from a slot
                if (item == null) {
                    OnItemRemoved?.Invoke(null, index);
                } else {
                    OnItemAdded?.Invoke(item, index);
                    // Subscribe to item's own value changes
                    item.ValueChanged += OnItemValueChanged;
                }
            };
        }

        private void OnItemValueChanged(Item item) {
            // Find the slot containing this item and fire OnItemChanged
            for (int i = 0; i < items.Length; i++) {
                if (items[i] == item) {
                    OnItemChanged?.Invoke(item, i);
                    break;
                }
            }
        }

        # region Item Manipulation

        public bool TryDropItem(int from, int to = -1, int quantity = 1) {
            if (from < 0 || from >= GetCapacity()) return false;

            var draggedItem = GetItem(from);
            if (draggedItem == null || draggedItem.quantity <= 0) return false;

            if (to == -1) {
                var tempItem = draggedItem.Copy();
                tempItem.quantity = quantity;

                if (TryStackItem(tempItem)) {
                    if (tempItem.quantity <= 0) {
                        TryRemoveItem(from, quantity);
                        return true;
                    }

                    draggedItem = tempItem;
                    quantity = draggedItem.quantity;
                }

                for (int i = 0; i < GetCapacity(); i++)
                    if (GetItem(i) == null)
                        return TryMoveItem(from, i, quantity);

                return false;
            }

            return TryMoveItem(from, to, quantity);
        }

        public bool TryMoveItem(int from, int to, int quantity = 1) {
            if (from == to) return false;

            var draggedItem = GetItem(from);
            var targetItem = GetItem(to);

            if (draggedItem == null) return false;

            quantity = Mathf.Min(quantity, draggedItem.quantity);

            if (targetItem == null) {
                if (quantity < draggedItem.quantity) {
                    var partialItem = draggedItem.Copy();
                    partialItem.quantity = quantity;

                    // Remove old item from event before replacing?
                    // We'll handle subscription in the setter via ObservableArray
                    items[to] = partialItem;
                    draggedItem.quantity -= quantity;
                    draggedItem.NotifyValueChanged(); // Will fire OnItemChanged via OnItemValueChanged
                } else {
                    items[to] = draggedItem;
                    items[from] = null;
                    OnItemMoved?.Invoke(draggedItem, from, to);
                }
                return true;
            } else if (targetItem.details.ID.Equals(draggedItem.details.ID)) {
                return TryCombineItems(from, to) > 0;
            } else {
                TrySwapItems(from, to);
                return true;
            }
        }

        public bool CanAddToSlot(Item item, int slot) {
            if (slot < 0 || slot >= GetCapacity() || item == null) return false;

            if (items[slot] == null) return true;
            if (items[slot].details.ID.Equals(item.details.ID)) {
                int space = items[slot].details.MaxStack - items[slot].quantity;
                return space >= item.quantity;
            }
            return false;
        }

        public bool TryAddItem(Item item, int preferredSlot = -1) {
            if (item == null || item.quantity <= 0) return false;

            if (preferredSlot >= 0 && preferredSlot < GetCapacity()) {
                var existing = items[preferredSlot];
                if (existing == null) {
                    return AddToSlot(item, preferredSlot);
                } else if (existing.details.ID.Equals(item.details.ID)) {
                    int space = existing.details.MaxStack - existing.quantity;
                    if (space > 0) {
                        int toAdd = Mathf.Min(space, item.quantity);
                        existing.quantity += toAdd;
                        item.quantity -= toAdd;
                        existing.NotifyValueChanged(); // Triggers OnItemValueChanged
                        if (item.quantity <= 0) return true;
                    }
                }
            }

            if (TryStackItem(item)) return true;

            for (int i = 0; i < GetCapacity(); i++)
                if (items[i] == null)
                    return AddToSlot(item, i);

            return false;
        }

        public bool TryStackItem(Item item) {
            for (int i = 0; i < GetCapacity(); i++) {
                if (items[i] != null && items[i].details.ID.Equals(item.details.ID) && CanAddToSlot(item, i)) {
                    int space = items[i].details.MaxStack - items[i].quantity;
                    if (space > 0) {
                        int toAdd = Mathf.Min(space, item.quantity);
                        items[i].quantity += toAdd;
                        item.quantity -= toAdd;

                        items[i].NotifyValueChanged(); // Triggers OnItemValueChanged

                        if (item.quantity <= 0) return true;
                    }
                }
            }
            return false;
        }

        private bool AddToSlot(Item item, int slot) {
            if (items[slot] != null) return false;
            items[slot] = item;
            // ObservableArray will fire OnItemAdded and we'll subscribe there
            return true;
        }

        public bool TryRemoveItem(int slot, int quantity = 1) {
            if (slot < 0 || slot >= GetCapacity() || items[slot] == null) return false;

            var item = items[slot];
            item.quantity -= quantity;

            if (item.quantity <= 0) {
                // Unsubscribe before removal
                item.ValueChanged -= OnItemValueChanged;
                items[slot] = null; // ObservableArray fires OnItemRemoved
            } else {
                item.NotifyValueChanged(); // Triggers OnItemValueChanged
            }

            return true;
        }

        public void TrySwapItems(int from, int to) {
            if (from < 0 || from >= GetCapacity() || to < 0 || to >= GetCapacity()) return;

            var fromItem = items[from];
            var toItem = items[to];

            if (!CanAddToSlot(fromItem, to) || !CanAddToSlot(toItem, from)) return;

            // Unsubscribe old items? No, we keep same items just move them
            items[from] = toItem;
            items[to] = fromItem;

            OnItemChanged?.Invoke(items[from], from);
            OnItemChanged?.Invoke(items[to], to);
        }

        public int TryCombineItems(int source, int target) {
            if (source < 0 || source >= GetCapacity() || target < 0 || target >= GetCapacity())
                return 0;

            var sourceItem = items[source];
            var targetItem = items[target];

            if (sourceItem == null || targetItem == null || !sourceItem.details.ID.Equals(targetItem.details.ID))
                return 0;

            int space = targetItem.details.MaxStack - targetItem.quantity;
            if (space <= 0) return 0;

            int toTransfer = Mathf.Min(space, sourceItem.quantity);
            targetItem.quantity += toTransfer;
            sourceItem.quantity -= toTransfer;

            if (sourceItem.quantity <= 0) {
                sourceItem.ValueChanged -= OnItemValueChanged;
                items[source] = null;
                OnItemRemoved?.Invoke(null, source);
            } else {
                sourceItem.NotifyValueChanged();
                // OnItemChanged will be called via OnItemValueChanged
            }

            targetItem.NotifyValueChanged();
            // OnItemChanged will be called via OnItemValueChanged
            return toTransfer;
        }

        public bool TryConsumeItem(Item item, int quantity = 1) {
            if (!HasItem(item, quantity)) return false;

            int toConsume = quantity;

            for (int i = 0; i < GetCapacity(); i++) {
                if (toConsume <= 0) break;

                var tempItem = GetItem(i);
                if (tempItem != null && item.details.ID.Equals(tempItem.details.ID)) {
                    int consumeFromThisSlot = Mathf.Min(tempItem.quantity, toConsume);

                    if (tempItem.quantity == consumeFromThisSlot) {
                        tempItem.ValueChanged -= OnItemValueChanged;
                        items[i] = null;
                        OnItemRemoved?.Invoke(null, i);
                    } else {
                        tempItem.quantity -= consumeFromThisSlot;
                        tempItem.NotifyValueChanged();
                    }

                    toConsume -= consumeFromThisSlot;
                }
            }

            return toConsume == 0;
        }

        public bool HasItem(Item item, int quantity = 1) {
            if (item == null || quantity <= 0) return false;

            int total = 0;
            for (int i = 0; i < GetCapacity(); i++) {
                var tempItem = GetItem(i);
                if (tempItem != null && tempItem.details.ID.Equals(item.details.ID)) {
                    total += tempItem.quantity;
                    if (total >= quantity) return true;
                }
            }

            return false;
        }

        public Item GetItem(int slot) => slot >= 0 && slot < GetCapacity() ? items[slot] : null;

        public bool TryTransferItem(int fromSlot, InventoryComponent targetInventory, int toSlot = -1, int quantity = -1) {
            var item = GetItem(fromSlot);
            if (item == null) return false;

            if (quantity < 0) quantity = item.quantity;
            quantity = Mathf.Min(quantity, item.quantity);

            if (targetInventory == this)
                return TryMoveItem(fromSlot, toSlot, quantity);

            var targetItem = toSlot >= 0 ? targetInventory.GetItem(toSlot) : null;

            if (targetItem == null) {
                var movedItem = item.Copy();
                movedItem.quantity = quantity;
                if (targetInventory.TryAddItem(movedItem, toSlot)) {
                    TryRemoveItem(fromSlot, quantity);
                    return true;
                }
                return false;
            } else if (targetItem.details.ID.Equals(item.details.ID)) {
                int space = targetItem.details.MaxStack - targetItem.quantity;
                if (space <= 0) return false;
                int transfer = Mathf.Min(space, quantity);
                targetItem.quantity += transfer;
                TryRemoveItem(fromSlot, transfer);
                targetItem.NotifyValueChanged();
                return true;
            } else {
                if (toSlot >= 0 && targetInventory.CanAddToSlot(item, toSlot) && CanAddToSlot(targetItem, fromSlot)) {
                    targetInventory.TryRemoveItem(toSlot, targetItem.quantity);
                    TryRemoveItem(fromSlot, item.quantity);
                    targetInventory.TryAddItem(item, toSlot);
                    TryAddItem(targetItem, fromSlot);
                    return true;
                }
                return false;
            }
        }

        # endregion

        // getters
        public Guid GetID() => ID;
        public string GetDisplayName() => configuration.GetName();
        public int GetCapacity() => items?.Length ?? 0;
        public InventoryConfiguration GetConfiguration() => configuration;
        public Item[] GetAllItems() {
            var result = new Item[items.Length];
            for (int i = 0; i < items.Length; i++)
                result[i] = items[i];

            return result;
        }
    }
}