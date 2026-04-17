using System;
using Game.Scripts.Inventory.Helpers;
using UnityEngine;

namespace Game.Scripts.Inventory.Items {
    [CreateAssetMenu(fileName = "New Item", menuName = "ScriptableObjects/Inventory/Item")]
    public class ItemDetails : ScriptableObject {
        public Guid ID = Guid.NewGuid();
        [SerializeField] private string itemName = "New Item";
        [SerializeField] private Sprite icon;
        [SerializeField] private int maxStack = 20;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private bool isSeed = false;

        public string ItemName => itemName;

        public Sprite Icon => icon;

        public int MaxStack => maxStack;

        public string Description => description;

        public bool IsSeed => isSeed;

        private void AssignNewGuid() => ID = Guid.NewGuid();

        public Item Create(int quantity) => new Item(this, quantity);

        private void OnValidate() {
            if (maxStack < 1)
                maxStack = 1;
        }
    }
}