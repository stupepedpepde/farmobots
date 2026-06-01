using System;
using Game.Scripts.Plants;
using UnityEngine;

namespace Game.Scripts.Planting {
    public class PlantableSpot : MonoBehaviour {
        public bool isOccupied { get; private set; }
        public Plant currentPlant { get; private set; }
        private Plant multiTilePlantOwner;

        public bool TryPlant(PlantSO plantSO) {
            if (isOccupied) return false;

            GameObject plantObject = new GameObject($"Plant_{plantSO.plantName}");
            plantObject.transform.SetParent(transform);
            plantObject.transform.localPosition = Vector3.zero;
            plantObject.transform.localRotation = Quaternion.identity;

            Plant plant = plantObject.AddComponent<Plant>();
            plant.SetPlantSO(plantSO);
            plant.SetOccupiedSpot(this); // Link the spot

            plant.Initialize();
            isOccupied = true;
            currentPlant = plant;
            multiTilePlantOwner = plant; // So Clear works

            return true;
        }

        public void Occupy(Plant plant) {
            if (isOccupied) return;

            isOccupied = true;
            currentPlant = plant;
            multiTilePlantOwner = plant;
            plant.SetOccupiedSpot(this); // Link the spot
        }

        public void Clear() {
            if (currentPlant != null && multiTilePlantOwner != null) {
                if (currentPlant.transform.parent == transform)
                    Destroy(currentPlant.gameObject);
            } else if (currentPlant != null) {
                Destroy(currentPlant.gameObject);
            }

            isOccupied = false;
            currentPlant = null;
            multiTilePlantOwner = null;
        }
    }
}