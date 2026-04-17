using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.Core {
    public interface IInitializable {
        void Initialize();
        int InitializationOrder { get; }
    }

    public interface IUnpausable {

    }
    
    public interface IUpdatable {
        void OnUpdate(float deltaTime);
    }

    public interface ILateUpdatable {
        void OnLateUpdate(float deltaTime);
    }

    public interface IFixedUpdatable {
        void OnFixedUpdate(float fixedDeltaTime);
    }

    public interface IInteractable {
        void OnInteract();
        float GetInteractionRange();
        string GetInteractionPrompt();
    }

    public enum GameState {
        PLAYING,
        PAUSED,
        LOADING,
        MENU,
        INTERFACE,
        NONE
    }
    
    public class GameManager : MonoBehaviour {
        public static GameManager instance { get; private set; }

        private readonly List<IInitializable> initializables = new List<IInitializable>();
        private readonly List<IUnpausable> unpausables = new List<IUnpausable>();
        private readonly List<IUpdatable> updatables = new List<IUpdatable>();
        private readonly List<ILateUpdatable> lateUpdatables = new List<ILateUpdatable>();
        private readonly List<IFixedUpdatable> fixedUpdatables = new List<IFixedUpdatable>();
        private readonly List<IInteractable> interactables = new List<IInteractable>();

        [SerializeField] private GameState currentState = GameState.LOADING;
        public event Action<GameState> OnGameStateChanged = delegate { };
        
        private bool isInitialized;
        
        private void Start() {
            InitializeAll();
        }

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }
        
        private void InitializeAll() {
            OnGameStateChanged += state => Debug.Log($"Changed game state to: {state}");

            SetGameState(GameState.LOADING);

            var initialize = new List<IInitializable>(initializables);
            initialize.Sort((a, b) => a.InitializationOrder.CompareTo(b.InitializationOrder));

            foreach (var system in initialize) {
                Debug.Log($"Initializing: {system.GetType().Name} (#{system.InitializationOrder})");
                system.Initialize();
            }

            isInitialized = true;
            Debug.Log("All systems initialized!");
            SetGameState(GameState.MENU);
        }

        public void InitializeLate(IInitializable system) {
            if (system == null) return;
            
            Debug.Log($"Late initializing: {system.GetType().Name}");
            system.Initialize();

            if (system is IUnpausable) Register(system as IUnpausable);
            if (system is IUpdatable) Register(system as IUpdatable);
            if (system is ILateUpdatable) Register(system as ILateUpdatable);
            if (system is IFixedUpdatable) Register(system as IFixedUpdatable);
        }

        private void Update() {
            if (!isInitialized) return;

            var deltaTime = UnityEngine.Time.deltaTime;
            if (currentState == GameState.PLAYING || currentState == GameState.INTERFACE)
                for (int i = 0; i < updatables.Count; i++)
                    updatables[i].OnUpdate(deltaTime);
            else if (currentState == GameState.PAUSED)
                for (int i = 0; i < updatables.Count; i++)
                    if (updatables[i] is IUnpausable)
                        updatables[i].OnUpdate(deltaTime);
        }
        
        private void LateUpdate() {
            if (!isInitialized) return;

            var deltaTime = UnityEngine.Time.deltaTime;
            if (currentState == GameState.PLAYING || currentState == GameState.INTERFACE)
                for (int i = 0; i < lateUpdatables.Count; i++)
                    lateUpdatables[i].OnLateUpdate(deltaTime);
            else if (currentState == GameState.PAUSED)
                for (int i = 0; i < lateUpdatables.Count; i++)
                    if (lateUpdatables[i] is IUnpausable)
                        lateUpdatables[i].OnLateUpdate(deltaTime);
        }
        
        private void FixedUpdate() {
            if (!isInitialized) return;

            var deltaTime = UnityEngine.Time.deltaTime;
            if (currentState == GameState.PLAYING || currentState == GameState.INTERFACE)
                for (int i = 0; i < fixedUpdatables.Count; i++)
                    fixedUpdatables[i].OnFixedUpdate(deltaTime);
            else if (currentState == GameState.PAUSED)
                for (int i = 0; i < fixedUpdatables.Count; i++)
                    if (fixedUpdatables[i] is IUnpausable)
                        fixedUpdatables[i].OnFixedUpdate(deltaTime);
        }

        public void Unregister(IInitializable initializable) => initializables.Remove(initializable);
        public void Register(IInitializable initializable) {
            if (initializable == null || initializables.Contains(initializable)) return;

            initializables.Add(initializable);
            if (isInitialized) InitializeLate(initializable);
        }

        public void Unregister(IUnpausable unpausable) => unpausables.Remove(unpausable);
        public void Register(IUnpausable unpausable) {
            if (unpausable == null) return;

            unpausables.Add(unpausable);
        }

        public void Unregister(IUpdatable updatable) => updatables.Remove(updatable);
        public void Register(IUpdatable updatable) {
            if (updatable == null) return;
            updatables.Add(updatable);
        }
        
        public void Unregister(ILateUpdatable lateUpdatable) => lateUpdatables.Remove(lateUpdatable);

        public void Register(ILateUpdatable lateUpdatable) {
            if (lateUpdatable == null) return;
            lateUpdatables.Add(lateUpdatable);
        }

        public void Unregister(IFixedUpdatable fixedUpdatable) => fixedUpdatables.Remove(fixedUpdatable);
        public void Register(IFixedUpdatable fixedUpdatable) {
            if (fixedUpdatable == null) return;
            fixedUpdatables.Add(fixedUpdatable);
        }

        public void Unregister(IInteractable interactable) => interactables.Remove(interactable);
        public void Register(IInteractable interactable) {
            if (interactable == null) return;
            interactables.Add(interactable);
        }


        public void SetGameState(GameState state) {
            if (currentState == state) return;

            currentState = state;
            OnGameStateChanged?.Invoke(state);
        }

        public GameState GetGameState() => currentState;
    }
}