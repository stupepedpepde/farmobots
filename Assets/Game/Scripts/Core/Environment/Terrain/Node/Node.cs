using System;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Robot;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Scripts.Core.Environment.Terrain.Node
{
    public class Node : MonoBehaviour, IInteractable, IUpdatable
    {
        [SerializeField] private string nodeName = "Node";
        public NodeType nodeType { get; private set; }
        [Space]
        [Header("Loot")]
        [SerializeField] private ItemDetails[] possibleDrops;
        [SerializeField] private int minQuantity = 1;
        [SerializeField] private int maxQuantity = 2;
        [Space]
        [Header("Mining")]
        [SerializeField] private float miningTime = 2.0f;
        [Space]
        [Header("Buried Behaviour (configured by NodeType)")]
        [SerializeField] private bool isBuried;
        [SerializeField] private float revealRadius = 5f;
        [SerializeField] private GameObject buriedVisualPrefab;

        // Runtime state
        private ItemDetails selectedItem;
        private int quantity;
        private bool isMined = false;
        private bool isRevealed = false;

        private GameObject highlightBoxInstance;
        private float highlightEndTime;

        // Cached components
        private MeshRenderer[] renderers;
        private Collider interactionCollider;
        private GameObject buriedVisualInstance;

        // Proximity check throttling
        private float nextProximityCheckTime;

        public float MiningTime => miningTime;
        public bool IsBuried => isBuried;
        public bool IsRevealed => isRevealed;
        public float GetRevealRadius() => revealRadius;

        private void Awake()
        {
            GameManager.instance?.Register(this as IInteractable);
            GameManager.instance?.Register(this as IUpdatable);
            RobotManager.instance?.RegisterNode(this);

            // Cache renderers and interaction collider
            renderers = GetComponentsInChildren<MeshRenderer>();
            interactionCollider = GetComponent<Collider>();

            // Initialize loot if manually placed (not spawned via NodeSpawner)
            if (possibleDrops != null && possibleDrops.Length > 0 && selectedItem == null)
                InitializeLoot();

            // Apply buried behaviour
            if (isBuried)
            {
                SetVisible(false);
                // Spawn the optional buried visual (e.g., a dirt mound)
                if (buriedVisualPrefab != null)
                {
                    buriedVisualInstance = Instantiate(buriedVisualPrefab, transform);
                    buriedVisualInstance.transform.localPosition = Vector3.zero;
                    buriedVisualInstance.transform.localRotation = Quaternion.identity;
                }
                // Schedule first proximity check
                nextProximityCheckTime = Time.time;
            }
            else
            {
                SetVisible(true);
            }
        }

        private void InitializeLoot()
        {
            if (possibleDrops != null && possibleDrops.Length > 0)
            {
                selectedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
                quantity = Random.Range(minQuantity, maxQuantity + 1);
            }
            else
            {
                Debug.LogWarning($"Node {name} has no possible drops configured!");
            }
        }

        /// <summary>
        /// Called by NodeSpawner after instantiation to assign properties from a NodeType asset.
        /// </summary>
        public void SetupNode(NodeType type)
        {
            nodeType = type;
            possibleDrops = type.possibleDrops;
            minQuantity = type.minQuantity;
            maxQuantity = type.maxQuantity;
            miningTime = type.miningTime;
            isBuried = type.isBuried;
            revealRadius = type.revealRadius;
            buriedVisualPrefab = type.buriedVisualPrefab;

            InitializeLoot();
        }

        private void SetVisible(bool visible)
        {
            if (renderers != null)
            {
                foreach (var rend in renderers)
                    if (rend != null) rend.enabled = visible;
            }
            // Disable the interaction collider when hidden, so player cannot mine it early
            if (interactionCollider != null)
                interactionCollider.enabled = visible;
        }

        public void OnUpdate(float deltaTime)
        {
            // Only check proximity if buried and not yet revealed
            if (!isBuried || isRevealed) return;

            // Throttle checks to every 0.2 seconds
            if (Time.time < nextProximityCheckTime) return;
            nextProximityCheckTime = Time.time + 0.2f;

            // 1. Check player
            Vector3 playerPos = GameEvents.GetPlayerPosition();
            if (playerPos != Vector3.zero)
            {
                float sqrDistToPlayer = (transform.position - playerPos).sqrMagnitude;
                if (sqrDistToPlayer <= revealRadius * revealRadius)
                {
                    Reveal();
                    return;
                }
            }

            // 2. Check robots
            if (RobotManager.instance != null)
            {
                var robots = RobotManager.instance.GetRobots();
                foreach (var robot in robots)
                {
                    if (robot == null) continue;
                    float sqrDistToRobot = (transform.position - robot.transform.position).sqrMagnitude;
                    if (sqrDistToRobot <= revealRadius * revealRadius)
                    {
                        Reveal();
                        return;
                    }
                }
            }
        }

        public void Reveal()
        {
            if (isRevealed) return;
            isRevealed = true;
            SetVisible(true);

            // Destroy the buried visual if present
            if (buriedVisualInstance != null)
                Destroy(buriedVisualInstance);

            Debug.Log($"Node '{nodeName}' revealed at {transform.position}");
        }

         public void Highlight(float duration)
        {
            if (highlightBoxInstance != null)
                Destroy(highlightBoxInstance);

            // Create an ESP wireframe box around the node's bounds
            Bounds bounds = GetNodeBounds();
            highlightBoxInstance = CreateWireframeBox(bounds, Color.red, duration);
            highlightEndTime = Time.time + duration;
        }

        private Bounds GetNodeBounds()
        {
            Bounds totalBounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;

            if (renderers != null && renderers.Length > 0)
            {
                foreach (var rend in renderers)
                {
                    if (rend != null)
                    {
                        if (!hasBounds)
                            totalBounds = rend.bounds;
                        else
                            totalBounds.Encapsulate(rend.bounds);
                        hasBounds = true;
                    }
                }
            }

            if (!hasBounds && TryGetComponent<Collider>(out var col))
            {
                totalBounds = col.bounds;
                hasBounds = true;
            }

            if (!hasBounds)
                totalBounds = new Bounds(transform.position, Vector3.one * 0.5f);

            return totalBounds;
        }

        private GameObject CreateWireframeBox(Bounds bounds, Color color, float duration)
        {
            GameObject boxObj = new GameObject("NodeHighlightBox");
            boxObj.transform.position = bounds.center;
            boxObj.transform.rotation = Quaternion.identity;

            Vector3 size = bounds.size;
            var lr = boxObj.AddComponent<LineRenderer>();
            lr.positionCount = 24; // 12 edges * 2 points each
            lr.useWorldSpace = true;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;

            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(-size.x, -size.y, -size.z) * 0.5f;
            corners[1] = new Vector3( size.x, -size.y, -size.z) * 0.5f;
            corners[2] = new Vector3( size.x, -size.y,  size.z) * 0.5f;
            corners[3] = new Vector3(-size.x, -size.y,  size.z) * 0.5f;
            corners[4] = new Vector3(-size.x,  size.y, -size.z) * 0.5f;
            corners[5] = new Vector3( size.x,  size.y, -size.z) * 0.5f;
            corners[6] = new Vector3( size.x,  size.y,  size.z) * 0.5f;
            corners[7] = new Vector3(-size.x,  size.y,  size.z) * 0.5f;

            // Define edges: pairs of corner indices
            int[,] edges = new int[,] {
                {0,1}, {1,2}, {2,3}, {3,0}, // bottom face
                {4,5}, {5,6}, {6,7}, {7,4}, // top face
                {0,4}, {1,5}, {2,6}, {3,7}  // vertical edges
            };

            Vector3[] points = new Vector3[24];
            for (int i = 0; i < 12; i++)
            {
                points[i*2] = bounds.center + corners[edges[i,0]];
                points[i*2+1] = bounds.center + corners[edges[i,1]];
            }
            lr.SetPositions(points);

            // Auto-destroy after duration
            Destroy(boxObj, duration);
            return boxObj;
        }

        public void OnInteract()
        {
            // For buried nodes, only allow interaction if revealed
            if (isBuried && !isRevealed) return;
            if (!HasLoot()) return;

            Item item = CollectLoot();
            var playerInv = InventoryService.PlayerInventory;
            if (playerInv != null) playerInv.TryAddItem(item);
        }

        public Item CollectLoot()
        {
            if (!HasLoot()) return null;

            Item item = selectedItem.Create(quantity);
            Debug.Log($"Mined {quantity} x {selectedItem.ItemName} from node {name}");

            isMined = true;
            Destroy(gameObject);
            return item;
        }

        public bool HasLoot() => !isMined && selectedItem != null;

        public float GetInteractionRange()
        {
            // If buried and not revealed, cannot interact at all
            return (isBuried && !isRevealed) ? 0f : 0.5f;
        }

        public string GetInteractionPrompt() => isBuried && !isRevealed ? "?" : $"Mine {nodeName}";

        private void OnDestroy()
        {
            GameManager.instance?.Unregister(this as IInteractable);
            RobotManager.instance?.UnregisterNode(this);
        }
    }
}