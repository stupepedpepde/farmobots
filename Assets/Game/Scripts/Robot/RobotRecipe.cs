using System;
using System.Collections.Generic;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using UnityEngine;

namespace Game.Scripts.Robot {
    [CreateAssetMenu(fileName = "New Robot Recipe", menuName = "ScriptableObjects/Robot/Recipe")]
    public class RobotRecipe : ScriptableObject {
        public RobotType robotType;
        public string displayName = "Robot";
        [TextArea] public string description;

        [Header("Crafting Requirements")]
        public List<ResourceRequirement> requirements = new List<ResourceRequirement>();

        [Header("Base Stats")]
        public float moveSpeed = 5f;
        public float workSpeed = 1f;
        public float efficiency = 1f;
        public float workRange = 1.5f;
        public float maxEnergy = 100f;
        public float energyDrainRate = 0.5f;
        public float rechargeRate = 5f;
        public float lowEnergyThreshold = 20f;
        public int maxQueuedTasks = 5;

        [Header("Appearance")]
        public Color robotColor = Color.white;

        [Serializable]
        public class ResourceRequirement {
            public ItemDetails item;
            public int quantity = 1;
        }

        public bool CanCraft(InventoryComponent inventory) {
            foreach (var req in requirements)
                if (!inventory.HasItem(req.item.Create(1), req.quantity))
                    return false;
            return true;
        }

        public void ConsumeResources(InventoryComponent inventory) {
            foreach (var req in requirements)
                inventory.TryConsumeItem(req.item.Create(1), req.quantity);
        }
    }
}