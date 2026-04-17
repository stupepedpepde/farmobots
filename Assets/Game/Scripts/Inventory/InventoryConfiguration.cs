using UnityEngine;

namespace Game.Scripts.Inventory {
    [CreateAssetMenu(fileName = "Inventory Configuration", menuName = "ScriptableObjects/Inventory/InventoryConfiguration")]
    public class InventoryConfiguration : ScriptableObject {
        [SerializeField] public string inventoryName = "Inventory";
        [SerializeField] public int capacity = 24;
        [SerializeField] public int rows = 3;
        [SerializeField] public bool autoSort = false;
        [SerializeField] public bool persistAcrossScenes = false;

        public void Initialize() {

        }

        // getters
        public string GetName() => inventoryName;
        public int GetCapacity() => capacity;
        public int GetRows() => rows;
        public bool IsAutoSort() => autoSort;
        public bool IsPersistAcrossScenes() => persistAcrossScenes;
    }
}