using Game.Scripts.Player;
using UnityEngine;

namespace Game.Scripts.Core.Environment
{
    public class RainParticleSystem : MonoBehaviour, IInitializable, ILateUpdatable
    {
        [Header("References")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private Transform particleTarget;

        [Header("Settings")]
        [SerializeField] private float followSmoothing = 10f;

        private bool isRaining;

        public int InitializationOrder => 20; // after Player and WeatherManager

        private void Awake()
        {
            GameManager.instance?.Register(this as IInitializable);

        }

        private void OnDestroy()
        {
            if (WeatherManager.instance != null)
                WeatherManager.instance.OnWeatherChanged -= OnWeatherChanged;

            GameManager.instance?.Unregister(this as IInitializable);
            GameManager.instance?.Unregister(this as ILateUpdatable);
        }

        public void Initialize()
        {
            GameManager.instance?.Register(this as ILateUpdatable);

            if (rainParticles == null)
                rainParticles = GetComponent<ParticleSystem>();

            if (rainParticles == null)
            {
                Debug.LogError("RainParticleSystem: No ParticleSystem found!", this);
                enabled = false;
                return;
            }

            // Ensure particles are world-aligned (disable local rotation inheritance)
            var main = rainParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Find particle target from PlayerCharacter if not assigned
            if (particleTarget == null)
            {
                var playerChar = FindObjectOfType<PlayerCharacter>();
                if (playerChar != null)
                    particleTarget = playerChar.GetParticleTarget();
            }

            if (particleTarget == null)
            {
                Debug.LogWarning("RainParticleSystem: No particle target assigned or found. Rain won't follow the player.", this);
                enabled = false;
                return;
            }

            // Start disabled until rain begins
            rainParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            enabled = false;

            // Subscribe to weather changes
            if (WeatherManager.instance != null)
            {
                WeatherManager.instance.OnWeatherChanged += OnWeatherChanged;
                // Initial check
                OnWeatherChanged(WeatherManager.instance.GetCurrentWeather);
            }
            else
                Debug.LogWarning("WeatherManager not found, rain will not respond to weather.");
        }

        private void OnWeatherChanged(WeatherType weather)
        {
            bool newRaining = (weather == WeatherType.RAIN);
            if (newRaining == isRaining) return;

            isRaining = newRaining;
            if (isRaining)
            {
                rainParticles.Play();
                enabled = true;

                RenderSettings.fogColor = new Color(0.25f, 0.25f, 0.30f);
            }
            else
            {
                rainParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                enabled = false;

                RenderSettings.fogColor = new Color(0.45f, 0.45f, 0.50f);
            }
        }

        public void OnLateUpdate(float deltaTime)
        {
            if (!isRaining || particleTarget == null) return;

            // Smoothly follow the target's position, keep rotation fixed (world aligned)
            transform.position = Vector3.Lerp(transform.position, particleTarget.position, followSmoothing * Time.deltaTime);
            // Ensure rotation stays identity so particles are world-up aligned
            transform.rotation = Quaternion.identity;
        }
    }
}