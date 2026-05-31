using System;
using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment.Terrain.Node;
using Game.Scripts.Plants;
using Game.Scripts.Planting;
using UnityEngine;

namespace Game.Scripts.Robot
{
    public class RobotManager : MonoBehaviour, IInitializable, IUpdatable
    {
        public static RobotManager instance { get; private set; }

        [SerializeField] private List<Robot> robots = new List<Robot>();
        [SerializeField] private List<Plantable> plantables = new List<Plantable>();
        [SerializeField] private List<Node> resourceNodes = new List<Node>();

        // events
        public event Action<Robot> OnRobotRegistered = delegate { };
        public event Action<Robot> OnRobotUnregistered = delegate { };
        public event Action<Plantable> OnPlantableRegistered = delegate { };
        public event Action<Plantable> OnPlantableUnregistered = delegate { };
        public event Action<Node> OnNodeRegistered = delegate { };
        public event Action<Node> OnNodeUnregistered = delegate { };
        public event Action<Robot, RobotTask> OnTaskAssigned = delegate { };
        public event Action<Robot, RobotTask> OnTaskCompleted = delegate { };

        public int InitializationOrder => 3;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            var existingRobots = FindObjectsOfType<Robot>();
            foreach (var robot in existingRobots) RegisterRobot(robot);

            var existingPlantables = FindObjectsOfType<Plantable>();
            foreach (var plantable in existingPlantables) RegisterPlantable(plantable);

            var existingNodes = FindObjectsOfType<Node>();
            foreach (var node in existingNodes) RegisterNode(node);

            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
        }

        public void OnUpdate(float deltaTime) {
            foreach (var robot in robots)
                robot.OnUpdate(deltaTime);
        }

        #region Registration

        public void RegisterRobot(Robot robot) {
            if (robot == null || robots.Contains(robot)) return;
            robots.Add(robot);
            OnRobotRegistered?.Invoke(robot);
        }

        public void UnregisterRobot(Robot robot) {
            if (robot == null || !robots.Contains(robot)) return;
            robots.Remove(robot);
            OnRobotUnregistered?.Invoke(robot);
        }

        public void RegisterPlantable(Plantable plantable) {
            if (plantable == null || plantables.Contains(plantable)) return;
            plantables.Add(plantable);
            OnPlantableRegistered?.Invoke(plantable);
        }

        public void UnregisterPlantable(Plantable plantable) {
            if (plantable == null || !plantables.Contains(plantable)) return;
            plantables.Remove(plantable);
            OnPlantableUnregistered?.Invoke(plantable);
        }

        public void RegisterNode(Node node) {
            if (node == null || resourceNodes.Contains(node)) return;
            resourceNodes.Add(node);
            OnNodeRegistered?.Invoke(node);
        }

        public void UnregisterNode(Node node) {
            if (node == null || !resourceNodes.Contains(node)) return;
            resourceNodes.Remove(node);
            OnNodeUnregistered?.Invoke(node);
        }

        #endregion

        #region Query Methods

        public List<Robot> GetRobots() => new List<Robot>(robots);
        public List<Robot> GetRobotsByType(RobotType type) {
            return robots.FindAll(r => r.Type == type);
        }

        public List<Robot> GetAvailableRobots(RobotType? typeFilter = null) {
            List<Robot> available = new List<Robot>();
            foreach (var robot in robots) {
                if (typeFilter.HasValue && robot.Type != typeFilter.Value) continue;
                if (!robot.IsBusy && robot.EnergyPercentage > 0.2f)
                    available.Add(robot);
            }

            return available;
        }

        public Robot GetNearestAvailableRobot(Vector3 position, RobotType? preferredType = null, float maxDistance = float.MaxValue) {
            Robot nearest = null;
            float minDistance = maxDistance;
            foreach (var robot in robots) {
                if (preferredType.HasValue && robot.Type != preferredType.Value) continue;
                if (robot.IsBusy || robot.EnergyPercentage <= 0.2f) continue;
                float distance = Vector3.Distance(robot.transform.position, position);

                if (distance < minDistance) {
                    minDistance = distance;
                    nearest = robot;
                }
            }

            return nearest;
        }

        #endregion

        #region Task Assignment

        public void AssignTaskToRobot(Robot robot, PlantableSpot spot, RobotAction action, float workDuration = 2f, float priority = 1f, Action OnComplete = null) {
            if (robot == null || spot == null) return;
            RobotTask task = new RobotTask {
                targetSpot = spot,
                action = action,
                priority = priority,
                workDuration = workDuration,
                OnComplete = OnComplete
            };

            robot.EnqueueTask(task);
            OnTaskAssigned?.Invoke(robot, task);
        }

        public void AssignTaskToRobot(Robot robot, Node node, RobotAction action, float workDuration = 2f, float priority = 1f, Action OnComplete = null) {
            if (robot == null || node == null) return;
            RobotTask task = new RobotTask {
                targetNode = node,
                action = action,
                priority = priority,
                workDuration = workDuration,
                OnComplete = OnComplete
            };

            robot.EnqueueTask(task);
            OnTaskAssigned?.Invoke(robot, task);
        }

        public void AssignTaskToNearestRobot(Vector3 position, PlantableSpot spot, RobotAction action, RobotType? preferredType = null, float maxDistance = 50f, float workDuration = 2f, float priority = 1f, Action OnComplete = null) {
            var nearest = GetNearestAvailableRobot(position, preferredType, maxDistance);
            if (nearest != null)
                AssignTaskToRobot(nearest, spot, action, workDuration, priority, OnComplete);
            else
                Debug.LogWarning($"No available robot found for {action} task near {position}");
        }

        public void AssignTaskToNearestRobot(Vector3 position, Node node, RobotAction action, RobotType? preferredType = null, float maxDistance = 50f, float workDuration = 2f, float priority = 1f, Action OnComplete = null) {
            var nearest = GetNearestAvailableRobot(position, preferredType, maxDistance);
            if (nearest != null)
                AssignTaskToRobot(nearest, node, action, workDuration, priority, OnComplete);
            else
                Debug.LogWarning($"No available robot found for {action} task near {position}");
        }

        #endregion

        #region Spot Finding

        public PlantableSpot FindSuitableSpot(Robot robot, Vector3 position, float maxDistance = 100f) {
            if (robot == null) return null;

            switch (robot.Type) {
                case RobotType.PLANTER:
                    int plantSize = 1;
                    return FindNearestEmptySpot(position, maxDistance, plantSize);
                case RobotType.HARVESTER:
                    return FindNearestHarvestableSpot(position, maxDistance);
                case RobotType.GARDENER:
                    return FindNearestThirstySpot(position, maxDistance);
                default:
                    return null;
            }
        }

        private PlantableSpot FindNearestEmptySpot(Vector3 position, float maxDistance, int plantSize = 1) {
            PlantableSpot closest = null;
            float closestDist = maxDistance;
            foreach (var plantable in plantables) {
                if (plantable == null) continue;
                var spots = plantable.AllSpots;
                foreach (var spot in spots) {
                    if (spot == null || spot.isOccupied) continue;
                    if (plantSize > 1 && !AreAllSpotsAvailableForMultiTile(spot, plantSize)) continue;
                    float dist = Vector3.Distance(position, spot.transform.position);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = spot;
                    }
                }
            }

            return closest;
        }

        private PlantableSpot FindNearestHarvestableSpot(Vector3 position, float maxDistance) {
            PlantableSpot closest = null;
            float closestDist = maxDistance;
            foreach (var plantable in plantables) {
                if (plantable == null) continue;
                var spots = plantable.AllSpots;
                foreach (var spot in spots) {
                    if (spot == null || !spot.isOccupied) continue;
                    var plant = spot.currentPlant;
                    if (plant != null && plant.IsGrown()) {
                        float dist = Vector3.Distance(position, spot.transform.position);
                        if (dist < closestDist) {
                            closestDist = dist;
                            closest = spot;
                        }
                    }
                }
            }

            return closest;
        }

        private PlantableSpot FindNearestThirstySpot(Vector3 position, float maxDistance) {
            PlantableSpot closest = null;
            float closestDist = maxDistance;
            foreach (var plantable in plantables) {
                if (plantable == null) continue;
                foreach (var spot in plantable.AllSpots) {
                    if (spot == null || !spot.isOccupied) continue;
                    var plant = spot.currentPlant;
                    if (plant != null && plant.NeedsWater) {
                        float dist = Vector3.Distance(position, spot.transform.position);
                        if (dist < closestDist) {
                            closestDist = dist;
                            closest = spot;
                        }
                    }
                }
            }
            return closest;
        }

        public PlantableSpot FindThirstySpotByPriority(Vector3 position, float maxDistance = 100f) {
            PlantableSpot urgentSpot = null;
            float urgentDist = maxDistance;
            PlantableSpot thirstySpot = null;
            float thirstyDist = maxDistance;

            foreach (var plantable in plantables) {
                if (plantable == null) continue;
                foreach (var spot in plantable.AllSpots) {
                    if (spot == null || !spot.isOccupied) continue;
                    var plant = spot.currentPlant;
                    if (plant != null && plant.NeedsWater) {
                        float waterPercent = plant.GetWaterPercentage();
                        float dist = Vector3.Distance(position, spot.transform.position);

                        if (waterPercent < 0.1f) { // Critical: cannot grow
                            if (dist < urgentDist) {
                                urgentDist = dist;
                                urgentSpot = spot;
                            }
                        } else if (waterPercent < 0.5f) { // Thirsty but still growing
                            if (dist < thirstyDist) {
                                thirstyDist = dist;
                                thirstySpot = spot;
                            }
                        }
                    }
                }
            }
            // Return urgent spot first, otherwise thirsty spot
            return urgentSpot != null ? urgentSpot : thirstySpot;
        }

        public Node FindNearestNode(Vector3 position, float maxDistance = 100f) {
            Node closest = null;
            float closestDist = maxDistance;
            foreach (var node in resourceNodes) {
                if (node == null || !node.HasLoot()) continue;
                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = node;
                }
            }

            return closest;
        }

        public Node FindNearestNodeOfType(Vector3 position, NodeType targetType, float maxDistance = 100f) {
            Node closest = null;
            float closestDist = maxDistance;
            foreach (var node in resourceNodes) {
                if (node == null || !node.HasLoot()) continue;
                if (node.nodeType != targetType) continue;   // type check
                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = node;
                }
            }
            return closest;
        }

        public List<NodeType> GetAllNodeTypes() {
            HashSet<NodeType> types = new HashSet<NodeType>();
            foreach (var node in resourceNodes)
                if (node != null && node.nodeType != null)
                    types.Add(node.nodeType);
            return new List<NodeType>(types);
        }

        private bool AreAllSpotsAvailableForMultiTile(PlantableSpot centerSpot, int plantSize) {
            Plantable plantable = centerSpot.GetComponentInParent<Plantable>();
            if (plantable == null) return false;

            Vector2Int gridPos = PlantingSystem.instance?.FindSpotGridPosition(centerSpot, plantable) ?? new Vector2Int(-1, -1);
            if (gridPos.x == -1) return false;

            int offset = (plantSize - 1) / 2;
            int startX = Mathf.Clamp(gridPos.x - offset, 0, plantable.GridSize.x - plantSize);
            int startY = Mathf.Clamp(gridPos.y - offset, 0, plantable.GridSize.y - plantSize);

            if (startX + plantSize > plantable.GridSize.x || startY + plantSize > plantable.GridSize.y) return false;

            for (int x = 0; x < plantSize; x++)
                for (int y = 0; y < plantSize; y++) {
                    int checkX = startX + x;
                    int checkY = startY + y;
                    PlantableSpot spot = plantable.PlantingSpots[checkX, checkY];
                    if (spot == null || spot.isOccupied) return false;
                }

            return true;
        }

        #endregion
    }
}