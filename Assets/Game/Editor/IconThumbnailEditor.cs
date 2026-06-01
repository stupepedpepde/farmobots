using System.Collections.Generic;
using System.IO;
using Game.Scripts.Core.Building;
using Game.Scripts.Inventory.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor {
    public class IconThumbnailEditor : EditorWindow {
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        // Buildings
        private List<BuildablePrefab> m_buildings = new List<BuildablePrefab>();
        private BuildablePrefab m_selectedBuilding;
        private SerializedObject m_serializedAsset;

        // Items
        private List<ItemDetails> m_items = new List<ItemDetails>();
        private ItemDetails m_selectedItem;

        // UI
        private ListView m_list;
        private VisualElement m_inspectorContainer;
        private Button m_saveAssetButton;
        private Button m_captureIconButton;
        private Vector3Field m_cameraPositionField;
        private Vector3Field m_cameraRotationField;
        private Slider m_objectRotationField;
        private FloatField m_objectRotationFloatField;

        // Mode selector
        private ToolbarToggle m_buildingsToggle;
        private ToolbarToggle m_itemsToggle;
        private enum AssetMode { Buildings, Items }
        private AssetMode m_currentMode = AssetMode.Buildings;

        // Preview scene
        private Scene m_previewScene;
        private GameObject m_cameraObject;
        private Camera m_sceneCamera;
        private GameObject m_instance;
        private List<GameObject> m_connectionPointHelpers = new List<GameObject>();
        private List<GameObject> m_furniturePointHelpers = new List<GameObject>();
        private Texture2D m_previewTexture;
        private int m_size = 512;

        private string m_iconOutputFolderBuildings = "Assets/Game/Icons/Buildings/";
        private string m_iconOutputFolderItems = "Assets/Game/Icons/Items/";

        [MenuItem("Tools/Icon Editor")]
        public static void ShowExample() {
            IconThumbnailEditor wnd = GetWindow<IconThumbnailEditor>();
            wnd.titleContent = new GUIContent("Icon Thumbnail Editor");
        }

        private void CreateGUI() {
            rootVisualElement.Add(m_VisualTreeAsset.Instantiate());

            // Toolbar for mode selection
            var toolbar = new Toolbar();
            m_buildingsToggle = new ToolbarToggle() { text = "Buildings", value = true };
            m_itemsToggle = new ToolbarToggle() { text = "Items", value = false };
            toolbar.Add(m_buildingsToggle);
            toolbar.Add(m_itemsToggle);
            rootVisualElement.Insert(0, toolbar);

            m_buildingsToggle.RegisterValueChangedCallback(evt => {
                if (evt.newValue) SwitchMode(AssetMode.Buildings);
            });
            m_itemsToggle.RegisterValueChangedCallback(evt => {
                if (evt.newValue) SwitchMode(AssetMode.Items);
            });

            m_list = rootVisualElement.Q<ListView>("list");
            if (m_list == null) return;

            m_inspectorContainer = rootVisualElement.Q<ScrollView>("inspector");
            if (m_inspectorContainer == null) {
                m_inspectorContainer = new ScrollView();
                rootVisualElement.Q<VisualElement>("content").Add(m_inspectorContainer);
            }

            m_cameraPositionField = rootVisualElement.Q<Vector3Field>("cameraPosition");
            m_cameraRotationField = rootVisualElement.Q<Vector3Field>("cameraRotation");
            m_objectRotationField = rootVisualElement.Q<Slider>("objectRotation");
            m_objectRotationFloatField = rootVisualElement.Q<FloatField>("objectRotationFloat");

            m_saveAssetButton = rootVisualElement.Q<Button>("save-asset-btn");
            if (m_saveAssetButton != null)
                m_saveAssetButton.clicked += SaveAssetChanges;

            m_captureIconButton = rootVisualElement.Q<Button>("capture-icon-btn");
            if (m_captureIconButton != null)
                m_captureIconButton.clicked += CaptureAndAssignIcon;

            m_cameraPositionField?.RegisterValueChangedCallback(OnCameraPositionChange);
            m_cameraRotationField?.RegisterValueChangedCallback(OnCameraRotationChange);

            if (m_objectRotationField != null && m_objectRotationFloatField != null) {
                m_objectRotationField.RegisterValueChangedCallback(evt => {
                    m_objectRotationFloatField.SetValueWithoutNotify(evt.newValue);
                    OnRotationChange(evt);
                });
                m_objectRotationFloatField.RegisterValueChangedCallback(evt => {
                    m_objectRotationField.SetValueWithoutNotify(evt.newValue);
                    OnRotationChange(evt);
                });
            }
            else {
                m_objectRotationField?.RegisterValueChangedCallback(OnRotationChange);
                m_objectRotationFloatField?.RegisterValueChangedCallback(OnRotationChange);
            }

            LoadBuildings();
            LoadItems();
            SwitchMode(AssetMode.Buildings);
        }

        private void LoadBuildings() {
            string[] assetGuids = AssetDatabase.FindAssets("t:BuildablePrefab");
            m_buildings.Clear();
            foreach (string guid in assetGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BuildablePrefab>(path);
                if (asset != null) m_buildings.Add(asset);
            }
        }

        private void LoadItems() {
            string[] assetGuids = AssetDatabase.FindAssets("t:ItemDetails");
            m_items.Clear();
            foreach (string guid in assetGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ItemDetails>(path);
                if (asset != null) m_items.Add(asset);
            }
        }

        private void SwitchMode(AssetMode mode) {
            m_currentMode = mode;
            m_buildingsToggle.value = (mode == AssetMode.Buildings);
            m_itemsToggle.value = (mode == AssetMode.Items);

            if (mode == AssetMode.Buildings) {
                m_list.itemsSource = m_buildings;
                m_list.makeItem = () => new Label();
                m_list.bindItem = (element, index) => {
                    var item = m_buildings[index];
                    (element as Label).text = item != null ? $"{item.name} - (BuildablePrefab)" : "<null>";
                };
                m_list.selectionChanged -= OnSelectItem;
                m_list.selectionChanged += OnSelectItem;
                m_list.RefreshItems();
            } else {
                m_list.itemsSource = m_items;
                m_list.makeItem = () => new Label();
                m_list.bindItem = (element, index) => {
                    var item = m_items[index];
                    (element as Label).text = item != null ? $"{item.name} - (ItemDetails)" : "<null>";
                };
                m_list.selectionChanged -= OnSelectItem;
                m_list.selectionChanged += OnSelectItem;
                m_list.RefreshItems();
            }
        }

        private void OnSelectItem(object item) {
            if (m_list.selectedIndex < 0) return;

            if (m_currentMode == AssetMode.Buildings) {
                m_selectedBuilding = m_buildings[m_list.selectedIndex];
                if (m_selectedBuilding == null) return;
                m_serializedAsset = new SerializedObject(m_selectedBuilding);
                BuildInspector();
                LoadPreviewForBuilding(m_selectedBuilding);
            } else {
                m_selectedItem = m_items[m_list.selectedIndex];
                if (m_selectedItem == null) return;
                m_serializedAsset = new SerializedObject(m_selectedItem);
                BuildInspector();
                LoadPreviewForItem(m_selectedItem);
            }
        }

        private void LoadPreviewForBuilding(BuildablePrefab building) {
            if (!m_previewScene.IsValid())
                m_previewScene = EditorSceneManager.NewPreviewScene();

            EnsureCamera();

            GameObject prefabToShow = building.prefabVariants != null && building.prefabVariants.Length > 0 ? building.prefabVariants[0] : null;
            if (prefabToShow == null) {
                Debug.LogWarning($"No prefab variant for {building.name}");
                return;
            }

            ClearInstance();
            m_instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToShow, m_previewScene);
            m_instance.transform.position = Vector3.zero;
            m_instance.transform.rotation = Quaternion.Euler(0, m_objectRotationField.value, 0);

            UpdateSnapPointVisuals();
            UpdateCamera();
        }

        private void LoadPreviewForItem(ItemDetails item) {
            if (item.PreviewPrefab == null) {
                Debug.LogWarning($"Item {item.name} has no PreviewPrefab assigned. Cannot render icon.");
                return;
            }

            if (!m_previewScene.IsValid())
                m_previewScene = EditorSceneManager.NewPreviewScene();

            EnsureCamera();

            ClearInstance();
            m_instance = (GameObject)PrefabUtility.InstantiatePrefab(item.PreviewPrefab, m_previewScene);
            m_instance.transform.position = Vector3.zero;
            m_instance.transform.rotation = Quaternion.Euler(0, m_objectRotationField.value, 0);

            // Clear any snap point helpers (not used for items)
            foreach (var helper in m_connectionPointHelpers)
                if (helper != null) DestroyImmediate(helper);
            m_connectionPointHelpers.Clear();
            foreach (var helper in m_furniturePointHelpers)
                if (helper != null) DestroyImmediate(helper);
            m_furniturePointHelpers.Clear();

            UpdateCamera();
        }

        private void EnsureCamera() {
            if (m_cameraObject == null) {
                m_cameraObject = new GameObject("PreviewCamera");
                m_cameraObject.transform.position = new Vector3(0, 2, -5);
                m_cameraObject.transform.eulerAngles = Vector3.zero;
                m_cameraPositionField.value = m_cameraObject.transform.position;
                m_cameraRotationField.value = m_cameraObject.transform.eulerAngles;

                m_sceneCamera = m_cameraObject.AddComponent<Camera>();
                m_sceneCamera.aspect = 1f;
                m_sceneCamera.backgroundColor = Color.black;
                m_sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                m_sceneCamera.targetTexture = new RenderTexture(m_size, m_size, 32, RenderTextureFormat.ARGBFloat);

                SceneManager.MoveGameObjectToScene(m_cameraObject, m_previewScene);
                m_sceneCamera.scene = m_previewScene;
            }
        }

        private void ClearInstance() {
            if (m_instance != null)
                DestroyImmediate(m_instance);
        }

        private void BuildInspector() {
            m_inspectorContainer.Clear();
            if (m_serializedAsset == null) return;

            SerializedProperty property = m_serializedAsset.GetIterator();
            property.NextVisible(true);
            while (property.NextVisible(false)) {
                PropertyField field = new PropertyField(property);
                field.Bind(m_serializedAsset);
                m_inspectorContainer.Add(field);
            }

            if (m_currentMode == AssetMode.Buildings) {
                Button refreshPointsBtn = new Button(() => UpdateSnapPointVisuals());
                refreshPointsBtn.text = "Refresh Snapping Points (Connection & Furniture)";
                m_inspectorContainer.Add(refreshPointsBtn);
            }
        }

        private void UpdateSnapPointVisuals() {
            // Clear old helpers
            foreach (var helper in m_connectionPointHelpers)
                if (helper != null) DestroyImmediate(helper);
            m_connectionPointHelpers.Clear();

            foreach (var helper in m_furniturePointHelpers)
                if (helper != null) DestroyImmediate(helper);
            m_furniturePointHelpers.Clear();

            if (m_instance == null || m_selectedBuilding == null) return;

            // Draw connection points (orange)
            for (int i = 0; i < m_selectedBuilding.connectionPointsLocal.Length; i++) {
                Vector3 localPos = m_selectedBuilding.connectionPointsLocal[i];
                GameObject sphere = CreatePointHelper(localPos, new Color(1f, 0.5f, 0f, 0.7f), "ConnectionPoint");
                m_connectionPointHelpers.Add(sphere);
            }

            // Draw furniture snap points (green)
            for (int i = 0; i < m_selectedBuilding.furnitureSnapPointsLocal.Length; i++) {
                Vector3 localPos = m_selectedBuilding.furnitureSnapPointsLocal[i];
                GameObject sphere = CreatePointHelper(localPos, new Color(0f, 1f, 0f, 0.7f), "FurniturePoint");
                m_furniturePointHelpers.Add(sphere);
            }

            UpdateCamera();
        }

        private GameObject CreatePointHelper(Vector3 localPos, Color color, string namePrefix) {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"{namePrefix}_{m_connectionPointHelpers.Count + m_furniturePointHelpers.Count}";
            sphere.transform.SetParent(m_instance.transform);
            sphere.transform.localPosition = localPos;
            sphere.transform.localScale = Vector3.one * 0.1f;
            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            renderer.sharedMaterial.color = color;
            DestroyImmediate(sphere.GetComponent<Collider>());
            return sphere;
        }

        private void SaveAssetChanges() {
            if (m_serializedAsset != null && m_serializedAsset.ApplyModifiedProperties()) {
                if (m_currentMode == AssetMode.Buildings && m_selectedBuilding != null)
                    EditorUtility.SetDirty(m_selectedBuilding);
                else if (m_currentMode == AssetMode.Items && m_selectedItem != null)
                    EditorUtility.SetDirty(m_selectedItem);
                AssetDatabase.SaveAssets();
                Debug.Log($"Saved changes to {(m_currentMode == AssetMode.Buildings ? m_selectedBuilding?.name : m_selectedItem?.name)}");
            } else
                Debug.Log("No changes to save.");
        }

        private void CaptureAndAssignIcon() {
            if (m_currentMode == AssetMode.Buildings && m_selectedBuilding == null) {
                Debug.LogError("No building selected.");
                return;
            }
            if (m_currentMode == AssetMode.Items && m_selectedItem == null) {
                Debug.LogError("No item selected.");
                return;
            }
            if (m_sceneCamera == null) {
                Debug.LogError("Preview camera not initialized.");
                return;
            }

            // Hide both connection and furniture helpers before capture
            bool[] connActive = null, furnActive = null;
            if (m_connectionPointHelpers.Count > 0) {
                connActive = new bool[m_connectionPointHelpers.Count];
                for (int i = 0; i < m_connectionPointHelpers.Count; i++) {
                    if (m_connectionPointHelpers[i] != null) {
                        connActive[i] = m_connectionPointHelpers[i].activeSelf;
                        m_connectionPointHelpers[i].SetActive(false);
                    }
                }
            }
            if (m_furniturePointHelpers.Count > 0) {
                furnActive = new bool[m_furniturePointHelpers.Count];
                for (int i = 0; i < m_furniturePointHelpers.Count; i++) {
                    if (m_furniturePointHelpers[i] != null) {
                        furnActive[i] = m_furniturePointHelpers[i].activeSelf;
                        m_furniturePointHelpers[i].SetActive(false);
                    }
                }
            }

            Color originalBackground = m_sceneCamera.backgroundColor;
            m_sceneCamera.backgroundColor = new Color(0, 0, 0, 0);
            m_sceneCamera.Render();

            Texture2D capturedTexture = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, false, true);
            RenderTexture previousActive = RenderTexture.active;
            try {
                RenderTexture.active = m_sceneCamera.targetTexture;
                capturedTexture.ReadPixels(new Rect(0, 0, m_size, m_size), 0, 0);
                capturedTexture.Apply();
            } finally {
                RenderTexture.active = previousActive;
            }

            m_sceneCamera.backgroundColor = originalBackground;

            // Restore helper visibility
            if (connActive != null) {
                for (int i = 0; i < m_connectionPointHelpers.Count; i++)
                    if (m_connectionPointHelpers[i] != null) m_connectionPointHelpers[i].SetActive(connActive[i]);
            }
            if (furnActive != null) {
                for (int i = 0; i < m_furniturePointHelpers.Count; i++)
                    if (m_furniturePointHelpers[i] != null) m_furniturePointHelpers[i].SetActive(furnActive[i]);
            }

            string outputFolder = (m_currentMode == AssetMode.Buildings) ? m_iconOutputFolderBuildings : m_iconOutputFolderItems;
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string assetName = (m_currentMode == AssetMode.Buildings) ? m_selectedBuilding.id.Replace(" ", "_") : m_selectedItem.name.Replace(" ", "_");
            string pngPath = Path.Combine(outputFolder, $"{assetName}_icon.png").Replace("\\", "/");

            if (File.Exists(pngPath)) {
                AssetDatabase.DeleteAsset(pngPath);
                AssetDatabase.Refresh();
            }

            byte[] pngData = capturedTexture.EncodeToPNG();
            if (pngData == null) {
                Debug.LogError("Failed to encode texture to PNG.");
                DestroyImmediate(capturedTexture);
                return;
            }

            File.WriteAllBytes(pngPath, pngData);
            AssetDatabase.Refresh();

            TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            } else {
                Debug.LogWarning($"Could not get TextureImporter for {pngPath}");
            }

            Sprite generatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (generatedSprite != null) {
                if (m_currentMode == AssetMode.Buildings) {
                    m_selectedBuilding.icon = generatedSprite;
                    EditorUtility.SetDirty(m_selectedBuilding);
                } else {
                    m_selectedItem.SetIcon(generatedSprite);
                    EditorUtility.SetDirty(m_selectedItem);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"Icon captured and assigned to {(m_currentMode == AssetMode.Buildings ? m_selectedBuilding.name : m_selectedItem.name)} at {pngPath}");
            } else {
                Debug.LogError("Failed to load generated sprite.");
            }

            DestroyImmediate(capturedTexture);
            BuildInspector();
        }

        private void UpdateCamera() {
            if (m_sceneCamera == null) return;

            m_sceneCamera.Render();

            if (m_previewTexture == null)
                m_previewTexture = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, false, true);

            RenderTexture previousActive = RenderTexture.active;
            try {
                RenderTexture.active = m_sceneCamera.targetTexture;
                m_previewTexture.ReadPixels(new Rect(0, 0, m_size, m_size), 0, 0);
                m_previewTexture.Apply();

                var outputElement = rootVisualElement.Q<VisualElement>("output");
                if (outputElement != null)
                    outputElement.style.backgroundImage = new StyleBackground(m_previewTexture);
            } finally {
                RenderTexture.active = previousActive;
            }
        }

        private void OnCameraPositionChange(ChangeEvent<Vector3> evt) {
            if (m_cameraObject != null) m_cameraObject.transform.position = evt.newValue;
            UpdateCamera();
        }

        private void OnCameraRotationChange(ChangeEvent<Vector3> evt) {
            if (m_cameraObject != null) m_cameraObject.transform.eulerAngles = evt.newValue;
            UpdateCamera();
        }

        private void OnRotationChange(ChangeEvent<float> evt) {
            if (m_instance != null) m_instance.transform.eulerAngles = new Vector3(0, evt.newValue, 0);
            UpdateCamera();
        }

        private void OnDisable() {
            if (m_previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(m_previewScene);

            if (m_cameraObject != null)
                DestroyImmediate(m_cameraObject);

            if (m_previewTexture != null)
                DestroyImmediate(m_previewTexture);

            foreach (var helper in m_connectionPointHelpers)
                if (helper != null) DestroyImmediate(helper);
            foreach (var helper in m_furniturePointHelpers)
                if (helper != null) DestroyImmediate(helper);
        }
    }
}