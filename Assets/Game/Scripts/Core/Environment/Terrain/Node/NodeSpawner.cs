using System.Collections;
using System.Collections.Generic;
using Game.Scripts.Robot;
using UnityEngine;

namespace Game.Scripts.Core.Environment.Terrain.Node {
    public class NodeSpawner : MonoBehaviour, IInitializable {
        public static NodeSpawner instance { get; private set; }

        [Header("Node Settings")]
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private int numberOfNodes = 200;
        [SerializeField] private float minDistanceBetweenNodes = 10f;

        private List<Vector3> spawnedPositions = new List<Vector3>();
        private List<GameObject> spawnedNodes = new List<GameObject>();
        private bool hasSpawned = false;

        public int InitializationOrder => 9;

        private void Awake() {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize() {
            StartCoroutine(WaitForTerrainAndSpawn());
        }

        private IEnumerator WaitForTerrainAndSpawn() {
            while (TerrainManager.instance == null) yield return null;
            if (TerrainManager.instance.IsTerrainGenerated)
                SpawnNodes();
            else
                TerrainManager.instance.OnTerrainGenerationCompleted += SpawnNodes;
        }

        private void SpawnNodes() {
            if (hasSpawned) return;
            hasSpawned = true;
            if (TerrainManager.instance != null)
                TerrainManager.instance.OnTerrainGenerationCompleted -= SpawnNodes;

            if (nodePrefab == null) { Debug.LogError("Node prefab not assigned!"); return; }
            if (TerrainManager.instance == null) return;

            ClearAllNodes();

            for (int i = 0; i < numberOfNodes; i++) {
                Vector3 randomPos = GetRandomTerrainPosition(TerrainManager.instance, out Vector3 normal);
                if (IsValidSpawnPosition(randomPos)) {
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

                    rotation *= Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                    GameObject newNode = Instantiate(nodePrefab, randomPos, rotation, transform);
                    spawnedNodes.Add(newNode);
                    spawnedPositions.Add(randomPos);

                    Node nodeComp = newNode.GetComponent<Node>();
                    if (nodeComp != null)
                        RobotManager.instance?.RegisterNode(nodeComp);
                }
                else {
                    i--;
                    if (spawnedPositions.Count > 1000) break;
                }
            }

            Debug.Log($"Spawned {spawnedNodes.Count} resource nodes.");
        }

        private Vector3 GetRandomTerrainPosition(TerrainManager terrainManager, out Vector3 normal) {
            float terrainSize = terrainManager.CurrentSettings.terrainSize;
            float x = Random.Range(0, terrainSize);
            float z = Random.Range(0, terrainSize);
            Vector3 worldPos = new Vector3(x, 0, z);
            float y = terrainManager.GetHeightAt(worldPos);
            normal = terrainManager.GetNormalAt(worldPos);
            return new Vector3(x, y, z);
        }

        private bool IsValidSpawnPosition(Vector3 pos) {
            foreach (var existing in spawnedPositions)
                if (Vector3.Distance(existing, pos) < minDistanceBetweenNodes)
                    return false;
            return true;
        }

        public void ClearAllNodes() {
            foreach (var node in spawnedNodes)
                if (node != null) Destroy(node);
            spawnedNodes.Clear();
            spawnedPositions.Clear();
            hasSpawned = false;
        }

        private void OnDestroy() {
            if (TerrainManager.instance != null)
                TerrainManager.instance.OnTerrainGenerationCompleted -= SpawnNodes;

            GameManager.instance?.Unregister(this as IInitializable);
        }
    }
}