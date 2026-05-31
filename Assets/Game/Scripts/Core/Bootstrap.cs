using Game.Scripts.Core.Building;
using Game.Scripts.Core.Environment;
using Game.Scripts.Core.Input;
using Game.Scripts.Inventory;
using Game.Scripts.Planting;
using Game.Scripts.Plants;
using Game.Scripts.Robot;
using UnityEngine.SceneManagement;

using UnityEngine;
using UnityUtils;

namespace Game.Scripts.Core {
    public class Bootstrap : PersistentSingleton<Bootstrap> {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Initialize() {
            Debug.Log("Bootstrap Initialization...");

            CreateSystem<GameManager>("Game Manager");
            CreateSystem<PlantManager>("Plant Manager");
            CreateSystem<RobotManager>("Robot Manager");
            CreateSystem<InputManager>("Input Manager");
            CreateSystem<InventoryManager>("Inventory Manager");
            CreateSystem<AtmosphereManager>("Atmosphere Manager");

            await SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
        }
        
        private static void CreateSystem<T>(string name) where T : Component {
            if (FindObjectOfType<T>() != null) {
                Debug.Log($"{typeof(T).Name} already exists, skipping creation.");
                return;
            }

            var go = new GameObject(name);
            go.AddComponent<T>();
            DontDestroyOnLoad(go);
            Debug.Log($"Created {name}");
        }
    }
}