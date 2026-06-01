using System;
using System.Collections.Generic;
using System.Linq;
using Game.Scripts.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.Building {
    public class BuildSelectionPopup {
        private const string POPUP_ID = "build-selection";
        private UIPopup popup;
        private List<VisualElement> slots = new();
        private int selectedIndex = -1;
        private float lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        private Image previewImage;
        private Label previewName;
        private VisualElement requirementsContainer;
        private List<Button> categoryTabs = new();
        private BuildCategory currentCategory = BuildCategory.ALL;
        private string currentFilter = "";

        private readonly Color panelBackground = new(0.12f, 0.12f, 0.12f, 0.98f);
        private readonly Color slotNormal = new(0.2f, 0.2f, 0.2f, 1f);
        private readonly Color slotHover = new(0.35f, 0.35f, 0.35f, 1f);
        private readonly Color slotSelected = new(0.4f, 0.7f, 0.4f, 1f);
        private readonly Color accentColor = new(0.6f, 0.8f, 0.6f, 1f);
        private readonly Color categoryTabNormal = new(0.15f, 0.15f, 0.15f, 1f);
        private readonly Color categoryTabSelected = new(0.3f, 0.5f, 0.3f, 1f);
        private readonly int slotHeight = 60;
        private readonly int previewSize = 150;

        public bool IsOpen => popup != null && popup.root.parent != null;

        public BuildSelectionPopup() {
            BuildPopup();
        }

        private void BuildPopup() {
            popup = UIManager.instance.CreatePopup(POPUP_ID, "BUILD MENU", draggable: true, closable: true)
                .SetSize(1000, 700)
                .OnClose(OnPopupClosed);

            popup.SetContent(BuildContent());
        }

        private void OnPopupClosed() {
            selectedIndex = -1;
            slots.Clear();
            categoryTabs.Clear();
        }

        public void Toggle() {
            if (popup == null) BuildPopup();
            UIManager.instance.TogglePopup(POPUP_ID, popup);
            RefreshFilter();
        }

        private VisualElement BuildContent() {
            var root = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithBackgroundColor(panelBackground)
                .WithPaddings(15)
                .WithGrow(1)
                .Build();

            var leftColumn = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithGrow(2)
                .WithMargins(0, 15, 0, 0)
                .Build();

            leftColumn.Add(CreateCategoryTabs());
            leftColumn.Add(CreateSearchBar());
            leftColumn.Add(CreatePrefabList());

            var rightColumn = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithGrow(1)
                .WithBackgroundColor(new Color(0.08f, 0.08f, 0.08f, 1f))
                .WithBorderRadius(10, 10, 10, 10)
                .WithPaddings(15)
                .WithAlignItems(Align.Center)
                .Build();

            previewImage = UIBuilder.CreateImage()
                .WithSize(previewSize, previewSize)
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithBorderRadius(8, 8, 8, 8)
                .WithMargins(0, 0, 10, 0)
                .Build<Image>();

            previewName = UIBuilder.CreateLabel()
                .WithText("Select a prefab")
                .WithFontSize(18)
                .WithColor(accentColor)
                .WithFontStyle(FontStyle.Bold)
                .WithMargins(0, 0, 15, 0)
                .Build<Label>();

            requirementsContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.FlexStart)
                .WithMargins(0, 10)
                .Build();

            var selectButton = UIBuilder.CreateButton()
                .WithText("START BUILDING")
                .WithBackgroundColor(accentColor)
                .WithColor(Color.black)
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .WithSize(180, 40)
                .WithBorderRadius(20, 20, 20, 20)
                .OnClick(() => ConfirmSelection(selectedIndex))
                .Build<Button>();

            rightColumn.Add(previewImage);
            rightColumn.Add(previewName);
            rightColumn.Add(requirementsContainer);
            rightColumn.Add(selectButton);

            root.Add(leftColumn);
            root.Add(rightColumn);
            return root;
        }

        private VisualElement CreateCategoryTabs() {
            var container = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithMargins(0, 0, 10, 0)
                .WithSize(Length.Percent(100), 40)
                .Build();

            var prefabs = BuildingSystem.instance.BuildablePrefabs;
            var categories = prefabs.Select(p => p.category).Distinct().OrderBy(c => c.ToString()).ToList();

            // Add ALL tab
            CreateCategoryTab(container, BuildCategory.ALL, true);
            foreach (var cat in categories)
                CreateCategoryTab(container, cat, false);

            return container;
        }

        private void CreateCategoryTab(VisualElement parent, BuildCategory category, bool isDefault) {
            var tab = UIBuilder.CreateButton()
                .WithText(category.ToString())
                .WithBackgroundColor(isDefault ? categoryTabSelected : categoryTabNormal)
                .WithColor(Color.white)
                .WithFontSize(14)
                .WithMargins(0, 5, 0, 0)
                .WithPaddings(5, 10)
                .WithBorderRadius(5, 5, 5, 5)
                .OnClick(() => SetCategory(category))
                .Build<Button>();

            // Store the category in userData for later reference
            tab.userData = category;
            parent.Add(tab);
            categoryTabs.Add(tab);
        }

        private void SetCategory(BuildCategory category) {
            if (currentCategory == category) return;
            currentCategory = category;

            // Update tab colors
            foreach (var tab in categoryTabs) {
                var tabCategory = (BuildCategory)tab.userData;
                tab.style.backgroundColor = tabCategory == category ? categoryTabSelected : categoryTabNormal;
            }

            RefreshFilter();
        }

        private VisualElement CreateSearchBar() {
            var searchField = UIBuilder.CreateTextField()
                .WithName("search-field")
                .WithSize(Length.Percent(100), 30)
                .WithMargins(0, 0, 10, 0)
                .WithBackgroundColor(new Color(0.15f, 0.15f, 0.15f))
                .WithColor(Color.white)
                .WithBorders(1, 1, 1, 1, accentColor, accentColor, accentColor, accentColor, 5)
                .Build<TextField>();

            searchField.RegisterValueChangedCallback(evt => {
                currentFilter = evt.newValue;
                RefreshFilter();
            });

            return searchField;
        }

        private VisualElement CreatePrefabList() {
            var scrollView = UIBuilder.CreateScrollView()
                .WithName("prefab-scroll")
                .Build<ScrollView>();

            var listContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithWidth(Length.Percent(100))
                .Build();

            var prefabs = BuildingSystem.instance.BuildablePrefabs;
            slots.Clear();

            for (int i = 0; i < prefabs.Count; i++) {
                var slot = CreateSlot(prefabs[i], i);
                listContainer.Add(slot);
                slots.Add(slot);
            }

            scrollView.contentContainer.Add(listContainer);
            return scrollView;
        }

        private VisualElement CreateSlot(BuildablePrefab prefab, int index) {
            var slot = UIBuilder.CreateContainer()
                .WithName($"slot-{prefab.id}")
                .WithHeight(slotHeight)
                .WithBackgroundColor(slotNormal)
                .WithMargins(0, 0, 5, 0)
                .WithBorderRadius(5, 5, 5, 5)
                .WithBorders(1, 1, 1, 1, new Color(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.3f, 0.3f), null)
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithPaddings(5)
                .Build();

            var icon = UIBuilder.CreateImage()
                .WithSize(50, 50)
                .WithSprite(prefab.icon != null ? prefab.icon : Resources.Load<Sprite>("default_icon"))
                .WithMargins(0, 0, 10, 0)
                .Build<Image>();

            var nameLabel = UIBuilder.CreateLabel()
                .WithText(prefab.displayName)
                .WithFontSize(14)
                .WithColor(Color.white)
                .WithGrow(1)
                .Build<Label>();

            string costText = "";
            if (prefab.requirements.Count > 0) {
                var first = prefab.requirements[0];
                costText = $"{first.quantity}x {first.requiredItem.ItemName}";
                if (prefab.requirements.Count > 1) costText += " +...";
            }

            var costLabel = UIBuilder.CreateLabel()
                .WithText(costText)
                .WithFontSize(10)
                .WithColor(accentColor)
                .WithMargins(0, 0, 10, 0)
                .Build<Label>();

            slot.Add(icon);
            slot.Add(nameLabel);
            slot.Add(costLabel);

            // Click selection
            slot.RegisterCallback<ClickEvent>(evt => {
                if (selectedIndex != index) {
                    ClearSelection();
                    selectedIndex = index;
                    slot.style.backgroundColor = slotSelected;
                    UpdatePreview(prefab);
                }
                evt.StopPropagation();
            });

            // Double-click handling
            slot.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                float now = Time.realtimeSinceStartup;
                if (now - lastClickTime < DoubleClickThreshold)
                    ConfirmSelection(selectedIndex);
                lastClickTime = now;
            });

            // Hover – only if not selected
            slot.RegisterCallback<PointerEnterEvent>(evt => {
                if (selectedIndex != index)
                    slot.style.backgroundColor = slotHover;
            });

            slot.RegisterCallback<PointerLeaveEvent>(evt => {
                if (selectedIndex != index)
                    slot.style.backgroundColor = slotNormal;
            });

            UITooltip.Attach(slot, prefab.displayName);
            return slot;
        }

        private void ClearSelection() {
            if (selectedIndex >= 0 && selectedIndex < slots.Count) {
                var previouslySelected = slots[selectedIndex];
                previouslySelected.style.backgroundColor = slotNormal;
            }
            selectedIndex = -1;
        }

        private void UpdatePreview(BuildablePrefab prefab) {
            if (previewImage != null)
                previewImage.sprite = prefab.icon;
            if (previewName != null)
                previewName.text = prefab.displayName;

            requirementsContainer.Clear();

            if (prefab.requirements.Count == 0) {
                requirementsContainer.Add(new Label("No requirements"));
                return;
            }

            var title = new Label("REQUIREMENTS:");
            title.style.color = accentColor;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            requirementsContainer.Add(title);

            foreach (var req in prefab.requirements) {
                var row = UIBuilder.CreateContainer()
                    .WithFlexDirection(FlexDirection.Row)
                    .WithAlignItems(Align.Center)
                    .WithMargins(0, 0, 5, 0)
                    .Build();

                var iconImg = UIBuilder.CreateImage()
                    .WithSize(32, 32)
                    .WithSprite(req.requiredItem.Icon)
                    .WithMargins(0, 0, 8, 0)
                    .Build<Image>();

                var label = new Label($"{req.quantity}x {req.requiredItem.ItemName}");
                label.style.color = Color.white;
                label.style.fontSize = 12;

                row.Add(iconImg);
                row.Add(label);
                requirementsContainer.Add(row);
            }
        }

        private void ConfirmSelection(int index) {
            var prefabs = BuildingSystem.instance.BuildablePrefabs;
            if (index < 0 || index >= prefabs.Count) return;

            popup?.Close();
            BuildingSystem.instance.SetCurrentPrefabIndex(index);
            BuildingSystem.instance.SetBuildMode(true);
        }

        private void RefreshFilter() {
            var prefabs = BuildingSystem.instance.BuildablePrefabs;
            for (int i = 0; i < slots.Count; i++) {
                bool matchesCategory = currentCategory == BuildCategory.ALL || prefabs[i].category == currentCategory;
                bool matchesFilter = string.IsNullOrEmpty(currentFilter) ||
                    prefabs[i].displayName.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0;

                slots[i].style.display = (matchesCategory && matchesFilter) ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}