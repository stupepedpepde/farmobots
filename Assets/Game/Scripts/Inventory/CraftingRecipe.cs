using System.Collections.Generic;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Inventory {
    [CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "ScriptableObjects/Crafting/Recipe")]
    public class CraftingRecipe : ScriptableObject {
        public string displayName = "Crafting Recipe";
        public Sprite icon;
        public List<ItemRequirement> inputs = new List<ItemRequirement>();
        public List<ItemResult> outputs = new List<ItemResult>();

        public bool Matches(Item[] gridItems) {
            var required = new Dictionary<System.Guid, int>();
            foreach (var req in inputs) {
                if (req.quantity <= 0) continue;
                if (required.ContainsKey(req.item.ID))
                    required[req.item.ID] += req.quantity;
                else
                    required[req.item.ID] = req.quantity;
            }

            var available = new Dictionary<System.Guid, int>();
            foreach (var item in gridItems) {
                if (item == null || item.quantity <= 0) continue;
                if (available.ContainsKey(item.details.ID))
                    available[item.details.ID] += item.quantity;
                else
                    available[item.details.ID] = item.quantity;
            }

            foreach (var kvp in required) {
                if (!available.TryGetValue(kvp.Key, out int have) || have < kvp.Value)
                    return false;
            }

            return true;
        }

        public List<Item> ConsumeAndGetOutputs(Item[] gridItems) {
            var mutable = new List<Item>();
            foreach (var item in gridItems) {
                if (item != null && item.quantity > 0)
                    mutable.Add(item);
            }

            foreach (var req in inputs) {
                int remaining = req.quantity;
                for (int i = mutable.Count - 1; i >= 0 && remaining > 0; i--) {
                    var gridItem = mutable[i];
                    if (gridItem.details.ID == req.item.ID) {
                        int take = Mathf.Min(remaining, gridItem.quantity);
                        gridItem.quantity -= take;
                        remaining -= take;
                        if (gridItem.quantity <= 0)
                            mutable.RemoveAt(i);
                    }
                }
                if (remaining > 0) {
                    Debug.LogError($"Failed to consume {req.item.ItemName} x{req.quantity} – recipe mismatch");
                    return null;
                }
            }

            for (int i = 0; i < gridItems.Length; i++) {
                if (gridItems[i] != null && gridItems[i].quantity <= 0)
                    gridItems[i] = null;
            }

            var results = new List<Item>();
            foreach (var outItem in outputs) {
                if (outItem.quantity > 0 && outItem.item != null) {
                    var result = outItem.item.Create(outItem.quantity);
                    results.Add(result);
                }
            }
            return results;
        }
    }

    [System.Serializable]
    public class ItemRequirement {
        public ItemDetails item;
        public int quantity;
    }

    [System.Serializable]
    public class ItemResult {
        public ItemDetails item;
        public int quantity;
    }
}