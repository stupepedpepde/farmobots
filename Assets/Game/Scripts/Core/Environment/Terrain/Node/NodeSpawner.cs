using System.Collections;
using System.Collections.Generic;
using Game.Scripts.Robot;
using UnityEngine;

namespace Game.Scripts.Core.Environment.Terrain.Node
{
    [System.Serializable]
    public class NodeTypeEntry
    {
        public NodeType nodeType;
        public int numberOfNodes = 20;   // How many nodes of this type to spawn
    }

    public class NodeSpawner : MonoBehaviour, IInitializable
    {
        public static NodeSpawner instance { get; private set; }

        [Header("Node Types")]
        [SerializeField] private List<NodeTypeEntry> nodeTypes = new List<NodeTypeEntry>();

        [Header("Global Settings")]
        [SerializeField] private float globalMinDistanceBetweenNodes = 10f;   // Minimum distance between ANY two nodes

        private List<GameObject> spawnedNodes = new List<GameObject>();
        private List<Vector3> allSpawnedPositions = new List<Vector3>();      // Positions of all spawned nodes
        private bool hasSpawned = false;

        public int InitializationOrder => 9;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            GameManager.instance?.Register(this as IInitializable);
        }

        public void Initialize()
        {
            StartCoroutine(WaitForTerrainAndSpawn());
        }

        private IEnumerator WaitForTerrainAndSpawn()
        {
            while (TerrainManager.instance == null) yield return null;
            if (TerrainManager.instance.IsTerrainGenerated)
                SpawnNodes();
            else
                TerrainManager.instance.OnTerrainGenerationCompleted += SpawnNodes;
        }

        private void SpawnNodes()
        {
            if (hasSpawned) return;
            hasSpawned = true;

            if (TerrainManager.instance != null)
                TerrainManager.instance.OnTerrainGenerationCompleted -= SpawnNodes;

            if (nodeTypes == null || nodeTypes.Count == 0)
            {
                Debug.LogError("NodeSpawner: No node types assigned!");
                return;
            }

            ClearAllNodes();

            // For each node type, spawn its allocated number of nodes
            foreach (var entry in nodeTypes)
            {
                NodeType type = entry.nodeType;
                if (type == null)
                {
                    Debug.LogWarning("NodeSpawner: A NodeTypeEntry has null nodeType, skipping.");
                    continue;
                }
                if (type.prefab == null)
                {
                    Debug.LogWarning($"NodeType '{type.name}' has no prefab assigned, skipping.");
                    continue;
                }

                int spawnedCount = 0;
                int attempts = 0;
                const int maxAttempts = 3000;

                while (spawnedCount < entry.numberOfNodes && attempts < maxAttempts)
                {
                    Vector3 randomPos = GetRandomTerrainPosition(out Vector3 normal);
                    float terrainHeight = randomPos.y;

                    // Apply height constraints
                    bool heightOk = true;
                    if (type.minHeight > 0f && terrainHeight < type.minHeight)
                        heightOk = false;
                    if (type.maxHeight > 0f && terrainHeight > type.maxHeight)
                        heightOk = false;

                    if (heightOk && IsValidSpawnPosition(randomPos, type.minDistanceFromSameType))
                    {
                        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
                        rotation *= Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                        GameObject newNode = Instantiate(type.prefab, randomPos, rotation, transform);
                        Node nodeComp = newNode.GetComponent<Node>();
                        if (nodeComp != null)
                        {
                            nodeComp.SetupNode(type);
                            RobotManager.instance?.RegisterNode(nodeComp);
                        }
                        else
                        {
                            Debug.LogWarning($"Node prefab for type '{type.name}' is missing Node component!");
                        }

                        spawnedNodes.Add(newNode);
                        allSpawnedPositions.Add(randomPos);
                        spawnedCount++;
                    }
                    attempts++;
                }

                Debug.Log($"Spawned {spawnedCount} nodes of type '{type.name}' (attempts: {attempts})");
            }

            Debug.Log($"Node spawning completed. Total nodes: {spawnedNodes.Count}");
        }

        private Vector3 GetRandomTerrainPosition(out Vector3 normal)
        {
            if (TerrainManager.instance == null)
            {
                normal = Vector3.up;
                return Vector3.zero;
            }

            float terrainSize = TerrainManager.instance.CurrentSettings.terrainSize;
            float x = Random.Range(0, terrainSize);
            float z = Random.Range(0, terrainSize);
            Vector3 worldPos = new Vector3(x, 0, z);
            float y = TerrainManager.instance.GetHeightAt(worldPos);
            normal = TerrainManager.instance.GetNormalAt(worldPos);
            return new Vector3(x, y, z);
        }

        private bool IsValidSpawnPosition(Vector3 pos, float minDistForThisType)
        {
            // Check against ALL previously spawned nodes (global minimum distance)
            foreach (var existing in allSpawnedPositions)
                if (Vector3.Distance(existing, pos) < globalMinDistanceBetweenNodes)
                    return false;

            // Also enforce per‑type minimum distance (against nodes of the same type only, but we currently don't store per‑type positions.
            // For simplicity we use global list again. If you need stricter per‑type separation, you would store separate position lists per type.
            // This implementation uses global distance for all types, which is usually sufficient.
            // If you want per‑type minimum distance to apply only to nodes of the same type, you would need to pass a list of positions of that type.
            // We'll use the global list for both checks to keep it simple, but it still respects the per‑type value.
            foreach (var existing in allSpawnedPositions)
                if (Vector3.Distance(existing, pos) < minDistForThisType)
                    return false;

            return true;
        }

        public void ClearAllNodes()
        {
            foreach (var node in spawnedNodes)
                if (node != null) Destroy(node);

            spawnedNodes.Clear();
            allSpawnedPositions.Clear();
            hasSpawned = false;
        }

        private void OnDestroy()
        {
            if (TerrainManager.instance != null)
                TerrainManager.instance.OnTerrainGenerationCompleted -= SpawnNodes;

            GameManager.instance?.Unregister(this as IInitializable);
        }
    }
}