using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Plants;
using UnityEngine;

namespace Game.Scripts.Planting {
    public class PlantingSystem : MonoBehaviour, IInitializable, IUpdatable {
        public static PlantingSystem instance { get; private set; }

        [SerializeField] private PlantSO selectedPlant; // todo: swap with inventory system
        [SerializeField] private Item seedItem;
        [SerializeField] private LayerMask plantableLayer = 1;
        [SerializeField] private float maxPlantDistance = 5.0f;
        [Space]
        [Header("Visual Indicator")]
        [SerializeField] private Material indicatorMaterial;
        [SerializeField] private Color indicatorValidColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color indicatorInvalidColor = new Color(1, 0, 0, 0.3f);
        [SerializeField] private float indicatorHeight = 0.05f;
        [SerializeField] private float indicatorOpacity = 0.4f;

        private Plantable currentHoveredPlantable;

        private GameObject[,] indicatorBlocks;
        private bool showingIndicator = false;
        private Vector2Int currentIndicatorCenter;
        private int currentIndicatorSize;

        public PlantSO SelectedPlant => selectedPlant; // todo seed -> plant connection

        public int InitializationOrder => 2;

        public void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            
            GameManager.instance?.Register(this as IInitializable);
        }
        
        public void Initialize() {
            GameEvents.OnPlantingRequested += HandlePlantingRequest;

            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            GameEvents.OnPlantingRequested -= HandlePlantingRequest;

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);

            ClearIndicatorBlocks();
        }

        public void OnUpdate(float deltaTime) {
            UpdateHoverIndicator();
        }

        # region Visual Indicator

        private void InitializeIndicatorBlocks(Plantable plantable) {
            ClearIndicatorBlocks();

            Vector2Int gridSize = plantable.GridSize;
            float cellSize = plantable.CellSize;

            indicatorBlocks = new GameObject[gridSize.x, gridSize.y];

            for (int x = 0; x < gridSize.x; x++) {
                for (int y = 0; y < gridSize.y; y++) {
                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(block.GetComponent<Collider>());

                    block.name = $"PlantingIndicator_{x}_{y}";
                    block.transform.SetParent(plantable.transform);

                    Renderer renderer = block.GetComponent<Renderer>();
                    if (indicatorMaterial != null) {
                        renderer.material = new Material(indicatorMaterial);
                        renderer.material.SetColor("_BaseColor", indicatorValidColor);
                        renderer.material.SetFloat("_Transparency", indicatorOpacity);
                    }

                    Vector3 cellPos = plantable.GetCellCenterWorld(x, y);
                    block.transform.position = cellPos + Vector3.up * indicatorHeight;
                    block.transform.localScale = new Vector3(cellSize * 0.85f, 0.05f, cellSize * 0.85f);
                    block.transform.rotation = plantable.transform.rotation;
                    block.SetActive(false);
                    indicatorBlocks[x, y] = block;
                }
            }
        }

        private void ClearIndicatorBlocks() {
            if (indicatorBlocks == null) return;

            foreach (var block in indicatorBlocks)
                if (block != null)
                    Destroy(block);

            indicatorBlocks = null;
        }

        private void ShowPlantingIndicator(Plantable plantable, Vector2Int center, int plantSize) {
            if (plantable == null) return;

            if (indicatorBlocks == null || indicatorBlocks.GetLength(0) != plantable.GridSize.x || indicatorBlocks.GetLength(1) != plantable.GridSize.y)
                InitializeIndicatorBlocks(plantable);

            showingIndicator = true;
            currentIndicatorCenter = center;
            currentIndicatorSize = plantSize;
            UpdateIndicatorVisuals(plantable);
        }

        private void HidePlantingIndicator() {
            showingIndicator = false;
            if (indicatorBlocks == null) return;

            foreach (var block in indicatorBlocks)
                if (block != null)
                    block.SetActive(false);
        }

        private void UpdatePlantingIndicator(Plantable plantable, Vector3 hitPoint) {
            Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);
            int plantSize = selectedPlant.plantSize;

            if (plantSize > 1) {
                int offset = (plantSize - 1) / 2;
                gridPos.x = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
                gridPos.y = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);
            }

            ShowPlantingIndicator(plantable, gridPos, plantSize);
        }

        private void UpdateIndicatorVisuals(Plantable plantable) {
            if (!showingIndicator || indicatorBlocks == null || plantable == null) return;

            foreach (var block in indicatorBlocks)
                if (block != null) block.SetActive(false);

            for (int x = 0; x < currentIndicatorSize; x++)
                for (int y = 0; y < currentIndicatorSize; y++) {
                    int gridX = currentIndicatorCenter.x + x;
                    int gridY = currentIndicatorCenter.y + y;

                    if (gridX >= 0 && gridX < indicatorBlocks.GetLength(0) && gridY >= 0 && gridY < indicatorBlocks.GetLength(1)) {
                        GameObject block = indicatorBlocks[gridX, gridY];
                        if (block != null) {
                            PlantableSpot spot = plantable.PlantingSpots[gridX, gridY];
                            bool isValid = spot != null & !spot.isOccupied;

                            Renderer renderer = block.GetComponent<Renderer>();
                            if (renderer != null) {
                                renderer.material.SetColor("_BaseColor", isValid ? indicatorValidColor : indicatorInvalidColor);
                                renderer.material.SetFloat("_Transparency", indicatorOpacity);
                            }

                            block.SetActive(true);
                        }
                    }
                }
        }

        private void UpdateHoverIndicator() { // todo add build mode check
            Camera cam = Camera.main;
            if (cam == null || selectedPlant == null) {
                if (currentHoveredPlantable != null) {
                    HidePlantingIndicator();
                    currentHoveredPlantable = null;
                }

                return;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlantDistance, plantableLayer)) {
                Plantable plantable = hit.collider.GetComponent<Plantable>();

                if (plantable != null) {
                    if (currentHoveredPlantable != plantable) {
                        if (currentHoveredPlantable != null) HidePlantingIndicator();

                        currentHoveredPlantable = plantable;
                    }

                    UpdatePlantingIndicator(plantable, hit.point);
                } else
                    ClearHoverIndicator();
            } else
                ClearHoverIndicator();
        }

        private void ClearHoverIndicator() {
            if (currentHoveredPlantable != null) {
                HidePlantingIndicator();
                currentHoveredPlantable = null;
            }
        }

        # endregion

        # region Planting

        private void HandlePlantingRequest(InventoryComponent inventory, PlantableSpot spot) {
            Camera cam = Camera.main;
            if (cam == null) return;

            InventoryComponent plantingInventory = inventory ?? InventoryService.PlayerInventory;

            if (plantingInventory == null) return;

            if (spot != null)
                TryPlacePlant(spot, plantingInventory);

            TryPlacePlant(cam.transform, plantingInventory);
        }

        private bool TryPlacePlant(PlantableSpot spot, InventoryComponent inventory) {
            if (!inventory.HasItem(seedItem)) return false;

            try {
                bool success = false;

                if (selectedPlant.plantSize > 1)
                    success = TryPlantMultiTile(spot, inventory);
                else
                    success = TryPlantSingleTile(spot, inventory);

                if (!success)
                    inventory.TryAddItem(seedItem.details.Create(1));

                return success;
            } catch {
                inventory.TryAddItem(seedItem.details.Create(1));
                return false;
            }
        }

        private bool TryPlacePlant(Transform cameraTransform, InventoryComponent inventory) {
            if (selectedPlant == null || currentHoveredPlantable == null) return false;

            if (!inventory.HasItem(seedItem)) return false;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlantDistance, plantableLayer)) {
                Plantable plantable = hit.collider.GetComponent<Plantable>();

                if (plantable != null) {
                    bool planted = false;

                    if (selectedPlant.plantSize > 1)
                        planted = TryPlantMultiTile(plantable, hit.point, inventory);
                    else
                        planted = TryPlantSingleTile(plantable, hit.point, inventory);

                    if (planted) HidePlantingIndicator();

                    return planted;
                }
            }

            return false;
        }

        private bool TryPlantSingleTile(Plantable plantable, Vector3 hitPoint, InventoryComponent inventory) {
            Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);

            if (gridPos.x >= 0 && gridPos.x < plantable.GridSize.x && gridPos.y >= 0 && gridPos.y < plantable.GridSize.y) {
                PlantableSpot spot = plantable.PlantingSpots[gridPos.x, gridPos.y];

                if (spot != null && !spot.isOccupied) {
                    if (!inventory.TryConsumeItem(seedItem, 1)) return false;

                    try {
                        bool success = spot.TryPlant(selectedPlant);
                        if (!success)
                            inventory.TryAddItem(seedItem.details.Create(1));

                        return success;
                    } catch {
                        inventory.TryAddItem(seedItem.details.Create(1));
                        return false;
                    }
                }
            }

            return false;
        }

        private bool TryPlantSingleTile(PlantableSpot spot, InventoryComponent inventory) {
            if (!inventory.TryConsumeItem(seedItem, 1) || spot == null || spot.isOccupied) return false;

            try {
                bool success = spot.TryPlant(selectedPlant);
                if (!success)
                    inventory.TryAddItem(seedItem.details.Create(1));

                return success;
            }
            catch {
                inventory.TryAddItem(seedItem.details.Create(1));
                return false;
            }
        }

        private bool TryPlantMultiTile(Plantable plantable, Vector3 hitPoint, InventoryComponent inventory) {
            Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);
            int plantSize = selectedPlant.plantSize;

            int offset = (plantSize - 1) / 2;
            int startX = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
            int startY = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);

            for (int x = 0; x < plantSize; x++)
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;

                    if (checkX >= plantable.GridSize.x || checkY >= plantable.GridSize.y) return false;

                    PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                    if (spot == null || spot.isOccupied) return false;
                }

            if (!inventory.TryConsumeItem(seedItem, 1)) return false;

            try {
                GameObject plantParent = new GameObject(selectedPlant.plantName);
                plantParent.transform.SetParent(plantable.transform);

                Vector3 centerPosition = Vector3.zero;
                for (int x = 0; x < plantSize; x++)
                    for (int y = 0; y < plantSize; y++) {
                        int checkX = startX + x;
                        int checkY = startY + y;

                        PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                        centerPosition += spot.transform.position;
                    }

                centerPosition /= Mathf.Pow(plantSize, 2);
                plantParent.transform.position = centerPosition;

                Plant plant = plantParent.AddComponent<Plant>();
                plant.SetPlantSO(selectedPlant);

                for (int x = 0; x < plantSize; x++) {
                    for (int y = 0; y < plantSize; y++) {
                        int checkX = startX + x;
                        int checkY = startY + y;

                        PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                        spot.Occupy(plant);
                    }
                }

                plant.Initialize();
                return true;
            } catch {
                inventory.TryAddItem(seedItem.details.Create(1));
                return false;
            }

            return false;
        }

        private bool TryPlantMultiTile(PlantableSpot spot, InventoryComponent inventory) {
            Plantable plantable = spot.GetComponentInParent<Plantable>();

            Vector2Int gridPos = FindSpotGridPosition(spot, plantable);
            if (gridPos == new Vector2Int(-1, -1)) return false;

            int plantSize = selectedPlant.plantSize;
            int offset = (plantSize - 1) / 2;

            int startX = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
            int startY = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);

            List<PlantableSpot> spotsToOccupy = new List<PlantableSpot>();
            for (int x = 0; x < plantSize; x++)
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;

                    if (checkX >= plantable.GridSize.x || checkY >= plantable.GridSize.y) return false;

                    PlantableSpot targetSpot = plantable.PlantingSpots[checkX, checkY];
                    if (targetSpot == null || targetSpot.isOccupied) return false;

                    spotsToOccupy.Add(targetSpot);
                }

            if (!inventory.TryConsumeItem(seedItem, 1)) return false;

            try {
                GameObject plantParent = new GameObject(selectedPlant.plantName);
                plantParent.transform.SetParent(plantable.transform);

                Vector3 centerPosition = Vector3.zero;
                foreach (var targetSpot in spotsToOccupy)
                    centerPosition += targetSpot.transform.position;

                centerPosition /= spotsToOccupy.Count;
                plantParent.transform.position = centerPosition;

                Plant plantedPlant = plantParent.AddComponent<Plant>();
                plantedPlant.SetPlantSO(selectedPlant);

                foreach (var targetSpot in spotsToOccupy)
                    targetSpot.Occupy(plantedPlant);

                plantedPlant.Initialize();

                return true;
            } catch {
                inventory.TryAddItem(seedItem.details.Create(1));

                return false;
            }
        }

        # endregion

        public Vector2Int FindSpotGridPosition(PlantableSpot spot, Plantable plantable) {
            for (int x = 0; x < plantable.GridSize.x; x++)
                for (int y = 0; y < plantable.GridSize.y; y++)
                    if (plantable.PlantingSpots[x, y] == spot)
                        return new Vector2Int(x, y);

            return new Vector2Int(-1, -1);
        }


        public void SelectPlant(PlantSO plant) {
            if (plant != null) {
                selectedPlant = plant;
                Debug.Log($"Selected: {selectedPlant.plantName}");
                UpdateHoverIndicator();
            }
        }
    }
}