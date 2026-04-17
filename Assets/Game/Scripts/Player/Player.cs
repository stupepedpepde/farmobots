using Game.Scripts.Core;
using Game.Scripts.Core.Environment.Terrain;
using Game.Scripts.Core.Input;
using UnityEngine;

namespace Game.Scripts.Player {
    public class Player : MonoBehaviour, IInitializable, IUpdatable, ILateUpdatable  {
        [SerializeField] private PlayerCharacter playerCharacter;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerInventory playerInventory;
        [Space]
        [Header("Building Tool")]
        [SerializeField] private GameObject buildingToolPrefab;
        [SerializeField] private Transform buildingToolAttachPoint;

        private GameObject activeBuildingToolInstance;

        public int InitializationOrder => 10;

        private void Awake() {
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            Cursor.lockState = CursorLockMode.Locked;
            
            playerCharacter.Initialize();
            playerCamera.Initialize(playerCharacter.GetCameraTarget());
            playerInventory.Initialize();

            GameEvents.RegisterPlayerPositionProvider(GetPlayerPosition);
            GameEvents.RegisterPlayerRotationProvider(GetPlayerRotation);
            GameEvents.OnTerrainModified += HandleTerrainModified;
            GameEvents.OnBuildModeChanged += HandleBuildModeChanged;

            GameManager.instance?.Register(this as IUpdatable);
            GameManager.instance?.Register(this as ILateUpdatable);
        }

        private void OnDestroy() {
            GameEvents.UnregisterPlayerPositionProvider();
            GameEvents.UnregisterPlayerRotationProvider();
            GameEvents.OnTerrainModified -= HandleTerrainModified;
            GameEvents.OnBuildModeChanged -= HandleBuildModeChanged;

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
            GameManager.instance?.Unregister(this as ILateUpdatable);
        }

        public void OnUpdate(float deltaTime) {
            if (InputManager.instance == null) return;

            var characterInput = InputManager.instance.characterInput;
            var cameraInput = InputManager.instance.cameraInput;

            if (GameManager.instance?.GetGameState() != GameState.INTERFACE)
                playerCamera.UpdateRotation(cameraInput, deltaTime);

            characterInput.Rotation = playerCamera.transform.rotation;

            playerCharacter.UpdateInput(characterInput);
            playerCharacter.UpdateBody(deltaTime);
        }

        public void OnLateUpdate(float deltaTime) {
            playerCamera.UpdatePosition(playerCharacter.GetCameraTarget());
        }

        private void HandleTerrainModified(Bounds bounds, float height) {
            Vector3 pos = GetPlayerPosition();
            Bounds expandedBounds = bounds;
            expandedBounds.Expand(5.0f);

            if (!expandedBounds.Contains(pos) || !TerrainManager.instance) return;

            float newHeight = TerrainManager.instance.GetHeightAt(pos);
            Vector3 newPos = pos;
            newPos.y = newHeight + 0.1f;

            var motor = playerCharacter.GetComponent<KinematicCharacterController.KinematicCharacterMotor>();
            if (motor != null) {
                motor.SetPosition(newPos);
                motor.BaseVelocity = Vector3.zero;
            } else
                playerCharacter.transform.position = newPos;
        }

        private void HandleBuildModeChanged(bool active) {
            if (active) {
                if (activeBuildingToolInstance == null && buildingToolPrefab != null) {
                    Transform parent = buildingToolAttachPoint != null ? buildingToolAttachPoint : Camera.main?.transform;
                    if (parent == null) {
                        Debug.LogWarning("No attach point for building tool.");
                        return;
                    }

                    activeBuildingToolInstance = Instantiate(buildingToolPrefab, parent);
                    activeBuildingToolInstance.transform.localPosition = Vector3.zero;
                    activeBuildingToolInstance.transform.localRotation = Quaternion.identity;
                }
            } else {
                if (activeBuildingToolInstance != null) {
                    Destroy(activeBuildingToolInstance);
                    activeBuildingToolInstance = null;
                }
            }
        }

        private Vector3 GetPlayerPosition() {
            if (playerCharacter != null)
                return playerCharacter.transform.position;

            return transform.position;
        }

        private Quaternion GetPlayerRotation() {
            if (playerCharacter != null)
                return playerCharacter.transform.rotation;

            return transform.rotation;
        }
    }
}
