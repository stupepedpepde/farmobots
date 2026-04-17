using System;
using Game.Scripts.Inventory;
using Game.Scripts.Planting;
using Game.Scripts.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Game.Scripts.Core {
    public static class GameEvents {
        // scenes
        public static event Action<object> OnSceneGroupRequested;
        public static event Action<object> OnSceneGroupAddRequested;
        public static event Action<object, GameState> OnUISceneRequested;

        // binds / systems
        public static event Action OnBuildingRequested;
        public static event Action<bool> OnBuildModeChanged;
        public static event Action OnBuildingUIRequested;
        public static event Action<InventoryComponent, PlantableSpot> OnPlantingRequested;

        // terrain
        public static event Action<Bounds, float> OnTerrainModified;

        // player
        private static Func<Vector3> playerPositionProvider;
        private static Func<Quaternion> playerRotationProvider;

        // inventory
        public static event Action<InventoryComponent> OnInventoryRequested;

        # region Scenes

        public static void RequestSceneGroup(object request) => OnSceneGroupRequested?.Invoke(request);

        public static void RequestAddSceneGroup(object request) => OnSceneGroupAddRequested?.Invoke(request);

        public static void RequestUIElement(object request, GameState state = GameState.INTERFACE) => OnUISceneRequested?.Invoke(request, state);

        # endregion

        # region Binds / Systems

        public static void RequestBuilding() => OnBuildingRequested?.Invoke();

        public static void BuildModeChanged(bool active) => OnBuildModeChanged?.Invoke(active);

        public static void RequestBuildingUI() => OnBuildingUIRequested?.Invoke();

        public static void RequestPlanting(InventoryComponent inventory = null, PlantableSpot spot = null) => OnPlantingRequested?.Invoke(inventory, spot);

        # endregion

        #region Terrain

        public static void TerrainModified(Bounds bounds, float height) => OnTerrainModified?.Invoke(bounds, height);

        #endregion

        # region Player
        # region Registration

        public static void RegisterPlayerPositionProvider(Func<Vector3> provider) => playerPositionProvider = provider;
        public static void RegisterPlayerRotationProvider(Func<Quaternion> provider) => playerRotationProvider = provider;

        public static void UnregisterPlayerPositionProvider() => playerPositionProvider = null;
        public static void UnregisterPlayerRotationProvider() => playerRotationProvider = null;

        # endregion

        public static Vector3 GetPlayerPosition() {
            if (playerPositionProvider != null)
                return playerPositionProvider();

            return Vector3.zero;
        }

        public static Quaternion GetPlayerRotation() {
            if (playerRotationProvider != null)
                return playerRotationProvider();

            return Quaternion.identity;
        }

        # endregion

        # region Inventory

        public static void RequestInventory([CanBeNull] InventoryComponent inventory = null) {
            if (inventory != null) Debug.Log($"requested inventory: {inventory.GetID().ToString().Substring(0, 8)}");
            OnInventoryRequested?.Invoke(inventory);
        }

        # endregion
    }
}