using System;
using UnityEngine;

namespace Game.Scripts.Core.Environment {
    public class AtmosphereManager : MonoBehaviour, IInitializable {
        public static AtmosphereManager instance { get; private set; }

        [Header("Atmosphere Settings")]
        [SerializeField] private float maxOxygen = 100f;       // 100% = habitable
        [SerializeField] private float currentOxygen = 0f;
        [SerializeField] private float targetOxygen = 100f;

        public float OxygenPercentage => currentOxygen / maxOxygen;

        public event Action<float> OnOxygenChanged;

        public int InitializationOrder => 10;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            // DontDestroyOnLoad(gameObject);
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            // OnOxygenChanged += f => { Debug.Log($"Oxygen changed to {f}"); };
        }

        private void OnDestroy() {
            GameManager.instance?.Unregister(this as IInitializable);
        }

        public void AddOxygen(float amount) {
            if (amount <= 0) return;
            currentOxygen = Mathf.Clamp(currentOxygen + amount, 0, maxOxygen);
            OnOxygenChanged?.Invoke(OxygenPercentage);
        }

        public void SetOxygen(float value) {
            currentOxygen = Mathf.Clamp(value, 0, maxOxygen);
            OnOxygenChanged?.Invoke(OxygenPercentage);
        }

        public float GetCurrentOxygen() => currentOxygen;
        public float GetMaxOxygen() => maxOxygen;
    }
}