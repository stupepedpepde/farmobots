using System;
using Game.Scripts.Core.Building;
using Game.Scripts.Core.HUD;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory;
using Game.Scripts.Planting;
using Game.Scripts.Player;
using Game.Scripts.Robot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Scripts.Core.Input {
    public class InputManager : MonoBehaviour, IInitializable, IUpdatable, IUnpausable {
        public static InputManager instance { get; private set; }

        private InputActions inputActions;

        public CharacterInput characterInput { get; private set; }
        public CameraInput cameraInput { get; private set; }

        private IInteractable currentLookAtInteractable;
        private float interactionCheckTimer;
        private const float INTERACTION_CHECK_INTERVAL = 0.1f;

        public int InitializationOrder => 5;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            inputActions = new InputActions();
            inputActions.Enable();

            GameManager.instance?.Register(this as IUpdatable);
        }

        private void OnDestroy() {
            inputActions?.Dispose();

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as IUpdatable);
        }

        public void OnUpdate(float deltaTime) {
            UpdatePlayerInputState();
            UpdateGeneral();
            UpdateInteractions(deltaTime);
        }

        private void UpdatePlayerInputState() {
            cameraInput = new CameraInput() {
                Look = inputActions.PlayerActions.Look.ReadValue<Vector2>()
            };

            characterInput = new CharacterInput() {
                Rotation = Quaternion.Euler(inputActions.PlayerActions.Look.ReadValue<Vector2>()),
                Move = inputActions.PlayerMovement.Move.ReadValue<Vector2>(),
                Jump = inputActions.PlayerMovement.Jump.WasPressedThisFrame(),
                Crouch = inputActions.PlayerMovement.Crouch.IsPressed(),
                Sprint = inputActions.PlayerMovement.Sprint.IsPressed()
            };
        }

        private void UpdateGeneral() {
            if (inputActions.General.Pause.WasPressedThisFrame()) UIManager.instance?.ShowPauseMenu();

            if (inputActions.General.Inventory.WasPressedThisFrame()) {
                if (BuildingSystem.instance != null && BuildingSystem.instance.IsBuildModeActive) {
                    GameEvents.RequestBuildingUI();
                } else {
                    GameEvents.RequestInventory();
                }
            }

            if (inputActions.PlayerActions.Interact.WasPressedThisFrame())
                RaycastForInteraction();

            if (inputActions.PlayerActions.BuildMode.WasPressedThisFrame())
                BuildingSystem.instance?.SetBuildMode(!BuildingSystem.instance?.GetBuildMode() ?? false);

            if (inputActions.PlayerActions.Place.WasPressedThisFrame()) {
                if (BuildingSystem.instance?.GetBuildMode() ?? true)
                    GameEvents.RequestBuilding();
                else
                    GameEvents.RequestPlanting(InventoryService.PlayerInventory);
            }

            if (inputActions.PlayerActions.SpawnRobot.WasPressedThisFrame())
                RobotBuilder.CreateGardener(GameEvents.GetPlayerPosition() + new Vector3(0.0f, 2.0f, 0.0f));

            if (inputActions.PlayerActions.Rotate.WasPressedThisFrame() && BuildingSystem.instance != null && BuildingSystem.instance.IsBuildModeActive)
                BuildingSystem.instance.RotateGhost();
        }

        private void UpdateInteractions(float deltaTime) {
            interactionCheckTimer += deltaTime;
            if (interactionCheckTimer < INTERACTION_CHECK_INTERVAL) return;
            interactionCheckTimer = 0f;

            IInteractable hit = GetInteractableInSight();
            if (hit != currentLookAtInteractable) {
                currentLookAtInteractable = hit;
                if (hit != null) {
                    string key = inputActions.PlayerActions.Interact.GetBindingDisplayString();
                    HUDManager.instance?.UpdateInteractionPrompt(hit, key);
                } else {
                    HUDManager.instance?.HideInteractionPrompt();
                }
            }
        }

        public bool WasPlayerPlacePressed() => inputActions.PlayerActions.Place.WasPressedThisFrame();

        private IInteractable GetInteractableInSight() {
            if (Camera.main == null) return null;
            float angle = Vector3.Angle(Vector3.up, Camera.main.transform.forward);
            if (angle < 5.0f || angle > 175.0f) return null;

            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out RaycastHit hit, 10.0f, ~0))
                return hit.collider.GetComponentInParent<IInteractable>();

            return null;
        }

        private bool RaycastForInteraction() {
            IInteractable obj = GetInteractableInSight();
            if (obj != null) {
                obj.OnInteract();
                return true;
            }
            return false;
        }
    }
}