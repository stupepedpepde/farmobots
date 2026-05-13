using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Plants;
using UnityEngine;

namespace Game.Scripts.Planting {
    public class PlantingSystem : MonoBehaviour, IInitializable, IUpdatable {
        public static PlantingSystem instance { get; private set; }

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

        private struct PendingPlantContext {
            public InventoryComponent inventory;
            public PlantableSpot targetSpot;
            public Vector3 hitPoint;
            public Plantable targetPlantable;
            public PlantableSpot raycastSpot;
        }
        private PendingPlantContext? pendingContext;

        private float lastPlantTime = -1f;
        private const float PLANT_COOLDOWN = 0.5f;
        private int previewPlantSize = 1;

        public int InitializationOrder => 2;

        private void Awake() {
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
            UpdateHoverIndicator(previewPlantSize);
        }

        #region Visual Indicator

        private void InitializeIndicatorBlocks(Plantable plantable) {
            ClearIndicatorBlocks();
            if (plantable == null) return;

            Vector2Int gridSize = plantable.GridSize;
            float cellSize = plantable.CellSize;
            indicatorBlocks = new GameObject[gridSize.x, gridSize.y];

            for (int x = 0; x < gridSize.x; x++) {
                for (int y = 0; y < gridSize.y; y++) {
                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(block.GetComponent<Collider>());
                    block.name = $"PlantingIndicator_{plantable.name}_{x}_{y}";
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
                if (block != null) Destroy(block);
            indicatorBlocks = null;
        }

        private void ShowPlantingIndicator(Plantable plantable, Vector2Int center, int plantSize) {
            if (plantable == null) return;
            if (indicatorBlocks == null || indicatorBlocks.GetLength(0) != plantable.GridSize.x || indicatorBlocks.GetLength(1) != plantable.GridSize.y) {
                InitializeIndicatorBlocks(plantable);
            }
            showingIndicator = true;
            currentIndicatorCenter = center;
            currentIndicatorSize = plantSize;
            UpdateIndicatorVisuals(plantable);
        }

        private void HidePlantingIndicator() {
            showingIndicator = false;
            if (indicatorBlocks == null) return;
            foreach (var block in indicatorBlocks)
                if (block != null) block.SetActive(false);
        }

        private void UpdatePlantingIndicator(Plantable plantable, Vector3 hitPoint, int plantSize) {
            if (plantable == null) return;
            Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);
            if (plantSize > 1) {
                int offset = (plantSize - 1) / 2;
                gridPos.x = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
                gridPos.y = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);
            }
            ShowPlantingIndicator(plantable, gridPos, plantSize);
        }

        private void UpdateIndicatorVisuals(Plantable plantable) {
            if (!showingIndicator || indicatorBlocks == null || plantable == null) return;
            if (indicatorBlocks.GetLength(0) != plantable.GridSize.x || indicatorBlocks.GetLength(1) != plantable.GridSize.y) {
                InitializeIndicatorBlocks(plantable);
                if (indicatorBlocks == null) return;
            }

            foreach (var block in indicatorBlocks)
                if (block != null) block.SetActive(false);

            for (int x = 0; x < currentIndicatorSize; x++) {
                for (int y = 0; y < currentIndicatorSize; y++) {
                    int gridX = currentIndicatorCenter.x + x;
                    int gridY = currentIndicatorCenter.y + y;
                    if (gridX >= 0 && gridX < indicatorBlocks.GetLength(0) && gridY >= 0 && gridY < indicatorBlocks.GetLength(1)) {
                        GameObject block = indicatorBlocks[gridX, gridY];
                        if (block != null) {
                            PlantableSpot spot = plantable.PlantingSpots[gridX, gridY];
                            bool isValid = spot != null && !spot.isOccupied;
                            Renderer renderer = block.GetComponent<Renderer>();
                            if (renderer != null) {
                                renderer.material.SetColor("_BaseColor", isValid ? indicatorValidColor : indicatorInvalidColor);
                            }
                            block.SetActive(true);
                        }
                    }
                }
            }
        }

        private void UpdateHoverIndicator(int plantSize) {
            Camera cam = Camera.main;
            if (cam == null) {
                ClearHoverIndicator();
                return;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlantDistance, plantableLayer)) {
                Plantable plantable = hit.collider.GetComponentInParent<Plantable>();
                if (plantable != null) {
                    if (currentHoveredPlantable != plantable) {
                        if (currentHoveredPlantable != null) {
                            HidePlantingIndicator();
                            ClearIndicatorBlocks(); // Force reinit for new plantable
                        }
                        currentHoveredPlantable = plantable;
                    }
                    UpdatePlantingIndicator(plantable, hit.point, plantSize);
                } else {
                    ClearHoverIndicator();
                }
            } else {
                ClearHoverIndicator();
            }
        }

        private void ClearHoverIndicator() {
            if (currentHoveredPlantable != null) {
                HidePlantingIndicator();
                ClearIndicatorBlocks();
                currentHoveredPlantable = null;
            }
        }

        #endregion

        #region Planting (Popup & Direct)

        private void HandlePlantingRequest(InventoryComponent inventory, PlantableSpot spot) {
            if (Time.time - lastPlantTime < PLANT_COOLDOWN) {
                Debug.Log("Planting on cooldown, please wait.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) {
                Debug.LogError("No main camera found.");
                return;
            }

            InventoryComponent plantingInventory = inventory ?? InventoryService.PlayerInventory;
            if (plantingInventory == null) {
                Debug.LogError("No inventory available for planting.");
                return;
            }

            if (spot != null) {
                if (spot.isOccupied) {
                    Debug.Log($"Spot {spot.name} is already occupied, cannot plant.");
                    return;
                }
                pendingContext = new PendingPlantContext {
                    inventory = plantingInventory,
                    targetSpot = spot,
                    targetPlantable = spot.GetComponentInParent<Plantable>()
                };
                SeedSelectionPopup.Show(plantingInventory, OnSeedSelected);
                return;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlantDistance, plantableLayer)) {
                Debug.Log("No plantable surface in range.");
                return;
            }

            Plantable plantable = hit.collider.GetComponentInParent<Plantable>();
            if (plantable == null) {
                Debug.Log($"Hit object {hit.collider.name} is not part of a Plantable.");
                return;
            }

            Vector2Int gridPos = plantable.WorldToGridPosition(hit.point);
            if (gridPos.x < 0 || gridPos.x >= plantable.GridSize.x || gridPos.y < 0 || gridPos.y >= plantable.GridSize.y) {
                Debug.Log($"Hit point maps to grid position {gridPos} which is out of bounds.");
                return;
            }

            PlantableSpot hitSpot = plantable.PlantingSpots[gridPos.x, gridPos.y];
            if (hitSpot == null) {
                Debug.Log($"No PlantableSpot at grid {gridPos}.");
                return;
            }

            if (hitSpot.isOccupied) {
                Debug.Log($"Spot at {hitSpot.name} is already occupied, cannot plant.");
                return;
            }

            pendingContext = new PendingPlantContext {
                inventory = plantingInventory,
                targetSpot = null,
                hitPoint = hit.point,
                targetPlantable = plantable,
                raycastSpot = hitSpot
            };
            SeedSelectionPopup.Show(plantingInventory, OnSeedSelected);
        }

        private void OnSeedSelected(Item selectedSeed) {
            if (selectedSeed == null || pendingContext == null) {
                pendingContext = null;
                return;
            }

            PlantSO plantToGrow = selectedSeed.details.PlantsToGrow;
            if (plantToGrow == null) {
                Debug.LogWarning($"Seed '{selectedSeed.details.ItemName}' has no PlantsToGrow assigned.");
                pendingContext = null;
                return;
            }

            var ctx = pendingContext.Value;
            InventoryComponent inv = ctx.inventory;

            if (ctx.targetSpot != null) {
                if (ctx.targetSpot.isOccupied) {
                    Debug.Log($"Target spot {ctx.targetSpot.name} became occupied while choosing seed.");
                    pendingContext = null;
                    return;
                }
            } else if (ctx.raycastSpot != null) {
                if (ctx.raycastSpot.isOccupied) {
                    Debug.Log($"Raycast spot {ctx.raycastSpot.name} became occupied while choosing seed.");
                    pendingContext = null;
                    return;
                }
            } else {
                Debug.LogError("No valid target spot in pending context.");
                pendingContext = null;
                return;
            }

            if (!inv.TryConsumeItem(selectedSeed, 1)) {
                Debug.Log($"Failed to consume seed '{selectedSeed.details.ItemName}'. Not enough quantity.");
                pendingContext = null;
                return;
            }

            bool success = false;
            if (ctx.targetSpot != null) {
                success = PlantAtSpot(ctx.targetSpot, plantToGrow);
            } else if (ctx.targetPlantable != null) {
                success = PlantAtPoint(ctx.targetPlantable, ctx.hitPoint, plantToGrow);
            }

            if (success) {
                Debug.Log($"Successfully planted {plantToGrow.plantName} using {selectedSeed.details.ItemName}.");
                lastPlantTime = Time.time;
                ClearHoverIndicator(); // Refresh indicator after planting
            } else {
                inv.TryAddItem(selectedSeed.details.Create(1));
                Debug.Log($"Planting failed for {plantToGrow.plantName}, seed refunded.");
            }

            pendingContext = null;
        }

        public bool TryPlantWithSeed(InventoryComponent inventory, PlantableSpot spot, Item seedItem) {
            if (inventory == null || spot == null || seedItem == null) {
                Debug.LogWarning("TryPlantWithSeed: invalid parameters.");
                return false;
            }
            if (spot.isOccupied) {
                Debug.Log("TryPlantWithSeed: spot already occupied.");
                return false;
            }

            PlantSO plant = seedItem.details.PlantsToGrow;
            if (plant == null) {
                Debug.LogWarning($"Seed {seedItem.details.ItemName} has no PlantSO.");
                return false;
            }

            if (!inventory.TryConsumeItem(seedItem, 1)) return false;

            bool success = PlantAtSpot(spot, plant);
            if (!success) inventory.TryAddItem(seedItem.details.Create(1));
            else ClearHoverIndicator();
            return success;
        }

        public bool TryPlantWithSeed(InventoryComponent inventory, Vector3 hitPoint, Plantable plantable, Item seedItem) {
            if (inventory == null || plantable == null || seedItem == null) return false;
            PlantSO plant = seedItem.details.PlantsToGrow;
            if (plant == null) return false;
            if (!inventory.TryConsumeItem(seedItem, 1)) return false;
            bool success = PlantAtPoint(plantable, hitPoint, plant);
            if (!success) inventory.TryAddItem(seedItem.details.Create(1));
            else ClearHoverIndicator();
            return success;
        }

        private bool PlantAtSpot(PlantableSpot spot, PlantSO plant) {
            if (spot == null || spot.isOccupied) return false;
            if (plant.plantSize > 1) return TryPlantMultiTileFromSpot(spot, plant);
            else return spot.TryPlant(plant);
        }

        private bool PlantAtPoint(Plantable plantable, Vector3 hitPoint, PlantSO plant) {
            if (plantable == null) return false;
            if (plant.plantSize > 1) return TryPlantMultiTileAtPoint(plantable, hitPoint, plant);
            else {
                Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);
                if (gridPos.x >= 0 && gridPos.x < plantable.GridSize.x && gridPos.y >= 0 && gridPos.y < plantable.GridSize.y) {
                    PlantableSpot spot = plantable.PlantingSpots[gridPos.x, gridPos.y];
                    if (spot != null && !spot.isOccupied) return spot.TryPlant(plant);
                }
                return false;
            }
        }

        private bool TryPlantMultiTileFromSpot(PlantableSpot spot, PlantSO plant) {
            Plantable plantable = spot.GetComponentInParent<Plantable>();
            Vector2Int gridPos = FindSpotGridPosition(spot, plantable);
            if (gridPos == new Vector2Int(-1, -1)) return false;
            int plantSize = plant.plantSize;
            int offset = (plantSize - 1) / 2;
            int startX = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
            int startY = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);
            return PlantMultiTile(plantable, startX, startY, plant);
        }

        private bool TryPlantMultiTileAtPoint(Plantable plantable, Vector3 hitPoint, PlantSO plant) {
            Vector2Int gridPos = plantable.WorldToGridPosition(hitPoint);
            int plantSize = plant.plantSize;
            int offset = (plantSize - 1) / 2;
            int startX = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
            int startY = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);
            return PlantMultiTile(plantable, startX, startY, plant);
        }

        private bool PlantMultiTile(Plantable plantable, int startX, int startY, PlantSO plant) {
            int plantSize = plant.plantSize;
            for (int x = 0; x < plantSize; x++) {
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;
                    if (checkX >= plantable.GridSize.x || checkY >= plantable.GridSize.y) return false;
                    PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                    if (spot == null || spot.isOccupied) return false;
                }
            }

            GameObject plantParent = new GameObject(plant.plantName);
            plantParent.transform.SetParent(plantable.transform);

            Vector3 centerPosition = Vector3.zero;
            for (int x = 0; x < plantSize; x++) {
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;
                    PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                    centerPosition += spot.transform.position;
                }
            }
            centerPosition /= (plantSize * plantSize);
            plantParent.transform.position = centerPosition;

            Plant newPlant = plantParent.AddComponent<Plant>();
            newPlant.SetPlantSO(plant);

            for (int x = 0; x < plantSize; x++) {
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;
                    PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                    spot.Occupy(newPlant);
                }
            }

            newPlant.Initialize();
            return true;
        }

        public Vector2Int FindSpotGridPosition(PlantableSpot spot, Plantable plantable) {
            for (int x = 0; x < plantable.GridSize.x; x++)
                for (int y = 0; y < plantable.GridSize.y; y++)
                    if (plantable.PlantingSpots[x, y] == spot)
                        return new Vector2Int(x, y);
            return new Vector2Int(-1, -1);
        }

        #endregion
    }
}