using System.Collections.Generic;
using System.IO;
using Game.Scripts.Core.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.SceneManagement;

namespace Game.Tools {
    public class IconThumbnailEditor : EditorWindow {
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        private List<BuildablePrefab> m_buildings = new List<BuildablePrefab>();
        private BuildablePrefab m_selectedAsset;
        private SerializedObject m_serializedAsset;

        private ListView m_list;
        private VisualElement m_inspectorContainer;
        private Button m_saveAssetButton;
        private Button m_captureIconButton;
        private Vector3Field m_cameraPositionField;
        private Vector3Field m_cameraRotationField;
        private Slider m_objectRotationField;
        private FloatField m_objectRotationFloatField;

        private Scene m_previewScene;
        private GameObject m_cameraObject;
        private Camera m_sceneCamera;
        private GameObject m_instance;
        private List<GameObject> m_connectionPointHelpers = new List<GameObject>();
        private Texture2D m_previewTexture;
        private int m_size = 512;

        private string m_iconOutputFolder = "Assets/Game/Icons/Buildings/";

        [MenuItem("Tools/Icon Editor")]
        public static void ShowExample() {
            IconThumbnailEditor wnd = GetWindow<IconThumbnailEditor>();
            wnd.titleContent = new GUIContent("Icon Thumbnail Editor");
        }

        private void CreateGUI() {
            rootVisualElement.Add(m_VisualTreeAsset.Instantiate());

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

            string[] assetGuids = AssetDatabase.FindAssets("t:BuildablePrefab");
            m_buildings.Clear();
            foreach (string guid in assetGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BuildablePrefab>(path);
                if (asset != null) m_buildings.Add(asset);
            }

            m_list.itemsSource = m_buildings;
            m_list.makeItem = () => new Label();
            m_list.bindItem = (element, index) => {
                var item = m_buildings[index];
                (element as Label).text = item != null ? $"{item.name} - (BuildablePrefab)" : "<null>";
            };

            m_list.selectionChanged += OnSelectItem;
            m_list.RefreshItems();
        }

        private void OnSelectItem(object item) {
            if (m_list.selectedIndex < 0 || m_list.selectedIndex >= m_buildings.Count)
                return;

            m_selectedAsset = m_buildings[m_list.selectedIndex];
            if (m_selectedAsset == null) return;

            m_serializedAsset = new SerializedObject(m_selectedAsset);
            BuildInspector();

            if (!m_previewScene.IsValid())
                m_previewScene = EditorSceneManager.NewPreviewScene();

            if (m_cameraObject == null) {
                m_cameraObject = new GameObject("PreviewCamera");
                m_cameraObject.transform.position = new Vector3(0, 4, -10);
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

            GameObject prefabToShow = m_selectedAsset.prefabVariants != null && m_selectedAsset.prefabVariants.Length > 0 ? m_selectedAsset.prefabVariants[0] : null;
            if (prefabToShow == null) {
                Debug.LogWarning($"No prefab variant for {m_selectedAsset.name}");
                return;
            }

            if (m_instance != null)
                DestroyImmediate(m_instance);

            m_instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToShow, m_previewScene);
            m_instance.transform.position = Vector3.zero;
            m_instance.transform.rotation = Quaternion.Euler(0, m_objectRotationField.value, 0);

            UpdateConnectionPointVisuals();
            UpdateCamera();
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

            Button refreshPointsBtn = new Button(() => UpdateConnectionPointVisuals());
            refreshPointsBtn.text = "Refresh Snapping Points Visuals";
            m_inspectorContainer.Add(refreshPointsBtn);
        }

        private void UpdateConnectionPointVisuals() {
            foreach (var helper in m_connectionPointHelpers)
                if (helper != null) DestroyImmediate(helper);

            m_connectionPointHelpers.Clear();

            if (m_instance == null || m_selectedAsset == null) return;

            for (int i = 0; i < m_selectedAsset.connectionPointsLocal.Length; i++) {
                Vector3 localPos = m_selectedAsset.connectionPointsLocal[i];
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"ConnectionPoint_{i}";
                sphere.transform.SetParent(m_instance.transform);
                sphere.transform.localPosition = localPos;
                sphere.transform.localScale = Vector3.one * 0.1f;

                var renderer = sphere.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Standard"));
                renderer.sharedMaterial.color = new Color(1f, 0.5f, 0f, 0.7f);

                DestroyImmediate(sphere.GetComponent<Collider>());
                m_connectionPointHelpers.Add(sphere);
            }

            UpdateCamera();
        }

        private void SaveAssetChanges() {
            if (m_serializedAsset != null && m_serializedAsset.ApplyModifiedProperties()) {
                EditorUtility.SetDirty(m_selectedAsset);
                AssetDatabase.SaveAssets();
                Debug.Log($"Saved changes to {m_selectedAsset.name}");
            } else
                Debug.Log("No changes to save.");
        }

        private void CaptureAndAssignIcon() {
            if (m_selectedAsset == null) {
                Debug.LogError("No asset selected.");
                return;
            }

            if (m_sceneCamera == null) {
                Debug.LogError("Preview camera not initialized.");
                return;
            }

            bool[] wasActive = new bool[m_connectionPointHelpers.Count];
            for (int i = 0; i < m_connectionPointHelpers.Count; i++) {
                if (m_connectionPointHelpers[i] != null) {
                    wasActive[i] = m_connectionPointHelpers[i].activeSelf;
                    m_connectionPointHelpers[i].SetActive(false);
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

            for (int i = 0; i < m_connectionPointHelpers.Count; i++)
                if (m_connectionPointHelpers[i] != null)
                    m_connectionPointHelpers[i].SetActive(wasActive[i]);

            if (!Directory.Exists(m_iconOutputFolder)) Directory.CreateDirectory(m_iconOutputFolder);

            string safeName = m_selectedAsset.id.Replace(" ", "_");
            string pngPath = Path.Combine(m_iconOutputFolder, $"{safeName}_icon.png").Replace("\\", "/");

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
            } else
                Debug.LogWarning($"Could not get TextureImporter for {pngPath}");

            Sprite generatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (generatedSprite != null) {
                m_selectedAsset.icon = generatedSprite;
                EditorUtility.SetDirty(m_selectedAsset);
                AssetDatabase.SaveAssets();
                Debug.Log($"Icon captured and assigned to {m_selectedAsset.name} at {pngPath} using: pos{m_cameraPositionField.value} : rot{m_cameraRotationField.value} : obj_rot{m_objectRotationField.value}");
            } else
                Debug.LogError("Failed to load generated sprite.");

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
        }
    }
}