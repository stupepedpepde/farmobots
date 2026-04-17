using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Scripts.Inventory.Items;
using Random = UnityEngine.Random;

namespace Game.Scripts.Core.Building {
    [CreateAssetMenu(fileName = "BuildablePrefab", menuName = "ScriptableObjects/BuildablePrefab")]
    public class BuildablePrefab : ScriptableObject {
        public string id;
        public string displayName;
        public GameObject[] prefabVariants;
        public BuildCategory category;
        public Sprite icon;
        public Vector3 placementOffset = Vector3.zero;
        public Vector3 placementRotation = Vector3.zero;
        public Vector3[] connectionPointsLocal = Array.Empty<Vector3>();
        public List<BuildRequirement> requirements = new List<BuildRequirement>();

        [HideInInspector] public Bounds bounds;
        public Vector3 BoundsSize => bounds.size;
        public Vector3 BoundsCenter => bounds.center;

        public GameObject GetRandomVariant() {
            if (prefabVariants == null || prefabVariants.Length == 0) return null;
            return prefabVariants[Random.Range(0, prefabVariants.Length)];
        }
    }

    [System.Serializable]
    public class BuildRequirement {
        public ItemDetails requiredItem;
        public int quantity;
    }
}