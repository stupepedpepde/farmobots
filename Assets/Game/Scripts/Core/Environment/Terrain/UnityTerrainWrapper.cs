using System;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Game.Scripts.Core.Environment.Terrain {
    public class UnityTerrainWrapper : ITerrainGenerator {
        public UnityEngine.Terrain UnityTerrain { get; private set; }
        public TerrainData UnityTerrainData { get; private set; }

        private GameObject terrainGameObject;
        private bool isInitialized;
        private int currentSeed;

        public async Task GenerateTerrainAsync(TerrainSettings settings, Transform parent = null) {
            try {
                currentSeed = settings.useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : settings.seed;
                float[,] heightmap = await Task.Run(() => GenerateFractalHeightmap(settings, currentSeed));

                await SetupTerrain(heightmap, settings, parent);
            } catch (Exception ex) {
                Debug.LogError($"Terrain generation failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public async Task SetupTerrain(float[,] heightmap, TerrainSettings settings, Transform parent = null) {
            await Task.Yield();

            UnityTerrainData = new TerrainData();
            UnityTerrainData.heightmapResolution = settings.heightmapResolution;
            UnityTerrainData.size = new Vector3(settings.terrainSize, settings.heightMultiplier, settings.terrainSize);

            UnityTerrainData.SetHeights(0, 0, heightmap);

            terrainGameObject = UnityEngine.Terrain.CreateTerrainGameObject(UnityTerrainData);
            UnityTerrain = terrainGameObject.GetComponent<UnityEngine.Terrain>();

            if (!settings.useStandardTexturing)
                UnityTerrain.materialTemplate = settings.terrainMaterial;

            if (parent != null) {
                terrainGameObject.transform.SetParent(parent, false);
                terrainGameObject.transform.localPosition = Vector3.zero;
                terrainGameObject.transform.localRotation = Quaternion.identity;
            }

            if (settings.terrainTextures != null && settings.terrainTextures.Length > 0 && settings.useStandardTexturing)
                SetupLayers(settings);

            isInitialized = true;
        }

        private float[,] GenerateFractalHeightmap(TerrainSettings settings, int seed) {
            int res = settings.heightmapResolution;
            float[,] heights = new  float[res, res];

            Random random = new Random(seed);

            float[] frequencies = new float[settings.octaves];
            float[] amplitudes = new float[settings.octaves];

            float frequency = settings.baseFrequency;
            float amplitude = settings.noiseAmplitude;

            for (int i = 0; i < settings.octaves; i++) {
                frequencies[i] = frequency;
                amplitudes[i] = amplitude;

                frequency *= settings.lacunarity;
                amplitude *= settings.persistence;
            }

            Vector2[] octaveOffsets = new Vector2[settings.octaves];
            for (int i = 0; i < settings.octaves; i++) {
                float offsetX = (float)(random.NextDouble() * 200000f - 100000f);
                float offsetY = (float)(random.NextDouble() * 200000f - 100000f);

                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            for (int x = 0; x < res; x++)
                for (int y = 0; y < res; y++)
                    heights[x, y] = GenerateFractalNoise(x, y, frequencies, amplitudes, octaveOffsets, settings);

            return heights;
        }
        private float GenerateFractalNoise(int x, int y, float[] frequencies, float[] amplitudes, Vector2[] octaveOffsets, TerrainSettings settings) {
            float value = 0.0f;
            float max = 0.0f;

            for (int i = 0; i < frequencies.Length; i++) {
                float sampleX = x * frequencies[i] + octaveOffsets[i].x;
                float sampleY = y * frequencies[i] + octaveOffsets[i].y;

                float noise = Mathf.PerlinNoise(sampleX, sampleY);

                if (!settings.useFastNoise) {
                    float warpX = sampleX + 0.3f * Mathf.PerlinNoise(x * 0.001f, y * 0.001f);
                    float warpY = sampleY + 0.3f * Mathf.PerlinNoise(x * 0.001f + 1000.0f, y * 0.001f + 1000.0f);

                    noise = Mathf.PerlinNoise(warpX, warpY);

                    if (i > frequencies.Length / 2) {
                        noise = 1.0f - Mathf.Abs(noise * 2.0f - 1.0f);
                        noise *= noise;
                    }
                }

                value += noise * amplitudes[i];
                max += amplitudes[i];
            }

            return Mathf.Clamp01(value / max);
        }

        private void SetupLayers(TerrainSettings settings) {
            var terrainLayers = new TerrainLayer[settings.terrainTextures.Length];

            for (int i = 0; i < settings.terrainTextures.Length; i++)
                terrainLayers[i] = new TerrainLayer {
                    diffuseTexture = settings.terrainTextures[i],
                    tileSize = new Vector2(15, 15)
                };

            UnityTerrainData.terrainLayers = terrainLayers;

            int alphaWidth = UnityTerrainData.alphamapWidth;
            int alphaHeight = UnityTerrainData.alphamapHeight;
            float[,,] alphaMaps = new float[alphaWidth, alphaHeight, terrainLayers.Length];

            for (int x = 0; x < alphaWidth; x++)
                for (int y = 0; y < alphaHeight; y++) {
                    float normX = (float)x / alphaWidth;
                    float normY = (float)y / alphaHeight;
                    float height = UnityTerrainData.GetInterpolatedHeight(normY, normX) / UnityTerrainData.size.y;

                    for (int i = 0; i < terrainLayers.Length; i++) {
                        float blend = Mathf.Clamp01(settings.textureHeightBlending.Evaluate(height) * (i + 1) / terrainLayers.Length);
                        alphaMaps[x, y, i] = blend;
                    }

                    float sum = 0f;
                    for (int i = 0; i < terrainLayers.Length; i++)
                        sum += alphaMaps[x, y, i];

                    if (sum > 0f)
                        for (int i = 0; i < terrainLayers.Length; i++)
                            alphaMaps[x, y, i] /= sum;
                }

            UnityTerrainData.SetAlphamaps(0, 0, alphaMaps);
        }

        public float GetHeightAtWorldPosition(Vector3 worldPos) {
            if (!isInitialized || UnityTerrain == null) return 0.0f;

            return UnityTerrain.SampleHeight(worldPos);
        }

        public Vector3 GetNormalAtWorldPosition(Vector3 worldPos) {
            if (!isInitialized || UnityTerrain == null) return Vector3.up;

            var terrainLocalPos = worldPos - UnityTerrain.transform.position;
            var normalizePos = new Vector3(terrainLocalPos.x / UnityTerrainData.size.x, terrainLocalPos.z / UnityTerrainData.size.z);

            return UnityTerrainData.GetInterpolatedNormal(normalizePos.x, normalizePos.y);
        }

        public bool IsPositionOnTerrain(Vector3 worldPos) {
            if (!isInitialized || UnityTerrain == null) return false;

            var terrainLocalPos = worldPos - UnityTerrain.transform.position;
            return terrainLocalPos.x >= 0 && terrainLocalPos.x <= UnityTerrainData.size.x && terrainLocalPos.z >= 0 && terrainLocalPos.z <= UnityTerrainData.size.z;
        }

        public void ModifyHeightArea(Vector3 worldPos, float radius, float intensity) {
            if (!isInitialized || UnityTerrain == null) return;

            var terrainLocalPos = worldPos - UnityTerrain.transform.position;
            int heightmapResolution = UnityTerrainData.heightmapResolution;

            int centerX = (int)(terrainLocalPos.x / UnityTerrainData.size.x * heightmapResolution);
            int centerY = (int)(terrainLocalPos.z / UnityTerrainData.size.z * heightmapResolution);
            int brushRadius = (int)(radius / UnityTerrainData.size.x * heightmapResolution);

            int startX = Mathf.Clamp(centerX - brushRadius, 0, heightmapResolution - 1);
            int startY = Mathf.Clamp(centerY - brushRadius, 0, heightmapResolution - 1);
            int width = Mathf.Clamp(brushRadius * 2, 1, heightmapResolution - startX);
            int height = Mathf.Clamp(brushRadius * 2, 1, heightmapResolution - startY);

            float[,] heights = UnityTerrainData.GetHeights(startX, startY, width, height);

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++) {
                    int actualX = startX + x;
                    int actualY = startY + y;
                    float distance = Vector2.Distance(new Vector2(actualX, actualY), new Vector2(centerX, centerY)) / brushRadius;

                    if (distance <= 1f) {
                        float falloff = 1f - (distance * distance);
                        heights[x, y] = Mathf.Clamp01(heights[x, y] + intensity * falloff);
                    }
                }

            UnityTerrainData.SetHeights(startX, startY, heights);
        }

        public void FlattenArea(Bounds worldBounds, float targetHeight, float blendWidth,
            float noiseStrength = 0.1f, float noiseScale = 0.5f)
        {
            if (!isInitialized || UnityTerrain == null)
            {
                Debug.LogWarning("Terrain not initialized. Cannot flatten.");
                return;
            }

            TerrainData terrainData = UnityTerrain.terrainData;
            Vector3 terrainPos = UnityTerrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            int heightmapRes = terrainData.heightmapResolution;

            // Expand bounds to include blend zone
            Bounds expandedBounds = worldBounds;
            expandedBounds.Expand(blendWidth * 2f);

            // Convert to terrain-local space
            Vector3 localMin = expandedBounds.min - terrainPos;
            Vector3 localMax = expandedBounds.max - terrainPos;

            // Clamp to terrain
            localMin.x = Mathf.Clamp(localMin.x, 0, terrainSize.x);
            localMin.z = Mathf.Clamp(localMin.z, 0, terrainSize.z);
            localMax.x = Mathf.Clamp(localMax.x, 0, terrainSize.x);
            localMax.z = Mathf.Clamp(localMax.z, 0, terrainSize.z);

            // Convert to heightmap indices
            int xStart = Mathf.FloorToInt(localMin.x / terrainSize.x * (heightmapRes - 1));
            int zStart = Mathf.FloorToInt(localMin.z / terrainSize.z * (heightmapRes - 1));
            int xEnd   = Mathf.CeilToInt(localMax.x / terrainSize.x * (heightmapRes - 1));
            int zEnd   = Mathf.CeilToInt(localMax.z / terrainSize.z * (heightmapRes - 1));

            xStart = Mathf.Clamp(xStart, 0, heightmapRes - 1);
            zStart = Mathf.Clamp(zStart, 0, heightmapRes - 1);
            xEnd   = Mathf.Clamp(xEnd,   0, heightmapRes - 1);
            zEnd   = Mathf.Clamp(zEnd,   0, heightmapRes - 1);

            if (xStart > xEnd) (xStart, xEnd) = (xEnd, xStart);
            if (zStart > zEnd) (zStart, zEnd) = (zEnd, zStart);

            int patchWidth  = xEnd - xStart + 1;
            int patchDepth  = zEnd - zStart + 1;
            if (patchWidth <= 0 || patchDepth <= 0)
            {
                Debug.LogWarning($"Invalid patch dimensions: width={patchWidth}, depth={patchDepth}");
                return;
            }

            // Inner rectangle (actual flattened area)
            Vector3 innerLocalMin = worldBounds.min - terrainPos;
            Vector3 innerLocalMax = worldBounds.max - terrainPos;
            innerLocalMin.x = Mathf.Clamp(innerLocalMin.x, 0, terrainSize.x);
            innerLocalMin.z = Mathf.Clamp(innerLocalMin.z, 0, terrainSize.z);
            innerLocalMax.x = Mathf.Clamp(innerLocalMax.x, 0, terrainSize.x);
            innerLocalMax.z = Mathf.Clamp(innerLocalMax.z, 0, terrainSize.z);

            int innerXStart = Mathf.FloorToInt(innerLocalMin.x / terrainSize.x * (heightmapRes - 1));
            int innerZStart = Mathf.FloorToInt(innerLocalMin.z / terrainSize.z * (heightmapRes - 1));
            int innerXEnd   = Mathf.CeilToInt(innerLocalMax.x / terrainSize.x * (heightmapRes - 1));
            int innerZEnd   = Mathf.CeilToInt(innerLocalMax.z / terrainSize.z * (heightmapRes - 1));

            innerXStart = Mathf.Clamp(innerXStart, xStart, xEnd);
            innerZStart = Mathf.Clamp(innerZStart, zStart, zEnd);
            innerXEnd   = Mathf.Clamp(innerXEnd,   xStart, xEnd);
            innerZEnd   = Mathf.Clamp(innerZEnd,   zStart, zEnd);

            // Fetch heights for the expanded patch
            float[,] heights = terrainData.GetHeights(xStart, zStart, patchWidth, patchDepth);
            if (heights == null)
            {
                Debug.LogError("GetHeights returned null.");
                return;
            }

            if (heights.GetLength(0) != patchDepth || heights.GetLength(1) != patchWidth)
            {
                Debug.LogError($"Height array size mismatch: expected depth={patchDepth}, width={patchWidth}; got depth={heights.GetLength(0)}, width={heights.GetLength(1)}");
                return;
            }

            float normTargetHeight = targetHeight / terrainSize.y;
            float blendPixels = blendWidth / terrainSize.x * (heightmapRes - 1);

            float noiseOffsetX = UnityEngine.Random.Range(0f, 1000f);
            float noiseOffsetZ = UnityEngine.Random.Range(0f, 1000f);

            for (int x = 0; x < patchWidth; x++)
            {
                for (int z = 0; z < patchDepth; z++)
                {
                    int globalX = xStart + x;
                    int globalZ = zStart + z;

                    float distToInner = SignedDistanceToRectangle(
                        globalX, globalZ,
                        innerXStart, innerXEnd, innerZStart, innerZEnd);

                    float blendFactor;
                    if (distToInner >= 0)
                        blendFactor = 1f;
                    else
                    {
                        float absDist = -distToInner;
                        blendFactor = Mathf.Clamp01(1f - (absDist / blendPixels));
                    }

                    blendFactor = Mathf.SmoothStep(0f, 1f, blendFactor);

                    // Apply noise ONLY in blend zone and cap result at target height
                    float noise = 0f;
                    if (blendFactor < 1f)
                    {
                        float noiseX = (globalX * noiseScale + noiseOffsetX);
                        float noiseZ = (globalZ * noiseScale + noiseOffsetZ);
                        noise = (Mathf.PerlinNoise(noiseX, noiseZ) * 2f - 1f) * noiseStrength * (1f - blendFactor);
                    }

                    float originalHeight = heights[z, x];
                    float blendedHeight = Mathf.Lerp(originalHeight, normTargetHeight, blendFactor) + noise;

                    // Ensure we never exceed the target floor height
                    blendedHeight = Mathf.Min(blendedHeight, normTargetHeight);

                    heights[z, x] = Mathf.Clamp01(blendedHeight);
                }
            }

            terrainData.SetHeights(xStart, zStart, heights);
        }

        private float SignedDistanceToRectangle(int px, int pz, int minX, int maxX, int minZ, int maxZ) {
            int dx = Mathf.Max(minX - px, 0, px - maxX);
            int dz = Mathf.Max(minZ - pz, 0, pz - maxZ);
            float outsideDist = Mathf.Sqrt(dx * dx + dz * dz);

            if (px >= minX && px <= maxX && pz >= minZ && pz <= maxZ) {
                int insideDistX = Mathf.Min(px - minX, maxX - px);
                int insideDistZ = Mathf.Min(pz - minZ, maxZ - pz);
                return Mathf.Min(insideDistX, insideDistZ);
            }
            return -outsideDist;
        }

        public void Cleanup() {
            if (terrainGameObject == null) return;
            Object.Destroy(terrainGameObject);
        }

        public Task GenerateTerrainAsync() => throw new NotImplementedException("Use overload with settings");
        public void Regenerate() => throw new NotImplementedException("Use TerrainManager instead");
    }
}