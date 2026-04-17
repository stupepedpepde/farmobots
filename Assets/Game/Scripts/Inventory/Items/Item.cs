using System;
using Game.Scripts.Inventory.Helpers;

namespace Game.Scripts.Inventory.Items {
    [Serializable]
    public class Item {
        public ItemDetails details;
        public int quantity;

        public event Action<Item> ValueChanged;

        public Item(ItemDetails details, int quantity = 1) {
            this.details = details;
            this.quantity = quantity;
        }

        public Item Copy() => new Item(details, quantity);

        public void NotifyValueChanged() => ValueChanged?.Invoke(this);
    }
}