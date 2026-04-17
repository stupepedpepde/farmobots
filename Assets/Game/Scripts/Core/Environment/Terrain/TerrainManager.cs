using System;
using System.Threading.Tasks;
using Game.Scripts.Core.Building;
using UnityEngine;

namespace Game.Scripts.Core.Environment.Terrain {
    public interface ITerrainGenerator {
        public Task GenerateTerrainAsync();
        public void Regenerate();
        public float GetHeightAtWorldPosition(Vector3 worldPos);
        public Vector3 GetNormalAtWorldPosition(Vector3 worldPos);
    }

    [Serializable]
    public class TerrainSettings {
        [Header("Terrain Dimensions")]
        public int terrainSize = 1000;
        public float heightMultiplier = 200f;
        public int heightmapResolution = 513;
        [Space]
        [Header("Fractal Noise Settings")]
        public float baseFrequency = 0.001f;
        public float noiseAmplitude = 1f;
        public int octaves = 6;
        public float persistence = 0.5f;
        public float lacunarity = 2.0f;
        [Space]
        [Header("Randomization")]
        public int seed = 0;
        public bool useRandomSeed = true;
        [Space]
        [Header("Material")]
        public Material terrainMaterial;
        public bool useStandardTexturing = false;
        [Space]
        [Header("Texturing")]
        public Texture2D[] terrainTextures;
        public AnimationCurve textureHeightBlending = AnimationCurve.Linear(0, 0, 1, 1);
        [Space]
        [Header("Performance")]
        public bool useFastNoise = true;
    }

    public class TerrainManager : MonoBehaviour, IInitializable {
        public static TerrainManager instance { get; private set; }

        [SerializeField] private TerrainSettings settings;

        private UnityTerrainWrapper wrapper;
        private TerrainData data;
        private bool isGenerating;

        public event Action<UnityEngine.Terrain> OnTerrainGenerated;
        public event Action OnTerrainGenerationStarted;
        public event Action OnTerrainGenerationCompleted;

        public bool IsTerrainGenerated { get; private set; } = false;

        public TerrainSettings CurrentSettings => settings;

        public int InitializationOrder => 10;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            wrapper = new UnityTerrainWrapper();

            OnTerrainGenerationStarted += () => Debug.Log($"Terrain generation started.");
            OnTerrainGenerationCompleted += () => Debug.Log($"Terrain generation finished.");
            OnTerrainGenerated += data => Debug.Log($"Terrain generated successfully");

            _ = GenerateInitialTerrainAsync();
        }

        public void OnDestroy() {
            wrapper?.Cleanup();

            GameManager.instance?.Unregister(this as IInitializable);
        }

        public async Task GenerateInitialTerrainAsync() {
            if (isGenerating) return;

            isGenerating = true;
            OnTerrainGenerationStarted?.Invoke();

            try {
                await wrapper.GenerateTerrainAsync(settings, this.transform);
                OnTerrainGenerated?.Invoke(wrapper.UnityTerrain);
            }
            catch (Exception ex) {
                Debug.LogError($"Terrain generation failed: {ex.Message}");
            }
            finally {
                isGenerating = false;
                IsTerrainGenerated = true;
                OnTerrainGenerationCompleted?.Invoke();
            }
        }

        public void Regenerate() {
            if (isGenerating) return;

            _ = GenerateInitialTerrainAsync();
        }

        public void GenerateWithSettings(TerrainSettings newSettings) {
            if (isGenerating) return;
            settings = newSettings;
            IsTerrainGenerated = false;
            _ = GenerateInitialTerrainAsync();
        }

        public float GetHeightAt(Vector3 worldPos) {
            return wrapper?.GetHeightAtWorldPosition(worldPos) ?? 0.0f;
        }

        public Vector3 GetNormalAt(Vector3 worldPosition) {
            return wrapper?.GetNormalAtWorldPosition(worldPosition) ?? Vector3.up;
        }

        public bool IsPositionOnTerrain(Vector3 worldPosition) {
            return wrapper?.IsPositionOnTerrain(worldPosition) ?? false;
        }

        public void ModifyHeightAt(Vector3 worldPos, float radius, float intensity) {
            wrapper?.ModifyHeightArea(worldPos, radius, intensity);
        }

        public void FlattenAroundBuilding(GameObject placedObject, float margin = 2.0f, float blendWith = 3.0f) {
            if (!IsTerrainGenerated || BuildingSystem.instance == null) return;

            Bounds bounds = BuildingSystem.instance.CalculateWorldBounds(placedObject);
            bounds.Expand(margin * 2.0f);

            float targetHeight = placedObject.transform.position.y;

            wrapper?.FlattenArea(bounds, targetHeight, blendWith, noiseStrength: 0.005f, noiseScale: 0.0005f);
            GameEvents.TerrainModified(bounds, targetHeight);
        }
    }
}