using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.Environment.Terrain.Node;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class PlayerScanner : MonoBehaviour
    {
        [Header("Scan Settings")]
        [SerializeField] private float scanRadius = 30f;
        [SerializeField] private float scanCooldown = 30f;
        [SerializeField] private float highlightDuration = 5f;
        [SerializeField] private LayerMask nodeLayerMask = -1;

        [Header("Visual Effect")]
        [SerializeField] private Gradient pulseColor = new Gradient();
        [SerializeField] private float pulseDuration = 0.8f;
        [SerializeField] private int pulseSegments = 32;
        [SerializeField] private float pulseLineWidth = 0.1f;

        private float lastScanTime = -Mathf.Infinity;
        private bool isOnCooldown => Time.time < lastScanTime + scanCooldown;

        public float CooldownRemaining => Mathf.Max(0, lastScanTime + scanCooldown - Time.time);
        public float CooldownNormalized => CooldownRemaining / scanCooldown;
        public bool IsReady => !isOnCooldown;

        private void OnEnable()
        {
            GameEvents.OnScanTriggered += PerformScan;
        }

        private void OnDisable()
        {
            GameEvents.OnScanTriggered -= PerformScan;
        }

        private void PerformScan()
        {
            if (isOnCooldown)
            {
                Debug.Log("Scan on cooldown.");
                return;
            }

            lastScanTime = Time.time;
            ExecuteScan();
            SpawnPulseEffect();
        }

        private void ExecuteScan()
        {
            Vector3 playerPos = transform.position;
            Collider[] hits = Physics.OverlapSphere(playerPos, scanRadius, nodeLayerMask);

            foreach (Collider hit in hits)
            {
                Node node = hit.GetComponentInParent<Node>();
                if (node == null) continue;

                // Reveal buried nodes permanently
                if (node.IsBuried && !node.IsRevealed)
                {
                    node.Reveal();
                }

                // Highlight all nodes in range
                node.Highlight(highlightDuration);
            }

            Debug.Log($"Scan completed. Found {hits.Length} nodes within {scanRadius}m.");
        }

        private void SpawnPulseEffect()
        {
            GameObject pulseObj = new GameObject("ScanPulse", typeof(ScanPulseEffect));
            pulseObj.transform.position = transform.position;
            var effect = pulseObj.GetComponent<ScanPulseEffect>();
            effect.Initialize(scanRadius, pulseDuration, pulseSegments, pulseLineWidth, pulseColor);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, scanRadius);
        }
    }

    public class ScanPulseEffect : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private float radius;
        private float duration;
        private float elapsed;
        private int segments;
        private Gradient colorGradient;

        public void Initialize(float maxRadius, float lifeDuration, int segs, float lineWidth, Gradient colorGrad)
        {
            radius = maxRadius;
            duration = lifeDuration;
            segments = segs;
            colorGradient = colorGrad;

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = segments + 1;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.colorGradient = colorGradient;

            UpdateCircle(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentRadius = radius * t;
            UpdateCircle(currentRadius);

            // Fade out alpha
            Color c = colorGradient.Evaluate(t);
            c.a = 1f - t;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;

            if (t >= 1f)
                Destroy(gameObject);
        }

        private void UpdateCircle(float currentRadius)
        {
            Vector3[] positions = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 360f / segments * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * currentRadius;
                float z = Mathf.Sin(angle) * currentRadius;
                positions[i] = transform.position + new Vector3(x, 0.1f, z);
            }
            lineRenderer.SetPositions(positions);
        }
    }
}