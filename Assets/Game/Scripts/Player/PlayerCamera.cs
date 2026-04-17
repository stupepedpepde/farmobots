using UnityEngine;

namespace Game.Scripts.Player {
    public struct CameraInput {
        public Vector2 Look;
    }

    public class PlayerCamera : MonoBehaviour {
        [SerializeField] private float sensitivity = 15.0f;
        
        private Vector3 eulerAngles;
        
        public void Initialize(Transform target) {
            transform.position = target.position;
            transform.eulerAngles = eulerAngles = target.eulerAngles;
        }

        public void UpdatePosition(Transform target) {
            transform.position = target.position;
        }

        public void UpdateRotation(CameraInput input, float deltaTime) {
            eulerAngles += new Vector3(-input.Look.y, input.Look.x) * sensitivity * deltaTime;
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -90.0f, 90.0f);
            
            transform.eulerAngles = eulerAngles;
        }
    }
}
