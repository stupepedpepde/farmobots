using System;
using System.Collections.Generic;
using System.Linq;
using Game.Scripts.Core.Environment.Terrain;
using UnityEngine;
using Game.Scripts.Core.Input;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;

namespace Game.Scripts.Core.Building {
    public enum BuildCategory {
        ALL,
        STRUCTURE,
        MACHINERY,
        FURNITURE,
        PLANTER
    }

    public class PlacedObjectData {
        public GameObject obj;
        public BuildablePrefab prefab;          // Now a ScriptableObject
        public Vector3[] worldConnectionPoints;
        public Quaternion rotation;
        public Vector3 position;
        public int variantIndex;

        public PlacedObjectData(GameObject obj, BuildablePrefab prefab, Quaternion rotation, Vector3 position, int variantIndex) {
            this.obj = obj;
            this.prefab = prefab;
            this.rotation = rotation;
            this.position = position;
            this.variantIndex = variantIndex;
            UpdateWorldConnectionPoints();
        }

        public void UpdateWorldConnectionPoints() {
            if (prefab.connectionPointsLocal == null || prefab.connectionPointsLocal.Length == 0) {
                worldConnectionPoints = Array.Empty<Vector3>();
                return;
            }

            worldConnectionPoints = new Vector3[prefab.connectionPointsLocal.Length];
            for (int i = 0; i < prefab.connectionPointsLocal.Length; i++)
                worldConnectionPoints[i] = position + rotation * prefab.connectionPointsLocal[i];
        }
    }

    public class BuildingSystem : MonoBehaviour, IInitializable, IUpdatable {
        public static BuildingSystem instance { get; private set; }

        [Header("Building Settings")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color ghostValidColor = new(0, 1, 0, 0.4f);
        [SerializeField] private Color ghostInvalidColor = new(1, 0, 0, 0.4f);
        [SerializeField] private LayerMask buildableLayerMask = ~0;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private float maxPlaceDistance = 20f;

        [Header("Grid Settings")]
        [SerializeField] private float[] gridSizes = { 0.25f, 0.5f, 1f, 2f };
        [Range(0, 3)] [SerializeField] private int gridIndex = 2;

        [Header("Snapping")]
        [SerializeField] private float connectionSnapDistance = 0.3f;
        [SerializeField] private bool debugDrawConnectionPoints = true;

        [Header("Ghost Visuals")]
        [SerializeField] private bool forceUnlitGhost = true;
        [SerializeField] private string unlitShaderName = "Unlit/Transparent";

        [Header("Buildable Prefabs")]
        [SerializeField] private List<BuildablePrefab> buildablePrefabs = new();
        [SerializeField] private int defaultPrefabIndex;

        private bool _buildMode;
        private GameObject _ghostObject;
        private bool _placable;
        private MeshRenderer[] _ghostRenderers;
        private int _currentPrefabIndex;
        private int _currentVariantIndex;
        private int _currentRotationIndex = 0;
        private readonly HashSet<GameObject> _placedObjects = new();
        private readonly Dictionary<GameObject, Bounds> _prefabBoundsCache = new();
        private List<PlacedObjectData> _placedObjectData = new();

        private GameObject _snappedToObject;
        private Vector3 _currentRotation = Vector3.zero;
        private Dictionary<string, Vector3> _prefabRotations = new();

        public event Action<bool> OnBuildModeChanged;
        public event Action<GameObject, Vector3, Quaternion> OnObjectPlaced;
        public event Action<int> OnPrefabChanged;
        public event Action<int> OnGridChanged;

        private BuildSelectionPopup buildSelectionPopup;

        public bool IsBuildModeActive => _buildMode;
        public BuildablePrefab CurrentPrefab => _currentPrefabIndex >= 0 && _currentPrefabIndex < buildablePrefabs.Count ? buildablePrefabs[_currentPrefabIndex] : null;

        public List<BuildablePrefab> BuildablePrefabs => buildablePrefabs;

        public int InitializationOrder => 7;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            if (_placedObjects.Count > 0) return;

            if (buildablePrefabs.Count == 0) {
                Debug.LogError("BuildingSystem: No BuildablePrefab ScriptableObjects assigned! Create assets via Assets/Create/Game/BuildablePrefab.");
                return;
            }

            _currentPrefabIndex = Mathf.Clamp(defaultPrefabIndex, 0, buildablePrefabs.Count - 1);
            CalculateAllPrefabBounds();
            CreateGhostObject();
            SetBuildMode(false);

            GameEvents.OnBuildingRequested += HandleBuildingRequest;
            GameEvents.OnBuildingUIRequested += HandleBuildingUIRequest;

            OnBuildModeChanged += active => Debug.Log($"Build mode: {(active ? "ON" : "OFF")}");
            OnObjectPlaced += (prefab, pos, rot) => Debug.Log($"Placed {prefab.name} at {pos}");
            OnPrefabChanged += index => Debug.Log($"Switched to {CurrentPrefab?.displayName}");
            OnGridChanged += index => Debug.Log($"Grid size: {gridSizes[index]}");

            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
            GameEvents.OnBuildingRequested -= HandleBuildingRequest;
        }

        public void OnUpdate(float deltaTime) {
            if (_buildMode) UpdateGhostPosition();
        }

        #region Rotation

        public void RotateGhost() {
            if (!_buildMode || CurrentPrefab == null) return;
            _currentRotationIndex = (_currentRotationIndex + 1) % 12;
            float angle = _currentRotationIndex * 30.0f;
            _currentRotation = new Vector3(0, angle, 0);

            _prefabRotations[CurrentPrefab.id] = _currentRotation;

            RecreateGhostObject();
            UpdateGhostPosition();
            OnPrefabChanged?.Invoke(_currentPrefabIndex);
        }

        #endregion

        #region Building Operations

        private void HandleBuildingRequest() {
            if (!_buildMode || _ghostObject == null || !_ghostObject.activeSelf || CurrentPrefab == null)
                return;

            if (IsValidBuildPosition(_ghostObject.transform.position))
                PlaceObject(CurrentPrefab, CurrentPrefab.category == BuildCategory.STRUCTURE);
        }

        private void HandleBuildingUIRequest() {
            if (_buildMode) {
                if (buildSelectionPopup == null)
                    buildSelectionPopup = new BuildSelectionPopup();
                buildSelectionPopup.Toggle();
            }
        }

        private void PlaceObject(BuildablePrefab buildable, bool raiseTerrain) {
            if (buildable?.prefabVariants == null || buildable.prefabVariants.Length == 0 || !_placable) return;

            var playerInv = InventoryService.PlayerInventory;
            if (playerInv == null) {
                Debug.LogWarning("No player inventory found!");
                return;
            }

            foreach (var req in buildable.requirements) {
                if (req.requiredItem == null) continue;
                if (!playerInv.HasItem(req.requiredItem.Create(1), req.quantity)) {
                    Debug.Log($"Missing {req.quantity}x {req.requiredItem.ItemName}");
                    return;
                }
            }

            foreach (var req in buildable.requirements) {
                if (req.requiredItem == null) continue;
                playerInv.TryConsumeItem(req.requiredItem.Create(1), req.quantity);
            }

            GameObject prefabToPlace = buildable.prefabVariants[_currentVariantIndex];
            if (prefabToPlace == null) return;

            var position = _ghostObject.transform.position;
            var rotation = Quaternion.Euler(_currentRotation);

            var newObj = Instantiate(prefabToPlace, position, rotation);
            newObj.name = $"{buildable.displayName}_{_placedObjects.Count}";

            if (newObj.GetComponent<Collider>() == null)
                newObj.AddComponent<MeshCollider>();

            _placedObjects.Add(newObj);
            var data = new PlacedObjectData(newObj, buildable, rotation, position, _currentVariantIndex);
            _placedObjectData.Add(data);

            if (raiseTerrain)
                TerrainManager.instance?.FlattenAroundBuilding(newObj, 0.01f);

            OnObjectPlaced?.Invoke(newObj, position, rotation);
        }

        #endregion

        #region Validation & Snapping

        private Vector3 GetSnappedPosition(Vector3 worldPos) {
            if (CurrentPrefab == null) return worldPos;
            Vector3 placementOffset = CurrentPrefab.placementOffset;
            Vector3 basePos = worldPos + placementOffset;

            if (TrySnapToConnectionPoints(basePos, out Vector3 snappedPos))
                return snappedPos;

            if (gridIndex < 0 || gridIndex >= gridSizes.Length) return basePos;
            float size = gridSizes[gridIndex];
            if (size <= 0) return basePos;

            return new Vector3(
                Mathf.Round(basePos.x / size) * size,
                Mathf.Round(basePos.y / size) * size,
                Mathf.Round(basePos.z / size) * size
            );
        }

        private bool TrySnapToConnectionPoints(Vector3 desiredPos, out Vector3 resultPos) {
            resultPos = desiredPos;
            _snappedToObject = null;

            if (CurrentPrefab == null || CurrentPrefab.connectionPointsLocal == null || CurrentPrefab.connectionPointsLocal.Length == 0)
                return false;

            Quaternion ghostRotation = Quaternion.Euler(_currentRotation);
            Vector3[] ghostWorldPoints = new Vector3[CurrentPrefab.connectionPointsLocal.Length];
            for (int i = 0; i < CurrentPrefab.connectionPointsLocal.Length; i++) {
                ghostWorldPoints[i] = desiredPos + ghostRotation * CurrentPrefab.connectionPointsLocal[i];
            }

            float bestDistance = float.MaxValue;
            Vector3 bestOffset = Vector3.zero;
            GameObject bestTarget = null;

            foreach (var placed in _placedObjectData) {
                if (placed.obj == null) continue;
                foreach (var placedPoint in placed.worldConnectionPoints) {
                    for (int i = 0; i < ghostWorldPoints.Length; i++) {
                        float dist = Vector3.Distance(ghostWorldPoints[i], placedPoint);
                        if (dist < bestDistance && dist < connectionSnapDistance) {
                            bestDistance = dist;
                            bestOffset = placedPoint - ghostWorldPoints[i];
                            bestTarget = placed.obj;
                        }
                    }
                }
            }

            if (bestDistance < float.MaxValue) {
                resultPos = desiredPos + bestOffset;
                _snappedToObject = bestTarget;
                return true;
            }

            return false;
        }

        private bool IsValidBuildPosition(Vector3 pos) {
            if (CurrentPrefab == null) return false;
            GameObject currentVariant = GetCurrentVariant();
            if (currentVariant == null) return false;

            var bounds = GetPrefabBounds(currentVariant);
            var checkCenter = pos + bounds.center;

            var colliders = Physics.OverlapBox(checkCenter, bounds.extents * 0.95f, Quaternion.identity, obstacleLayerMask);
            foreach (var col in colliders) {
                if (col == null || col.isTrigger) continue;
                if (col.gameObject == _ghostObject) continue;
                if (_snappedToObject != null && col.gameObject == _snappedToObject) continue;
                if (_placedObjects.Contains(col.gameObject)) continue;

                return false;
            }

            return true;
        }

        #endregion

        #region Prefab Management

        private void CalculateAllPrefabBounds() {
            foreach (var data in buildablePrefabs) {
                if (data.prefabVariants == null) continue;
                foreach (var variant in data.prefabVariants)
                    if (variant != null && !_prefabBoundsCache.ContainsKey(variant))
                        _prefabBoundsCache[variant] = CalculatePrefabBounds(variant);
            }
        }

        private Bounds CalculatePrefabBounds(GameObject prefab) {
            var temp = Instantiate(prefab);
            temp.SetActive(false);
            var bounds = CombineBounds(temp);
            Destroy(temp);
            return bounds;
        }

        public Bounds CalculateWorldBounds(GameObject prefab) {
            if (prefab == null) return new Bounds(Vector3.zero, Vector3.one);

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                bounds.size = Vector3.Max(bounds.size, new Vector3(2f, 0.1f, 2f));
                return bounds;
            }

            var colliders = prefab.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0) {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);

                bounds.size = Vector3.Max(bounds.size, new Vector3(2f, 0.1f, 2f));
                return bounds;
            }

            return new Bounds(prefab.transform.position, new Vector3(3f, 1f, 3f));
        }

        private Bounds CombineBounds(GameObject root) {
            var bounds = new Bounds();
            bool hasBounds = false;

            foreach (var r in root.GetComponentsInChildren<Renderer>()) {
                if (r == null) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!hasBounds)
                foreach (var c in root.GetComponentsInChildren<Collider>()) {
                    if (c == null) continue;
                    if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                    else bounds.Encapsulate(c.bounds);
                }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        public Bounds GetPrefabBounds(GameObject prefab) {
            return _prefabBoundsCache.TryGetValue(prefab, out var bounds) ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        public void RandomizeCurrentVariant() {
            if (CurrentPrefab == null || CurrentPrefab.prefabVariants.Length == 0) return;

            _currentVariantIndex = UnityEngine.Random.Range(0, CurrentPrefab.prefabVariants.Length);
            RecreateGhostObject();

            if (_buildMode) UpdateGhostPosition();
        }

        public void SetCurrentPrefabIndex(int index) {
            if (index < 0 || index >= buildablePrefabs.Count || index == _currentPrefabIndex)
                return;

            _currentPrefabIndex = index;
            RandomizeCurrentVariant();

            string prefabId = CurrentPrefab.id;
            if (_prefabRotations.TryGetValue(prefabId, out Vector3 savedRotation)) {
                _currentRotation = savedRotation;
            } else {
                _currentRotation = CurrentPrefab.placementRotation;
                _prefabRotations[prefabId] = _currentRotation;
            }
            _currentRotationIndex = Mathf.RoundToInt(_currentRotation.y / 30f) % 12;

            if (_buildMode) UpdateGhostPosition();
            OnPrefabChanged?.Invoke(index);
        }

        public void SetCurrentPrefabById(string id) {
            int index = buildablePrefabs.FindIndex(p => p.id == id);
            if (index >= 0) SetCurrentPrefabIndex(index);
        }

        public int GetCurrentPrefabIndex() => _currentPrefabIndex;

        private GameObject GetCurrentVariant() {
            if (CurrentPrefab == null || CurrentPrefab.prefabVariants == null || CurrentPrefab.prefabVariants.Length == 0)
                return null;
            return CurrentPrefab.prefabVariants[_currentVariantIndex];
        }

        #endregion

        #region Ghost Object

        private void CreateGhostObject() {
            GameObject currentVariant = GetCurrentVariant();
            if (currentVariant == null) return;

            _ghostObject = Instantiate(currentVariant);
            _ghostObject.name = $"Ghost_{CurrentPrefab.id}";
            _ghostObject.transform.rotation = Quaternion.Euler(_currentRotation);
            _ghostObject.SetActive(false);

            CleanupGhostComponents();

            _ghostObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform child in _ghostObject.transform)
                child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            _ghostRenderers = _ghostObject.GetComponentsInChildren<MeshRenderer>();
            if (_ghostRenderers.Length == 0) {
                Debug.LogWarning($"Prefab '{CurrentPrefab.displayName}' variant has no MeshRenderer. Adding a temporary cube for visibility.");
                var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tempCube.transform.SetParent(_ghostObject.transform, false);
                tempCube.layer = LayerMask.NameToLayer("Ignore Raycast");
                _ghostRenderers = _ghostObject.GetComponentsInChildren<MeshRenderer>();
            }

            Material baseMat = ghostMaterial;
            if (baseMat == null) {
                Shader fallbackShader = Shader.Find("Unlit/Transparent");
                if (fallbackShader != null)
                    baseMat = new Material(fallbackShader);
                else
                    baseMat = new Material(Shader.Find("Standard"));
            }

            foreach (var r in _ghostRenderers) {
                r.material = new Material(baseMat);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                if (forceUnlitGhost) {
                    Shader unlitShader = Shader.Find(unlitShaderName);
                    if (unlitShader != null)
                        r.material.shader = unlitShader;
                    else
                        Debug.LogWarning($"Unlit shader '{unlitShaderName}' not found.");
                }

                r.material.SetFloat("_Mode", 3);
                r.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                r.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                r.material.SetInt("_ZWrite", 0);
                r.material.DisableKeyword("_ALPHATEST_ON");
                r.material.EnableKeyword("_ALPHABLEND_ON");
                r.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                r.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            UpdateGhostVisuals(true);
        }

        private void RecreateGhostObject() {
            if (_ghostObject != null)
                Destroy(_ghostObject);
            CreateGhostObject();
        }

        private void UpdateGhostPosition() {
            if (_ghostObject == null || CurrentPrefab == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            bool rayHit = Physics.Raycast(ray, out var hit, maxPlaceDistance, buildableLayerMask);

            Vector3 targetPos;
            if (rayHit)
                targetPos = hit.point;
            else
                targetPos = cam.transform.position + cam.transform.forward * 5f;

            var snapped = GetSnappedPosition(targetPos);
            var valid = IsValidBuildPosition(snapped);

            if (rayHit)
                _placable = valid;
            else
                _placable = valid && (_snappedToObject != null);

            _ghostObject.transform.position = snapped;
            _ghostObject.transform.rotation = Quaternion.Euler(_currentRotation);
            _ghostObject.SetActive(true);
            UpdateGhostVisuals(valid);

            if (debugDrawConnectionPoints && _ghostObject.activeSelf && CurrentPrefab.connectionPointsLocal.Length > 0) {
                for (int i = 0; i < CurrentPrefab.connectionPointsLocal.Length; i++) {
                    Vector3 worldPoint = _ghostObject.transform.position + _ghostObject.transform.rotation * CurrentPrefab.connectionPointsLocal[i];
                    Debug.DrawLine(worldPoint - Vector3.up * 0.1f, worldPoint + Vector3.up * 0.1f, Color.yellow);
                    Debug.DrawLine(worldPoint - Vector3.right * 0.1f, worldPoint + Vector3.right * 0.1f, Color.yellow);
                }
            }
        }

        private void UpdateGhostVisuals(bool valid) {
            if (_ghostRenderers == null || _ghostRenderers.Length == 0) return;
            var color = valid ? ghostValidColor : ghostInvalidColor;
            foreach (var r in _ghostRenderers) {
                if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", color);
                else if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", color);
                else
                    Debug.LogWarning("Ghost material has no recognized color property (_Color or _BaseColor)");
            }
        }

        private void CleanupGhostComponents() {
            var components = _ghostObject.GetComponentsInChildren<Component>();
            foreach (var comp in components) {
                if (comp is MonoBehaviour) Destroy(comp);
                else if (comp is not (Transform or MeshRenderer or MeshFilter))
                    Destroy(comp);
            }

            var colliders = _ghostObject.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
                Destroy(col);
        }

        #endregion

        #region Public API

        public void SetBuildMode(bool active) {
            if (_buildMode == active) return;
            _buildMode = active;
            if (_ghostObject != null) {
                _ghostObject.SetActive(active);
                if (active) {
                    _currentRotationIndex = Mathf.RoundToInt(_currentRotation.y / 30.0f) % 12;
                    _snappedToObject = null;
                }
            }
            OnBuildModeChanged?.Invoke(active);
            GameEvents.BuildModeChanged(active);
        }

        public bool GetBuildMode() => _buildMode;

        #endregion
    }
}