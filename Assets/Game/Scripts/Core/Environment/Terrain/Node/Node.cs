using System;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Robot;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Scripts.Core.Environment.Terrain.Node
{
    public class Node : MonoBehaviour, IInteractable
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

        private void Update()
        {
            // Only check proximity if buried and not yet revealed
            if (!isBuried || isRevealed) return;

            // Throttle checks to every 0.2 seconds (adjust as needed)
            if (Time.time < nextProximityCheckTime) return;
            nextProximityCheckTime = Time.time + 0.2f;

            Vector3 playerPos = GameEvents.GetPlayerPosition();
            if (playerPos == Vector3.zero) return;

            float sqrDist = (transform.position - playerPos).sqrMagnitude;
            if (sqrDist <= revealRadius * revealRadius)
            {
                Reveal();
            }
        }

        private void Reveal()
        {
            if (isRevealed) return;
            isRevealed = true;
            SetVisible(true);

            // Destroy the buried visual if present
            if (buriedVisualInstance != null)
                Destroy(buriedVisualInstance);

            Debug.Log($"Node '{nodeName}' revealed at {transform.position}");
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