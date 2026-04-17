using System;
using System.Collections.Generic;
using Game.Scripts.Inventory;
using Game.Scripts.Robot;
using UnityEngine;

namespace Game.Scripts.Core.Building.Buildings {
    public class RobotComputer : MonoBehaviour, IInitializable, IInteractable {
        [Header("References")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private InventoryComponent internalInventory;

        [Header("Settings")]
        [SerializeField] private string computerName = "Robot Control Terminal";
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private int inventoryCapacity = 30;
        [SerializeField] private int inventoryRows = 5;

        [Header("Available Recipes")]
        [SerializeField] private List<RobotRecipe> availableRecipes = new List<RobotRecipe>();

        public int InitializationOrder => 45;

        private RobotManagementPopup _managementPopup;
        private bool isUIOpen;
        private InventoryComponent playerInventory;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            if (internalInventory == null) {
                internalInventory = InventoryBuilder.Create(gameObject, $"{computerName} Storage")
                    .WithCapacity(inventoryCapacity)
                    .WithRows(inventoryRows)
                    .Build();
            }

            InventoryService.Register(internalInventory);

            playerInventory = InventoryService.PlayerInventory;

            if (spawnPoint == null) {
                GameObject sp = new GameObject("SpawnPoint");
                sp.transform.SetParent(transform);
                sp.transform.localPosition = new Vector3(0, 0, 2f);
                spawnPoint = sp.transform;
            }
        }

        private void OnDestroy() {
            InventoryService.Unregister(internalInventory);
            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IInteractable);

            if (isUIOpen)
                _managementPopup?.Toggle();
        }

        public void OnInteract() {
            playerInventory = InventoryService.PlayerInventory;

            if (_managementPopup == null)
                _managementPopup = new RobotManagementPopup(this, availableRecipes, playerInventory);
            _managementPopup.Toggle();
        }

        public bool TryCraftRobot(RobotRecipe recipe) {
            if (recipe == null) return false;

            InventoryComponent sourceInventory = playerInventory ?? internalInventory;
            if (sourceInventory == null) {
                Debug.LogError("No inventory available for crafting!");
                return false;
            }

            if (!recipe.CanCraft(sourceInventory)) return false;

            recipe.ConsumeResources(sourceInventory);

            Robot.Robot robot = new RobotBuilder()
                .WithType(recipe.robotType)
                .AtPosition(spawnPoint.position)
                .WithRotation(spawnPoint.rotation)
                .WithName($"{recipe.displayName}-{Guid.NewGuid().ToString().Substring(0,4)}")
                .WithStats(recipe.moveSpeed, recipe.workSpeed, recipe.efficiency, recipe.workRange)
                .WithEnergy(recipe.maxEnergy, recipe.energyDrainRate, recipe.rechargeRate, recipe.lowEnergyThreshold)
                .WithTaskCapacity(recipe.maxQueuedTasks)
                .WithColor(recipe.robotColor)
                .Build();

            RobotManager.instance?.RegisterRobot(robot);
            robot.SetHomePosition(transform.position);

            Debug.Log($"Crafted new {recipe.displayName} robot!");
            return true;
        }

        public void RecallAllRobots() {
            var robots = RobotManager.instance?.GetRobots();
            if (robots == null) return;

            foreach (var robot in robots) {
                robot.ClearAllTasks();
                robot.ReturnToBaseToRecharge();
            }
        }

        public InventoryComponent GetPlayerInventory() => playerInventory;
        public string GetComputerName() => computerName;
        public float GetInteractionRange() => interactionRange;
        public string GetInteractionPrompt() => $"Use {computerName}";
    }
}