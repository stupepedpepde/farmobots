using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Robot;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Scripts.Planting {
    public class Plantable : MonoBehaviour {
        [SerializeField] private Vector2Int gridSize;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float spotHeight = 0.5f;
        [SerializeField] private GameObject Spot;

        private PlantableSpot[,] plantingSpots;
        private List<PlantableSpot> allSpots = new List<PlantableSpot>();

        private void Awake() {
            RobotManager.instance?.RegisterPlantable(this);

            InitializeGrid();
        }

        private void OnDestroy() {
            RobotManager.instance?.UnregisterPlantable(this);
        }

        private void InitializeGrid() {
            plantingSpots = new PlantableSpot[gridSize.x, gridSize.y];
            allSpots = new List<PlantableSpot>();
            
            for (int x = 0; x < gridSize.x; x++)
                for (int z = 0; z < gridSize.y; z++) {
                    GameObject instance = Instantiate(Spot, transform);
                    instance.transform.SetParent(transform);
                    instance.transform.localScale = new Vector3(cellSize * 0.9f, spotHeight, cellSize * 0.9f);
                    instance.transform.localPosition = GetCellPos(x, z);

                    PlantableSpot pSpot = instance.GetComponent<PlantableSpot>();
                    if (pSpot == null) pSpot = instance.GetComponent<PlantableSpot>();

                    plantingSpots[x, z] = pSpot;
                    allSpots.Add(pSpot);
                    
                    if (instance.GetComponent<Collider>() != null) Destroy(instance.GetComponent<Collider>());
                }
        }

        public Vector2Int WorldToGridPosition(Vector3 worldPos) {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float halfGridX = (gridSize.x * cellSize) / 2f;
            float halfGridY = (gridSize.y * cellSize) / 2f;

            int x = Mathf.FloorToInt((localPos.x + halfGridX) / cellSize);
            int y = Mathf.FloorToInt((localPos.z + halfGridY) / cellSize);

            return new Vector2Int(Mathf.Clamp(x, 0, gridSize.x - 1), Mathf.Clamp(y, 0, gridSize.y - 1));
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPos) => transform.TransformPoint(GetCellPos(gridPos.x, gridPos.y));

        public Vector3 GetCellCenterWorld(int x, int z) => transform.TransformPoint(GetCellPos(x, z));

        private Vector3 GetCellPos(int x, int z) {
            return new Vector3(
                (x - (gridSize.x - 1) * 0.5f) * cellSize,
                spotHeight * 0.5f,
                (z - (gridSize.y - 1) * 0.5f) * cellSize
            );
        }

        public float CellSize => cellSize;
        public Vector2Int GridSize => gridSize;
        public PlantableSpot[,] PlantingSpots => plantingSpots;
        public List<PlantableSpot> AllSpots => allSpots;
    }
}