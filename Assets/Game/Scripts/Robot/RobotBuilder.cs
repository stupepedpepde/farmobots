using System;
using Game.Scripts.Inventory.Helpers;
using Game.Scripts.Plants;
using UnityEngine;

namespace Game.Scripts.Robot {
    public class RobotBuilder {
        private RobotType robotType = RobotType.PLANTER;
        private Vector3 position = Vector3.zero;
        private Quaternion rotation = Quaternion.identity;
        private Transform parent = null;
        private string robotName = null;

        private float moveSpeed = 5f;
        private float workSpeed = 1f;
        private float efficiency = 1f;
        private float workRange = 1.5f;
        private float maxEnergy = 100f;
        private float energyDrainRate = 0.5f;
        private float rechargeRate = 5f;
        private float lowEnergyThreshold = 20f;
        private int maxQueuedTasks = 5;

        private PlantSO selectedPlant = null;
        private Material robotMaterial = null;
        private Mesh robotMesh = null;
        private Color robotColor = Color.white;

        private Action<Robot> onRobotCreated = null;

        #region Fluent Builder

        public RobotBuilder WithType(RobotType type) {
            robotType = type;

            return this;
        }

        public RobotBuilder AtPosition(Vector3 position) {
            this.position = position;

            return this;
        }

        public RobotBuilder WithRotation(Quaternion rotation) {
            this.rotation = rotation;

            return this;
        }

        public RobotBuilder WithRotation(Vector3 euler) {
            rotation = Quaternion.Euler(euler);

            return this;
        }

        public RobotBuilder WithParent(Transform parent) {
            this.parent = parent;

            return this;
        }

        public RobotBuilder WithName(string name) {
            robotName = name;

            return this;
        }

        public RobotBuilder WithStats(float moveSpeed = 5f, float workSpeed = 1f, float efficiency = 1f, float workRange = 1.5f) {
            this.moveSpeed = moveSpeed;
            this.workSpeed = workSpeed;
            this.efficiency = efficiency;
            this.workRange = workRange;

            return this;
        }

        public RobotBuilder WithEnergy(float maxEnergy = 100f, float energyDrainRate = 0.5f, float rechargeRate = 5f, float lowEnergyThreshold = 20f) {
            this.maxEnergy = maxEnergy;
            this.energyDrainRate = energyDrainRate;
            this.rechargeRate = rechargeRate;
            this.lowEnergyThreshold = lowEnergyThreshold;

            return this;
        }

        public RobotBuilder WithTaskCapacity(int maxQueuedTasks = 5) {
            this.maxQueuedTasks = maxQueuedTasks;

            return this;
        }

        public RobotBuilder WithSelectedPlant(PlantSO plant) {
            selectedPlant = plant;

            return this;
        }

        public RobotBuilder WithAppearance(Material material, Mesh mesh = null) {
            robotMaterial = material;
            robotMesh = mesh;

            return this;
        }

        public RobotBuilder WithColor(Color color) {
            robotColor = color;

            return this;
        }

        public RobotBuilder OnCreated(Action<Robot> callback) {
            onRobotCreated = callback;

            return this;
        }

        #endregion

        #region Creation

        public Robot Build() {
            GameObject robotGO = CreateRobotGameObject();
            Robot robot = ConfigureRobotComponent(robotGO);
            ConfigureAppearance(robotGO);

            onRobotCreated?.Invoke(robot);

            return robot;
        }

        public static Robot CreateDefault(RobotType type, Vector3 position) {
            return new RobotBuilder()
                .WithType(type)
                .AtPosition(position)
                .Build();
        }

        public static Robot CreatePlanter(Vector3 position, PlantSO plant = null) {
            return new RobotBuilder()
                .WithType(RobotType.PLANTER)
                .AtPosition(position)
                .WithSelectedPlant(plant)
                .WithColor(Color.green)
                .WithName("Planter")
                .Build();
        }

        public static Robot CreateHarvester(Vector3 position) {
            return new RobotBuilder()
                .WithType(RobotType.HARVESTER)
                .AtPosition(position)
                .WithColor(Color.yellow)
                .WithName("Harvester")
                .WithStats(moveSpeed: 6f, workRange: 2f)
                .Build();
        }

        public static Robot CreateGardener(Vector3 position) {
            return new RobotBuilder()
                .WithType(RobotType.GARDENER)
                .AtPosition(position)
                .WithColor(Color.blue)
                .WithName("Gardener")
                .WithStats(workSpeed: 2f)
                .Build();
        }

        public static Robot CreateMiner(Vector3 position) {
            return new RobotBuilder()
                .WithType(RobotType.MINER)
                .AtPosition(position)
                .WithColor(Color.magenta)
                .WithName("Miner")
                .WithStats(moveSpeed: 4f, workRange: 1.2f)
                .WithEnergy(maxEnergy: 120f)
                .Build();
        }

        #endregion

        #region Utility

        private GameObject CreateRobotGameObject() {
            GameObject robotGO;
            string prefabPath = $"Robots/{robotType}";

            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
                robotGO = GameObject.Instantiate(prefab, position, rotation, parent);
            else {
                robotGO = new GameObject();
                robotGO.transform.SetPositionAndRotation(position, rotation);

                if (parent != null) robotGO.transform.parent = parent;

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Cube";
                visual.transform.SetParent(robotGO.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                var collider = visual.GetComponent<Collider>();
                if (collider != null) { collider.isTrigger = true; }
            }

            robotGO.name = robotName ?? $"{robotType}-{Guid.NewGuid().ToString().Substring(0, 8)}";

            return robotGO;
        }

        private Robot ConfigureRobotComponent(GameObject robotGO) {
            Robot robot = robotGO.GetComponent<Robot>();
            if (robot == null) robot = robotGO.AddComponent<Robot>();

            robot.Type = robotType;
            robot.MoveSpeed = moveSpeed;
            robot.WorkSpeed = workSpeed;
            robot.Efficiency = efficiency;
            robot.WorkRange = workRange;
            robot.MaxEnergy = maxEnergy;
            robot.EnergyDrainRate = energyDrainRate;
            robot.RechargeRate = rechargeRate;
            robot.LowEnergyThreshold = lowEnergyThreshold;
            robot.MaxQueuedTasks = maxQueuedTasks;

            robot.SetHomePosition(position);

            if (selectedPlant != null)
                robot.SelectedPlant = selectedPlant;

            return robot;
        }

        private void ConfigureAppearance(GameObject robotGO) {
            Transform visualChild = null;
            foreach (Transform child in robotGO.transform)
                if (child.GetComponent<Renderer>() != null) {
                    visualChild = child; break;
                }

            if (visualChild == null)
                visualChild = robotGO.transform;

            if (robotMaterial != null) {
                var renderer = visualChild.GetComponent<Renderer>();
                if (renderer != null) renderer.material = robotMaterial;
            }

            var rendererComp = visualChild.GetComponent<Renderer>();

            if (rendererComp != null) {
                Material mat = rendererComp.material;
                mat.color = robotColor;
                rendererComp.material = mat;
            }

            if (robotMesh != null) {
                var meshFilter = visualChild.GetComponent<MeshFilter>();
                if (meshFilter != null) meshFilter.mesh = robotMesh;
            }
        }

        #endregion
    }
}