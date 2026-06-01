using System;
using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment;
using Game.Scripts.Core.Environment.Terrain;
using Game.Scripts.Core.Environment.Terrain.Node;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Plants;
using Game.Scripts.Planting;
using UnityEngine;
using UnityUtils;

namespace Game.Scripts.Robot {
    public enum RobotType { PLANTER, HARVESTER, MINER, GARDENER }
    public enum RobotState { IDLE, MOVING, WORKING, RETURNING, RECHARGING }
    public enum RobotAction { WAIT, PLANT, HARVEST, WATER, MINE, MOVE_TO_POSITION }

    [Serializable]
    public class RobotTask {
        public PlantableSpot targetSpot;
        public Node targetNode;
        public Vector3 targetPosition;
        public RobotAction action;
        public float priority = 1.0f;
        public float workDuration = 2.0f;
        public Action OnComplete;
    }

    public class Robot : MonoBehaviour, IInitializable, IInteractable {
        [Header("Robot Configuration")]
        [SerializeField] private RobotType type;
        [Space]
        [Header("Inventory")]
        [SerializeField] private RobotInventory inventory;
        [SerializeField] private float interactionRange = 3.0f;
        [Space]
        [Header("Robot Stats")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float workSpeed = 1.0f;
        [SerializeField] private float efficiency = 1.0f;
        [SerializeField] private float workRange = 1.5f;
        [SerializeField] private float hoverHeight = 0.5f;
        [Space]
        [Header("Energy System")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float energyDrainRate = 0.5f;
        [SerializeField] private float rechargeRate = 5f;
        [SerializeField] private float lowEnergyThreshold = 20f;
        [Space]
        [Header("Flashlight")]
        [SerializeField] private float flashlightIntensity = 30f;
        [SerializeField] private float flashlightRange = 100f;
        [SerializeField] private float flashlightInnerSpotAngle = 50f;
        [SerializeField] private float flashlightOuterSpotAngle = 80f;
        [SerializeField] private Color flashlightColor = Color.white;
        [Space]
        [Header("Task System")]
        [SerializeField] private int maxQueuedTasks = 5;
        [Space]
        [Header("Idle Behavior")]
        [SerializeField] private float idleWanderRadius = 10f;
        [SerializeField] private float idleWanderInterval = 5f;
        [Space]
        [Header("Debug")]
        [SerializeField] private RobotState state = RobotState.IDLE;
        [SerializeField] private float currentEnergy;
        [SerializeField] private PlantableSpot currentTargetSpot;
        [SerializeField] private Node currentTargetNode;
        [SerializeField] private NodeType prioritizedNodeType;
        [SerializeField] private Vector3 homePosition;
        [SerializeField] private Queue<RobotTask> taskQueue = new Queue<RobotTask>();
        [SerializeField] private RobotTask currentTask;
        [SerializeField] private float workTimer;

        private Light flashlightLight;
        private bool isSubscribedToTime = false;

        private float idleWanderTimer;
        private Vector3 idleDestination;

        public NodeType PrioritizedNodeType {
            get => prioritizedNodeType;
            set => prioritizedNodeType = value;
        }

        public int InitializationOrder => 50;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            currentEnergy = maxEnergy;
            homePosition = transform.position;
            SnapToTerrainHeight();

            if (inventory == null) {
                inventory = GetComponentInChildren<RobotInventory>();
                if (inventory == null) {
                    GameObject inventoryGO = new GameObject("Inventory");
                    inventoryGO.transform.parent = transform;
                    inventoryGO.transform.localPosition = Vector3.zero;
                    inventory = inventoryGO.AddComponent<RobotInventory>();
                }
            }

            inventory?.Initialize();

            inventory.GetInventoryComponent().OnItemAdded += (item, slot) => {
                if (type == RobotType.PLANTER && item.details.IsSeed && state == RobotState.IDLE)
                    FindWork();
            };

            SetupFlashlight();

            RobotManager.instance?.RegisterRobot(this);
        }

        private void OnDestroy() {
            RobotManager.instance?.UnregisterRobot(this);
            GameManager.instance?.Unregister(this as IInitializable);
            UnsubscribeFromTimeManager();
        }

        public void OnInteract() {
            if (inventory != null)
                GameEvents.RequestInventory(inventory.GetInventoryComponent());
        }

        public void OnUpdate(float deltaTime) {
            SnapToTerrainHeight();

            switch (state) {
                case RobotState.IDLE: ProcessIdleState(deltaTime); break;
                case RobotState.MOVING: ProcessMovingState(deltaTime); break;
                case RobotState.WORKING: ProcessWorkingState(deltaTime); break;
                case RobotState.RETURNING: ProcessReturningState(deltaTime); break;
                case RobotState.RECHARGING: ProcessRechargingState(deltaTime); break;
            }
        }

        #region Terrain Height
        private void SnapToTerrainHeight() {
            if (TerrainManager.instance == null) return;
            float terrainHeight = TerrainManager.instance.GetHeightAt(transform.position);
            Vector3 pos = transform.position;
            pos.y = terrainHeight + hoverHeight;
            transform.position = pos;
        }
        #endregion

        #region State Processing
        private void ProcessIdleState(float deltaTime) {
            if (currentEnergy <= lowEnergyThreshold) {
                state = RobotState.RETURNING;
                return;
            }
            if (taskQueue.Count > 0 && currentTask == null)
                ProcessNextTask();
            else if (currentTask == null && taskQueue.Count == 0 && state == RobotState.IDLE) {
                FindWork();
                UpdateIdleWander(deltaTime);
            }
        }

        private void ProcessMovingState(float deltaTime) {
            if (currentTask == null) {
                state = RobotState.IDLE;
                return;
            }

            Vector3 target = GetTargetPosition();
            if (target == Vector3.zero) {
                state = RobotState.IDLE;
                return;
            }

            MoveTowards(target, deltaTime);
            if (Vector3.Distance(transform.position, target) <= workRange)
                OnReachedTarget();

            currentEnergy = Mathf.Max(0, currentEnergy - energyDrainRate * deltaTime * 0.5f);
        }

        private void ProcessWorkingState(float deltaTime) {
            if (currentTask == null) {
                state = RobotState.IDLE;
                FindWork();
                return;
            }
            workTimer -= deltaTime;
            if (workTimer <= 0f)
                CompleteCurrentTask();
            currentEnergy = Mathf.Max(0, currentEnergy - energyDrainRate * deltaTime);
        }

        private void ProcessReturningState(float deltaTime) {
            MoveTowards(homePosition, deltaTime);
            if (Vector3.Distance(transform.position, homePosition) <= 1.0f)
                state = currentEnergy < maxEnergy ? RobotState.RECHARGING : RobotState.IDLE;
            currentEnergy = Mathf.Max(0, currentEnergy - energyDrainRate * deltaTime * 0.2f);
        }

        private void ProcessRechargingState(float deltaTime) {
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + rechargeRate * deltaTime);
            if (currentEnergy >= maxEnergy * 0.8f)
                state = RobotState.IDLE;
        }
        #endregion

        #region Movement
        private void MoveTowards(Vector3 target, float deltaTime) {
            Vector3 direction = (target - transform.position).normalized;
            Vector3 newPos = transform.position + direction * (moveSpeed * deltaTime);
            transform.position = newPos;

            if (direction != Vector3.zero) {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5.0f * deltaTime);
            }
        }

        private void OnReachedTarget() {
            if (currentTask == null) return;
            workTimer = currentTask.workDuration / Mathf.Max(0.1f, workSpeed);
            state = RobotState.WORKING;
        }
        #endregion

        #region Idle Wander
        private void UpdateIdleWander(float deltaTime) {
            idleWanderTimer -= deltaTime;
            if (idleWanderTimer <= 0f) {
                Vector3 center = (currentTargetSpot != null) ? currentTargetSpot.transform.position : homePosition;
                Vector3 randomDir = UnityEngine.Random.insideUnitSphere * idleWanderRadius;
                idleDestination = center + randomDir;

                if (TerrainManager.instance != null)
                    idleDestination.y = TerrainManager.instance.GetHeightAt(idleDestination) + hoverHeight;
                idleWanderTimer = idleWanderInterval;
            }

            if (idleDestination != Vector3.zero && Vector3.Distance(transform.position, idleDestination) > 1f)
                MoveTowards(idleDestination, deltaTime);
        }
        #endregion

        #region Flashlight
        private void SetupFlashlight() {
            GameObject lightGO = new GameObject("Flashlight");
            lightGO.transform.SetParent(transform);
            lightGO.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            lightGO.transform.localRotation = Quaternion.identity;

            flashlightLight = lightGO.AddComponent<Light>();
            flashlightLight.type = LightType.Spot;
            flashlightLight.intensity = flashlightIntensity;
            flashlightLight.range = flashlightRange;
            flashlightLight.spotAngle = flashlightInnerSpotAngle;
            flashlightLight.spotAngle = flashlightOuterSpotAngle;
            flashlightLight.color = flashlightColor;
            flashlightLight.shadows = LightShadows.Soft;

            flashlightLight.enabled = false;

            SubscribeToTimeManager();
        }

        private void SubscribeToTimeManager() {
            if (isSubscribedToTime) return;
            if (TimeManager.instance == null) return;

            TimeManager.instance.OnDayStageChanged += OnDayStageChanged;
            isSubscribedToTime = true;

            OnDayStageChanged(TimeManager.instance.GetCurrentDayStage);
        }

        private void UnsubscribeFromTimeManager() {
            if (!isSubscribedToTime) return;
            if (TimeManager.instance != null)
                TimeManager.instance.OnDayStageChanged -= OnDayStageChanged;
            isSubscribedToTime = false;
        }

        private void OnDayStageChanged(DayStage newStage) {
            if (flashlightLight == null) return;
            bool shouldBeOn = newStage == DayStage.DAWN || newStage == DayStage.NIGHT;
            Debug.Log($"flashlight {shouldBeOn}");
            flashlightLight.enabled = shouldBeOn;
        }
        #endregion

        #region Task Management
        public void EnqueueTask(RobotTask task) {
            if (taskQueue.Count >= maxQueuedTasks) return;
            taskQueue.Enqueue(task);
            if (state == RobotState.IDLE && currentTask == null) ProcessNextTask();
        }

        public void AssignImmediateTask(RobotTask task) {
            currentTask = null;
            taskQueue.Clear();
            currentTask = task;
            currentTargetSpot = task.targetSpot;
            currentTargetNode = task.targetNode;
            state = RobotState.MOVING;
        }

        public void ClearAllTasks() {
            taskQueue.Clear();
            currentTask = null;
            currentTargetSpot = null;
            currentTargetNode = null;
            if (state != RobotState.RECHARGING && state != RobotState.RETURNING) {
                state = RobotState.IDLE;
                FindWork();
            }
        }

        public void ReturnToBaseToRecharge() {
            ClearAllTasks();
            state = RobotState.RETURNING;
        }

        private void ProcessNextTask() {
            if (taskQueue.Count == 0 || state == RobotState.RECHARGING) { currentTask = null; return; }
            currentTask = taskQueue.Dequeue();
            currentTargetSpot = currentTask.targetSpot;
            currentTargetNode = currentTask.targetNode;
            state = RobotState.MOVING;
        }

        private void CompleteCurrentTask() {
            bool success = PerformWorkAction();
            if (success) currentTask?.OnComplete?.Invoke();
            currentTask = null;
            currentTargetSpot = null;
            currentTargetNode = null;
            state = RobotState.IDLE;
            FindWork();
        }

        private Vector3 GetTargetPosition() {
            if (currentTask == null) return Vector3.zero;
            if (currentTask.targetSpot != null) return currentTask.targetSpot.transform.position;
            if (currentTask.targetNode != null) return currentTask.targetNode.transform.position;
            if (currentTask.targetPosition != Vector3.zero) return currentTask.targetPosition;
            return Vector3.zero;
        }
        #endregion

        #region Work Actions
        private bool PerformWorkAction() {
            if (currentTask == null) return true;
            switch (currentTask.action) {
                case RobotAction.PLANT: return PerformPlanting();
                case RobotAction.HARVEST: return PerformHarvesting();
                case RobotAction.WATER: return PerformWatering();
                case RobotAction.MINE: return PerformMining();
                case RobotAction.MOVE_TO_POSITION: return true;
                default: return true;
            }
        }

        private bool PerformPlanting() {
            if (currentTargetSpot == null) return false;

            InventoryComponent inv = inventory.GetInventoryComponent();
            if (inv == null) return false;

            Item seed = GetFirstSeedFromInventory(inv);
            if (seed == null) {
                Debug.LogWarning($"{name} has no seeds to plant.");
                return false;
            }

            bool planted = PlantingSystem.instance.TryPlantWithSeed(inv, currentTargetSpot, seed);
            if (planted) {
                Debug.Log($"{name} planted {seed.details.ItemName} at {currentTargetSpot.name}");
            } else {
                Debug.LogWarning($"{name} failed to plant {seed.details.ItemName} – spot may be occupied or invalid.");
            }
            return planted;
        }

        private Item GetFirstSeedFromInventory(InventoryComponent inv) {
            for (int i = 0; i < inv.GetCapacity(); i++) {
                Item item = inv.GetItem(i);
                if (item != null && item.details.IsSeed && item.details.PlantsToGrow != null) {
                    return item;
                }
            }
            return null;
        }

        private bool PerformHarvesting() {
            if (currentTargetSpot == null || !currentTargetSpot.isOccupied) return false;
            Plant plant = currentTargetSpot.currentPlant;
            if (plant == null) return false;
            InventoryComponent inv = inventory.GetInventoryComponent();
            if (inv != null) {
                var drops = plant.GetHarvestDrops();
                foreach (var drop in drops) {
                    int qty = UnityEngine.Random.Range(drop.minQuantity, drop.maxQuantity + 1);
                    Item item = drop.itemDetails.Create(qty);
                    inv.TryAddItem(item);
                }
            }
            currentTargetSpot.Clear();
            return true;
        }

        private bool PerformWatering() {
            if (currentTargetSpot == null || !currentTargetSpot.isOccupied) return false;
            Plant plant = currentTargetSpot.currentPlant;
            if (plant == null) return false;
            if (!plant.NeedsWater) return false;

            InventoryComponent inv = inventory.GetInventoryComponent();
            if (inv == null) return false;

            Item water = GetWaterFromInventory(inv);
            if (water == null || water.quantity < 1) {
                Debug.LogWarning($"{name} has no water to water the plant.");
                return false;
            }

            if (!inv.TryConsumeItem(water, 1)) {
                Debug.LogWarning($"{name} failed to consume water.");
                return false;
            }

            plant.Water(100);
            Debug.Log($"{name} watered {plant.name} using water.");
            return true;
        }

        private Item GetWaterFromInventory(InventoryComponent inv) {
            for (int i = 0; i < inv.GetCapacity(); i++) {
                Item item = inv.GetItem(i);
                if (item != null && item.details.ItemName.ToLower() == "water") {
                    return item;
                }
            }
            return null;
        }

        private bool PerformMining() {
            if (currentTargetNode == null) return false;
            InventoryComponent inv = inventory.GetInventoryComponent();
            if (inv != null && currentTargetNode.HasLoot()) {
                Item item = currentTargetNode.CollectLoot();
                inv.TryAddItem(item);
                return true;
            }
            return false;
        }
        #endregion

        #region Utility
        public void FindWork() {
            if (RobotManager.instance == null) return;

            switch (type) {
                case RobotType.PLANTER:
                    if (!HasSeeds()) return;
                    currentTargetSpot = RobotManager.instance.FindSuitableSpot(this, transform.position);
                    if (currentTargetSpot != null) {
                        float workDuration = 2f;
                        RobotTask task = new RobotTask {
                            targetSpot = currentTargetSpot,
                            action = RobotAction.PLANT,
                            workDuration = workDuration
                        };
                        AssignImmediateTask(task);
                    }
                    break;
                case RobotType.HARVESTER:
                    currentTargetSpot = RobotManager.instance.FindSuitableSpot(this, transform.position);
                    if (currentTargetSpot != null && currentTargetSpot.currentPlant != null) {
                        float harvestTime = currentTargetSpot.currentPlant.GetHarvestTime();
                        RobotTask task = new RobotTask {
                            targetSpot = currentTargetSpot,
                            action = RobotAction.HARVEST,
                            workDuration = harvestTime
                        };
                        AssignImmediateTask(task);
                    }
                    break;
                case RobotType.MINER:
                    if (prioritizedNodeType != null)
                        currentTargetNode = RobotManager.instance.FindNearestNodeOfType(transform.position, prioritizedNodeType);
                    else
                        currentTargetNode = RobotManager.instance.FindNearestNode(transform.position);

                    if (currentTargetNode != null) {
                        RobotTask task = new RobotTask {
                            targetNode = currentTargetNode,
                            action = RobotAction.MINE,
                            workDuration = currentTargetNode.MiningTime
                        };
                        AssignImmediateTask(task);
                    }
                    break;
                case RobotType.GARDENER:
                    InventoryComponent inv = inventory.GetInventoryComponent();
                    if (inv == null) break;
                    Item water = GetWaterFromInventory(inv);
                    if (water == null || water.quantity < 1) {
                        Debug.Log($"{name} has no water, cannot water.");
                        break;
                    }

                    currentTargetSpot = RobotManager.instance.FindThirstySpotByPriority(transform.position);
                    if (currentTargetSpot != null) {
                        RobotTask task = new RobotTask {
                            targetSpot = currentTargetSpot,
                            action = RobotAction.WATER,
                            workDuration = 1.5f
                        };
                        AssignImmediateTask(task);
                    } else
                        Debug.Log($"{name} found no plants below 50% water.");
                    break;
            }
        }

        private bool HasSeeds() {
            InventoryComponent inv = inventory.GetInventoryComponent();
            if (inv == null) return false;
            for (int i = 0; i < inv.GetCapacity(); i++) {
                Item item = inv.GetItem(i);
                if (item != null && item.details != null && item.details.IsSeed && item.details.PlantsToGrow != null)
                    return true;
            }
            return false;
        }

        public void SetHomePosition(Vector3 pos) => homePosition = pos;
        public void RechargeEnergy(float amount) => currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        public void DrainEnergy(float amount) => currentEnergy = Mathf.Max(0, currentEnergy - amount);
        public bool HasSufficientEnergy(float requiredPercentage = 0.2f) => EnergyPercentage >= requiredPercentage;
        #endregion

        #region Getters & Setters
        public RobotType Type {
            get => type;
            set => type = value;
        }

        public RobotInventory Inventory => inventory;

        public float InteractionRange {
            get => interactionRange;
            set => interactionRange = Mathf.Max(0.1f, value);
        }

        public RobotState CurrentState => state;

        public float EnergyPercentage => currentEnergy / maxEnergy;

        public bool IsBusy => state != RobotState.IDLE || taskQueue.Count > 0;

        public int QueuedTaskCount => taskQueue.Count;

        public float MoveSpeed {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0.1f, value);
        }

        public float WorkSpeed {
            get => workSpeed;
            set => workSpeed = Mathf.Max(0.1f, value);
        }

        public float Efficiency {
            get => efficiency;
            set => efficiency = Mathf.Max(0.1f, value);
        }

        public float WorkRange {
            get => workRange;
            set => workRange = Mathf.Max(0.5f, value);
        }

        public float MaxEnergy {
            get => maxEnergy;
            set {
                maxEnergy = Mathf.Max(10f, value);
                if (currentEnergy > maxEnergy)
                    currentEnergy = maxEnergy;
            }
        }

        public float EnergyDrainRate {
            get => energyDrainRate;
            set => energyDrainRate = Mathf.Max(0.1f, value);
        }

        public float RechargeRate {
            get => rechargeRate;
            set => rechargeRate = Mathf.Max(0.1f, value);
        }

        public float LowEnergyThreshold {
            get => lowEnergyThreshold;
            set => lowEnergyThreshold = Mathf.Clamp(value, 0f, maxEnergy);
        }

        public int MaxQueuedTasks {
            get => maxQueuedTasks;
            set => maxQueuedTasks = Mathf.Max(1, value);
        }

        public float GetInteractionRange() => interactionRange;
        public string GetInteractionPrompt() => $"Open {type}";
        #endregion

        #region Debug
        private void OnDrawGizmos() {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, workRange);
            if (state == RobotState.MOVING && currentTargetSpot != null) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentTargetSpot.transform.position);
            } else if (state == RobotState.MOVING && currentTargetNode != null) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, currentTargetNode.transform.position);
            }
            if (state == RobotState.RETURNING) {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, homePosition);
            }
            float energyHeight = 2f + (EnergyPercentage * 1f);
            Vector3 energyPos = transform.position + Vector3.up * energyHeight;
            Gizmos.color = Color.Lerp(Color.red, Color.green, EnergyPercentage);
            Gizmos.DrawWireCube(energyPos, new Vector3(0.5f, 0.1f, 0.1f));
        }
        #endregion
    }
}