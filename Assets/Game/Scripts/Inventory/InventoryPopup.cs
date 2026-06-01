using System.Collections.Generic;
using Game.Scripts.Core;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Game.Scripts.Inventory
{
    public class InventoryPopup
    {
        private const string POPUP_ID = "inventory";
        private UIPopup popup;
        private VisualElement container;
        private VisualElement ghostIcon;

        private bool dragging;
        private Slot dragSource;
        private InventoryComponent dragSourceInventory;
        private int dragSourceIndex = -1;
        private int dragSlotIndex = -1;

        private Item draggedItem;
        private int draggedQuantity;

        private Slot lastClickedSlot;
        private float lastClickTime;
        private const float doubleClickThreshold = 0.3f;

        private Slot[][] inventorySlots = new Slot[2][];
        private InventoryComponent[] displayedInventories = new InventoryComponent[2];

        // Crafting data
        private List<CraftingRecipe> craftingRecipes = new List<CraftingRecipe>();
        private InventoryComponent craftingInventory;
        private VisualElement recipeListContainer;
        private Button craftButton;
        private Slot outputSlot;
        private Item outputItem;

        private readonly int slotSize = 64;
        private readonly int slotSpacing = 4;
        private readonly Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private readonly Color headerColor = new Color(0.2f, 0.15f, 0.1f, 1f);

        public bool IsOpen => popup != null && popup.root.parent != null;

        public InventoryPopup()
        {
            BuildPopup();
            SubscribeToEvents();
        }

        private void BuildPopup()
        {
            popup = UIManager.instance.CreatePopup(POPUP_ID, "", draggable: false, closable: true)
                .SetSize(Length.Percent(100), Length.Percent(100))
                .OnClose(OnPopupClosed);

            popup.root.style.backgroundColor = new StyleColor(Color.clear);
            popup.content.style.backgroundColor = new StyleColor(Color.clear);
            popup.content.style.borderTopWidth = 0;
            popup.content.style.borderRightWidth = 0;
            popup.content.style.borderBottomWidth = 0;
            popup.content.style.borderLeftWidth = 0;
            popup.content.style.paddingTop = 0;
            popup.content.style.paddingRight = 0;
            popup.content.style.paddingBottom = 0;
            popup.content.style.paddingLeft = 0;
            popup.header.style.display = DisplayStyle.None;

            container = UIBuilder.CreateContainer()
                .WithName("inventory-container")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithPosition(UIPosition.ABSOLUTE)
                .WithBackgroundColor(Color.clear)
                .Build();

            ghostIcon = UIBuilder.CreateContainer()
                .WithName("ghost-icon")
                .WithPosition(UIPosition.ABSOLUTE)
                .WithSize(slotSize, slotSize)
                .WithVisibility(false)
                .WithOpacity(0.8f)
                .WithBorders(2, Color.yellow, 8)
                .Build();
            ghostIcon.pickingMode = PickingMode.Ignore;

            popup.content.Add(ghostIcon);
            ghostIcon.BringToFront();
            popup.content.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            popup.content.RegisterCallback<PointerUpEvent>(OnPointerUp);
            popup.SetContent(container);

            UIManager.instance?.RegisterPopup(popup);
        }

        private void SubscribeToEvents()
        {
            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.OnInventoriesShouldDisplay += DisplayInventories;
                InventoryManager.instance.OnInventoryClosed += OnInventoryClosed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.OnInventoriesShouldDisplay -= DisplayInventories;
                InventoryManager.instance.OnInventoryClosed -= OnInventoryClosed;
            }
        }

        private void OnPopupClosed()
        {
            UITooltip.Hide();
            UnsubscribeFromEvents();
            CleanupInventoryBindings();

            // Clear UI references but KEEP the recipes and crafting inventory data
            // (so they are still there when the popup is reopened)
            recipeListContainer = null;
            craftButton = null;
            outputSlot = null;
            outputItem = null;
            // DO NOT clear craftingRecipes or craftingInventory

            UIManager.instance?.UnregisterPopup(popup);
        }

        private void OnInventoryClosed() => popup?.Close();

        public void Show()
        {
            if (popup == null) BuildPopup();
            var root = UIManager.instance?.GetRoot();
            if (root != null)
            {
                popup.Show(root);
                GameManager.instance?.SetGameState(GameState.INTERFACE);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Toggle()
        {
            if (popup == null) BuildPopup();
            if (IsOpen)
                popup.Close();
            else
            {
                var root = UIManager.instance?.GetRoot();
                if (root != null)
                {
                    popup.Show(root);
                    GameManager.instance?.SetGameState(GameState.INTERFACE);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        public void DisplayInventories(InventoryComponent primary, InventoryComponent secondary = null)
        {
            if (primary == null) return;
            displayedInventories[0] = primary;
            displayedInventories[1] = secondary;
            CreateInventoryUI();
        }

        public void SetCraftingData(List<CraftingRecipe> recipes, InventoryComponent inventory)
        {
            // Update the stored recipes and crafting inventory
            craftingRecipes = recipes ?? new List<CraftingRecipe>();
            craftingInventory = inventory;
            // The UI will be refreshed when CreateCraftingView is called.
        }

        private void CreateInventoryUI()
        {
            container.Clear();
            switch (InventoryManager.instance?.GetCurrentMode())
            {
                case InventoryMode.SINGLE:
                    CreateSingleView();
                    break;
                case InventoryMode.DUAL:
                    CreateDualView();
                    break;
                case InventoryMode.CRAFTING:
                    CreateCraftingView();
                    break;
                default:
                    CreateSingleView();
                    break;
            }
        }

        private void CreateSingleView()
        {
            var panel = CreateInventoryPanel("single-panel", 0);
            panel.style.position = Position.Absolute;
            panel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float screenWidth = container.resolvedStyle.width;
                float screenHeight = container.resolvedStyle.height;
                float panelWidth = panel.resolvedStyle.width;
                float panelHeight = panel.resolvedStyle.height;
                panel.style.left = (screenWidth - panelWidth) / 2;
                panel.style.top = (screenHeight - panelHeight) / 2;
                panel.UnregisterCallback<GeometryChangedEvent>(evt => { });
            });
            container.Add(panel);
        }

        private void CreateDualView()
        {
            var leftPanel = CreateInventoryPanel("left-panel", 0);
            var rightPanel = CreateInventoryPanel("right-panel", 1);
            leftPanel.style.position = Position.Absolute;
            rightPanel.style.position = Position.Absolute;

            leftPanel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float screenHeight = container.resolvedStyle.height;
                float leftHeight = leftPanel.resolvedStyle.height;
                leftPanel.style.left = 40;
                leftPanel.style.top = (screenHeight - leftHeight) / 2;
                leftPanel.UnregisterCallback<GeometryChangedEvent>(evt => { });
            });

            rightPanel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float screenHeight = container.resolvedStyle.height;
                float rightHeight = rightPanel.resolvedStyle.height;
                rightPanel.style.right = 65;
                rightPanel.style.top = (screenHeight - rightHeight) / 2;
                rightPanel.UnregisterCallback<GeometryChangedEvent>(evt => { });
            });

            container.Add(leftPanel);
            container.Add(rightPanel);
        }

        private VisualElement CreateInventoryPanel(string className, int inventoryIndex)
        {
            var inventory = displayedInventories[inventoryIndex];
            if (inventory == null) return new VisualElement();

            int capacity = inventory.GetCapacity();
            int rows = inventory.GetConfiguration()?.GetRows() ?? 4;
            int columns = Mathf.CeilToInt((float)capacity / rows);
            int panelWidth = (columns * (slotSize + slotSpacing)) + 30;
            int panelHeight = (rows * (slotSize + slotSpacing)) + 70;

            var panel = UIBuilder.CreateContainer()
                .WithName(className)
                .WithSize(panelWidth, panelHeight)
                .WithBackgroundColor(panelColor)
                .WithBorders(3, new Color(0.25f, 0.2f, 0.15f), 12)
                .WithPaddings(15)
                .Build();

            var header = UIBuilder.CreateContainer()
                .WithSize(Length.Percent(100), 40)
                .WithBackgroundColor(headerColor)
                .WithBorders(0, 0, 2, 0, Color.clear, Color.clear, new Color(0.4f, 0.3f, 0.2f), Color.clear)
                .WithBorderRadius(8, 8, 0, 0)
                .WithDisplay(UIDisplay.FLEX)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(0, 0, 15, 0)
                .Build();

            var titleLabel = UIBuilder.CreateLabel()
                .WithText(inventory.GetDisplayName())
                .WithColor(new Color(1f, 0.9f, 0.7f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .WithTextShadow(new Color(0.2f, 0.15f, 0.1f), new Vector2(1, 1))
                .Build<Label>();
            header.Add(titleLabel);
            panel.Add(header);

            var slotsContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.FlexStart)
                .Build();

            CreateSlots(slotsContainer, inventoryIndex, rows, columns);
            panel.Add(slotsContainer);
            return panel;
        }

        private void CreateSlots(VisualElement slotsContainer, int inventoryIndex, int rows, int columns)
        {
            var inventory = displayedInventories[inventoryIndex];
            if (inventory == null) return;

            int capacity = inventory.GetCapacity();
            inventorySlots[inventoryIndex] = new Slot[capacity];

            for (int row = 0; row < rows; row++)
            {
                var rowContainer = UIBuilder.CreateContainer()
                    .WithFlexDirection(FlexDirection.Row)
                    .WithJustifyContent(Justify.Center)
                    .WithMargins(0, 0, slotSpacing, 0)
                    .Build();

                for (int col = 0; col < columns; col++)
                {
                    int slotIndex = row * columns + col;
                    if (slotIndex >= capacity)
                    {
                        var emptySpace = UIBuilder.CreateContainer()
                            .WithSize(slotSize, slotSize)
                            .WithMargins(0, slotSpacing, 0, 0)
                            .WithVisibility(false)
                            .Build();
                        rowContainer.Add(emptySpace);
                        continue;
                    }

                    var slot = new Slot();
                    slot.index = slotIndex;
                    slot.inventoryIndex = inventoryIndex;

                    int capturedIndex = inventoryIndex;
                    int capturedSlotIndex = slotIndex;
                    slot.OnDragStarted += (s) => OnSlotDragStart(s, capturedIndex, capturedSlotIndex);
                    slot.OnPointerDown += OnSlotPointerDown;
                    slot.OnShiftClicked += OnSlotShiftClicked;

                    inventorySlots[inventoryIndex][slotIndex] = slot;
                    rowContainer.Add(slot);

                    var item = inventory.GetItem(slotIndex);
                    if (item != null) slot.Set(item);
                }
                slotsContainer.Add(rowContainer);
            }

            SubscribeToInventoryEvents(inventory, inventoryIndex);
        }

        private void SubscribeToInventoryEvents(InventoryComponent inventory, int index)
        {
            if (inventory == null) return;
            inventory.OnItemChanged += (item, slot) => OnInventoryItemChanged(index, item, slot);
            inventory.OnItemAdded += (item, slot) => OnInventoryItemChanged(index, item, slot);
            inventory.OnItemRemoved += (item, slot) => OnInventoryItemChanged(index, item, slot);
            inventory.OnItemMoved += (item, from, to) => OnInventoryItemMoved(index, item, from, to);
        }

        private void OnSlotPointerDown(Slot slot)
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            if (lastClickedSlot == slot && timeSinceLastClick < doubleClickThreshold)
            {
                CollectAllSimilarItems(slot);
                lastClickTime = 0;
            }
            else
            {
                lastClickedSlot = slot;
                lastClickTime = Time.time;
            }
        }

        private void CollectAllSimilarItems(Slot targetSlot)
        {
            var targetInv = displayedInventories[targetSlot.inventoryIndex];
            var targetItem = targetInv.GetItem(targetSlot.index);
            if (targetItem == null) return;

            System.Guid itemTypeId = targetItem.details.ID;
            int maxStack = targetItem.details.MaxStack;

            foreach (var inv in displayedInventories)
            {
                if (inv == null) continue;
                for (int i = 0; i < inv.GetCapacity(); i++)
                {
                    if (inv == targetInv && i == targetSlot.index) continue;
                    var item = inv.GetItem(i);
                    if (item != null && item.details.ID == itemTypeId)
                    {
                        int space = maxStack - targetItem.quantity;
                        if (space <= 0) break;
                        int transfer = Mathf.Min(item.quantity, space);
                        targetItem.quantity += transfer;
                        item.quantity -= transfer;
                        if (item.quantity <= 0)
                            inv.TryRemoveItem(i, item.quantity);
                        else
                            item.NotifyValueChanged();
                    }
                }
            }
            targetItem.NotifyValueChanged();
        }

        private void OnSlotShiftClicked(Slot slot)
        {
            int sourceInvIndex = slot.inventoryIndex;
            int targetInvIndex = (sourceInvIndex == 0) ? 1 : 0;
            var sourceInv = displayedInventories[sourceInvIndex];
            var targetInv = displayedInventories[targetInvIndex];
            if (sourceInv == null || targetInv == null) return;

            var item = sourceInv.GetItem(slot.index);
            if (item == null) return;
            sourceInv.TryTransferItem(slot.index, targetInv, -1, -1);
        }

        private void OnSlotDragStart(Slot slot, int inventoryIndex, int slotIndex)
        {
            var inventory = displayedInventories[inventoryIndex];
            if (inventory == null) return;

            var originalItem = inventory.GetItem(slotIndex);
            if (originalItem == null || originalItem.details == null || originalItem.details.Icon == null) return;

            draggedItem = originalItem.Copy();
            draggedQuantity = originalItem.quantity;
            inventory.TryRemoveItem(slotIndex, originalItem.quantity);

            dragging = true;
            dragSourceInventory = inventory;
            dragSourceIndex = inventoryIndex;
            dragSlotIndex = slotIndex;
            dragSource = slot;

            ghostIcon.style.backgroundImage = new StyleBackground(draggedItem.details.Icon);
            ghostIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            SetGhostIconPosition(Pointer.current.position.ReadValue());
            ghostIcon.style.visibility = Visibility.Visible;
            slot.style.opacity = 0.5f;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (draggedItem == null) return;
            SetGhostIconPosition(evt.position);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging || draggedItem == null) return;
            Slot dropTarget = FindDropTarget(evt.position);

            if (evt.button == 0)
            {
                bool success = false;
                if (dropTarget != null)
                {
                    if (dropTarget.inventoryIndex == -2)
                    {
                        success = false;
                    }
                    else
                    {
                        int targetInvIndex = dropTarget.inventoryIndex;
                        int targetSlot = dropTarget.index;
                        var targetInv = displayedInventories[targetInvIndex];
                        if (targetInv != null)
                        {
                            var itemToDrop = draggedItem.Copy();
                            itemToDrop.quantity = draggedQuantity;
                            if (targetInv.TryAddItem(itemToDrop, targetSlot))
                                success = true;
                        }
                    }
                }
                if (!success)
                    dragSourceInventory?.TryAddItem(draggedItem, dragSlotIndex);
                ResetDragState();
            }
            else if (evt.button == 1 && dropTarget != null && dropTarget.inventoryIndex >= 0)
            {
                var targetInv = displayedInventories[dropTarget.inventoryIndex];
                var oneItem = draggedItem.Copy();
                oneItem.quantity = 1;
                if (targetInv.TryAddItem(oneItem, dropTarget.index))
                {
                    draggedQuantity--;
                    if (draggedQuantity <= 0)
                        ResetDragState();
                }
            }
        }

        private void ResetDragState()
        {
            if (dragSource != null)
                dragSource.style.opacity = 1f;
            dragging = false;
            dragSource = null;
            dragSourceInventory = null;
            dragSourceIndex = -1;
            dragSlotIndex = -1;
            draggedItem = null;
            draggedQuantity = 0;
            ghostIcon.style.visibility = Visibility.Hidden;
        }

        private Slot FindDropTarget(Vector2 position)
        {
            for (int invIndex = 0; invIndex < 2; invIndex++)
            {
                if (inventorySlots[invIndex] == null) continue;
                foreach (var slot in inventorySlots[invIndex])
                    if (slot != null && slot.worldBound.Contains(position))
                        return slot;
            }
            return null;
        }

        private void SetGhostIconPosition(Vector2 position)
        {
            ghostIcon.style.top = position.y - (slotSize / 2);
            ghostIcon.style.left = position.x - (slotSize / 2);
            ghostIcon.BringToFront();
        }

        private void OnInventoryItemChanged(int inventoryIndex, Item item, int slotIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= inventorySlots.Length ||
                inventorySlots[inventoryIndex] == null ||
                slotIndex < 0 || slotIndex >= inventorySlots[inventoryIndex].Length)
                return;

            var slot = inventorySlots[inventoryIndex][slotIndex];
            if (slot == null) return;
            if (item == null) slot.Clear();
            else slot.Set(item);

            if (inventoryIndex == 1)
                EvaluateRecipe();
        }

        private void OnInventoryItemMoved(int inventoryIndex, Item item, int from, int to)
        {
            if (inventoryIndex < 0 || inventoryIndex >= inventorySlots.Length ||
                inventorySlots[inventoryIndex] == null)
                return;

            if (from >= 0 && from < inventorySlots[inventoryIndex].Length)
            {
                var fromSlot = inventorySlots[inventoryIndex][from];
                if (fromSlot != null) fromSlot.Clear();
            }
            if (to >= 0 && to < inventorySlots[inventoryIndex].Length && item != null)
            {
                var toSlot = inventorySlots[inventoryIndex][to];
                if (toSlot != null) toSlot.Set(item);
            }

            if (inventoryIndex == 1)
                EvaluateRecipe();
        }

        private void CleanupInventoryBindings()
        {
            for (int i = 0; i < 2; i++)
            {
                if (displayedInventories[i] != null)
                {
                    var inv = displayedInventories[i];
                    inv.OnItemChanged -= (item, slot) => OnInventoryItemChanged(i, item, slot);
                    inv.OnItemAdded -= (item, slot) => OnInventoryItemChanged(i, item, slot);
                    inv.OnItemRemoved -= (item, slot) => OnInventoryItemChanged(i, item, slot);
                    inv.OnItemMoved -= (item, from, to) => OnInventoryItemMoved(i, item, from, to);
                    displayedInventories[i] = null;
                }
                inventorySlots[i] = null;
            }
        }

        // ======================== CRAFTING VIEW ========================

        private void CreateCraftingView()
        {
            var mainHorizontal = UIBuilder.CreateContainer()
                .WithName("crafting-main-container")
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithFlexDirection(FlexDirection.Row)
                .WithJustifyContent(Justify.SpaceBetween)
                .WithAlignItems(Align.Center)
                .WithPaddings(50, 100)
                .Build();

            var playerPanel = CreateInventoryPanel("player-inventory-panel", 0);
            var centerPanel = CreateCraftingCenterPanel();
            var recipePanel = CreateRecipeListPanel();

            centerPanel.style.flexGrow = 1;
            playerPanel.style.marginRight = 40;
            recipePanel.style.marginLeft = 40;

            mainHorizontal.Add(playerPanel);
            mainHorizontal.Add(centerPanel);
            mainHorizontal.Add(recipePanel);
            container.Add(mainHorizontal);

            RefreshRecipeList();
            EvaluateRecipe();
        }

        private VisualElement CreateCraftingCenterPanel()
        {
            var panel = UIBuilder.CreateContainer()
                .WithName("crafting-center-panel")
                .WithWidth(350)
                .WithBackgroundColor(panelColor)
                .WithBorders(3, new Color(0.25f, 0.2f, 0.15f), 12)
                .WithPaddings(15)
                .Build();

            var header = UIBuilder.CreateContainer()
                .WithSize(Length.Percent(100), 40)
                .WithBackgroundColor(new Color(0.15f, 0.1f, 0.2f, 1f))
                .WithBorderRadius(8, 8, 0, 0)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(0, 0, 15, 0)
                .Build();
            var titleLabel = UIBuilder.CreateLabel()
                .WithText("Crafting")
                .WithColor(new Color(0.9f, 0.7f, 1f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();
            header.Add(titleLabel);
            panel.Add(header);

            if (craftingInventory == null)
            {
                Debug.LogError("Crafting inventory is null in CreateCraftingCenterPanel");
                return panel;
            }

            int capacity = craftingInventory.GetCapacity();
            inventorySlots[1] = new Slot[capacity];
            var gridContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithJustifyContent(Justify.Center)
                .WithAlignItems(Align.Center)
                .Build();

            for (int i = 0; i < capacity; i++)
            {
                int slotIndex = i;
                var slot = new Slot();
                slot.index = slotIndex;
                slot.inventoryIndex = 1;
                slot.OnDragStarted += (s) => OnSlotDragStart(s, 1, slotIndex);
                slot.OnPointerDown += OnSlotPointerDown;
                slot.OnShiftClicked += OnSlotShiftClicked;

                inventorySlots[1][slotIndex] = slot;
                gridContainer.Add(slot);

                var item = craftingInventory.GetItem(slotIndex);
                if (item != null) slot.Set(item);
            }
            panel.Add(gridContainer);

            var outputRow = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(20, 0, 20, 0)
                .Build();

            outputSlot = new Slot();
            outputSlot.inventoryIndex = -2;
            outputSlot.Clear();
            outputSlot.SetEnabled(false);
            outputSlot.RegisterCallback<ClickEvent>(evt => TakeOutput());
            outputRow.Add(outputSlot);

            craftButton = UIBuilder.CreateButton()
                .WithText("Craft")
                .WithBackgroundColor(new Color(0.2f, 0.5f, 0.2f))
                .WithColor(Color.white)
                .WithFontSize(16)
                .WithPaddings(10, 20)
                .WithBorders(2, Color.gray, 8)
                .OnClick(Craft)
                .Build<Button>();
            outputRow.Add(craftButton);
            panel.Add(outputRow);

            SubscribeToInventoryEvents(craftingInventory, 1);

            return panel;
        }

        private VisualElement CreateRecipeListPanel()
        {
            var panel = UIBuilder.CreateContainer()
                .WithName("recipe-list-panel")
                .WithWidth(300)
                .WithHeight(600) // Fixed height for the entire panel
                .WithBackgroundColor(panelColor)
                .WithBorders(3, new Color(0.25f, 0.2f, 0.15f), 12)
                .WithPaddings(15)
                .Build();

            var header = UIBuilder.CreateContainer()
                .WithSize(Length.Percent(100), 40)
                .WithBackgroundColor(new Color(0.15f, 0.1f, 0.2f, 1f))
                .WithBorderRadius(8, 8, 0, 0)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(0, 0, 15, 0)
                .Build();
            var titleLabel = UIBuilder.CreateLabel()
                .WithText("Recipes")
                .WithColor(new Color(0.9f, 0.7f, 1f))
                .WithFontSize(18)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();
            header.Add(titleLabel);
            panel.Add(header);

            // Use a ScrollView for scrolling recipe list
            var scrollView = UIBuilder.CreateScrollView()
                .WithName("recipe-scroll-view")
                .WithWidth(Length.Percent(100))
                .WithHeight(Length.Percent(100))
                .Build<ScrollView>();

            recipeListContainer = scrollView.contentContainer; // assign the content container to add entries
            panel.Add(scrollView);

            return panel;
        }

        private void RefreshRecipeList()
        {
            if (recipeListContainer == null) return;
            recipeListContainer.Clear();
            foreach (var recipe in craftingRecipes)
            {
                var entry = CreateRecipeEntry(recipe);
                recipeListContainer.Add(entry);
            }
        }

        private VisualElement CreateRecipeEntry(CraftingRecipe recipe)
        {
            var container = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f))
                .WithPaddings(8)
                .WithMargins(0, 0, 5, 0)
                .WithBorders(1, Color.gray, 4)
                .Build();

            var nameLabel = UIBuilder.CreateLabel()
                .WithText(recipe.displayName)
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .WithColor(new Color(1f, 0.9f, 0.5f))
                .Build<Label>();
            container.Add(nameLabel);

            var ingredientsLabel = UIBuilder.CreateLabel()
                .WithText("Requires:")
                .WithFontSize(11)
                .WithColor(Color.gray)
                .Build<Label>();
            container.Add(ingredientsLabel);

            foreach (var req in recipe.inputs)
            {
                var reqLabel = UIBuilder.CreateLabel()
                    .WithText($"- {req.item.ItemName} x{req.quantity}")
                    .WithFontSize(11)
                    .WithColor(Color.white)
                    .Build<Label>();
                container.Add(reqLabel);
            }

            string outputText = "Produces: ";
            foreach (var outItem in recipe.outputs)
                outputText += $"{outItem.item.ItemName} x{outItem.quantity} ";
            var outputLabel = UIBuilder.CreateLabel()
                .WithText(outputText)
                .WithFontSize(11)
                .WithColor(new Color(0.5f, 1f, 0.5f))
                .Build<Label>();
            container.Add(outputLabel);

            return container;
        }

        private void EvaluateRecipe()
        {
            if (craftingInventory == null) return;
            if (outputSlot == null || craftButton == null) return;

            var craftingItemsList = new List<Item>();
            for (int i = 0; i < craftingInventory.GetCapacity(); i++)
            {
                var item = craftingInventory.GetItem(i);
                if (item != null && item.quantity > 0)
                    craftingItemsList.Add(item);
            }

            CraftingRecipe bestRecipe = null;
            int maxCrafts = 0;

            foreach (var recipe in craftingRecipes)
            {
                if (recipe.Matches(craftingItemsList.ToArray()))
                {
                    int possible = int.MaxValue;
                    foreach (var req in recipe.inputs)
                    {
                        int total = 0;
                        foreach (var invItem in craftingItemsList)
                            if (invItem.details.ID == req.item.ID)
                                total += invItem.quantity;
                        possible = Mathf.Min(possible, total / req.quantity);
                    }
                    if (possible > maxCrafts)
                    {
                        maxCrafts = possible;
                        bestRecipe = recipe;
                    }
                }
            }

            if (bestRecipe != null && maxCrafts > 0)
            {
                var primary = bestRecipe.outputs[0];
                outputItem = primary.item.Create(primary.quantity * maxCrafts);
                outputSlot.Set(outputItem);
                craftButton.SetEnabled(true);
                craftButton.userData = maxCrafts;
            }
            else
            {
                outputItem = null;
                outputSlot.Clear();
                craftButton.SetEnabled(false);
                craftButton.userData = null;
            }
        }

        private void Craft()
        {
            if (outputItem == null) return;
            int maxCrafts = (craftButton.userData is int) ? (int)craftButton.userData : 0;
            if (maxCrafts <= 0) return;

            var craftingItemsList = new List<Item>();
            for (int i = 0; i < craftingInventory.GetCapacity(); i++)
            {
                var item = craftingInventory.GetItem(i);
                if (item != null && item.quantity > 0)
                    craftingItemsList.Add(item);
            }

            CraftingRecipe recipe = null;
            foreach (var r in craftingRecipes)
            {
                if (r.Matches(craftingItemsList.ToArray()))
                {
                    recipe = r;
                    break;
                }
            }
            if (recipe == null) return;

            for (int craft = 0; craft < maxCrafts; craft++)
            {
                foreach (var req in recipe.inputs)
                {
                    int remaining = req.quantity;
                    for (int i = 0; i < craftingInventory.GetCapacity() && remaining > 0; i++)
                    {
                        var slotItem = craftingInventory.GetItem(i);
                        if (slotItem != null && slotItem.details.ID == req.item.ID)
                        {
                            int take = Mathf.Min(remaining, slotItem.quantity);
                            craftingInventory.TryRemoveItem(i, take);
                            remaining -= take;
                        }
                    }
                }
                foreach (var outItem in recipe.outputs)
                {
                    var itemToAdd = outItem.item.Create(outItem.quantity);
                    displayedInventories[0].TryAddItem(itemToAdd);
                }
            }

            for (int i = 0; i < craftingInventory.GetCapacity(); i++)
            {
                if (inventorySlots[1] != null && inventorySlots[1][i] != null)
                {
                    var newItem = craftingInventory.GetItem(i);
                    if (newItem != null) inventorySlots[1][i].Set(newItem);
                    else inventorySlots[1][i].Clear();
                }
            }

            EvaluateRecipe();
        }

        private void TakeOutput()
        {
            if (outputItem == null) return;
            if (displayedInventories[0].TryAddItem(outputItem))
            {
                outputItem = null;
                outputSlot.Clear();
                EvaluateRecipe();
            }
            else
            {
                Debug.Log("Inventory full, cannot take output.");
            }
        }
    }
}