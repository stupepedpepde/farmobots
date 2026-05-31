using System.Collections.Generic;
using Game.Scripts.Core.Building.Buildings;
using Game.Scripts.Core.Environment.Terrain.Node;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Robot
{
    public class RobotManagementPopup
    {
        private const string POPUP_ID = "robot-computer";
        private RobotComputer computer;
        private List<RobotRecipe> recipes;
        private InventoryComponent playerInventory;

        private UIPopup popup;
        private VisualElement root;
        private ScrollView robotListScroll;
        private VisualElement recipeContainer;
        private Button recallAllButton;
        private Button closeButton;

        private Robot selectedRobot;
        private RobotRecipe selectedRecipe;
        private List<NodeType> availableNodeTypes = new List<NodeType>();

        private static readonly Color PanelBgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color HeaderColor = new Color(0.2f, 0.15f, 0.1f, 1f);
        private static readonly Color BorderColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color ButtonBgColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color CraftButtonColor = new Color(0.2f, 0.5f, 0.2f);
        private static readonly Color WarningButtonColor = new Color(0.5f, 0.3f, 0.2f);

        public bool IsOpen => popup != null && popup.root.parent != null;

        public RobotManagementPopup(RobotComputer computer, List<RobotRecipe> recipes, InventoryComponent playerInventory)
        {
            this.computer = computer;
            this.recipes = recipes;
            this.playerInventory = playerInventory;
        }

        private void BuildUI()
        {
            popup = UIManager.instance.CreatePopup(POPUP_ID, computer.GetComputerName(), draggable: true, closable: true)
                .SetSize(900, 800)
                .OnClose(OnPopupClosed);

            var mainRow = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithSize(Length.Percent(100), Length.Percent(100))
                .WithPaddings(15)
                .Build();

            var leftPanel = CreateRobotListPanel();
            var rightPanel = CreateRightPanel();

            mainRow.Add(leftPanel);
            mainRow.Add(rightPanel);

            popup.SetContent(mainRow);
            root = popup.content;
        }

        private void OnPopupClosed()
        {
            if (playerInventory != null)
            {
                playerInventory.OnItemChanged -= OnInventoryChanged;
                playerInventory.OnItemAdded -= OnInventoryChanged;
                playerInventory.OnItemRemoved -= OnInventoryChanged;
            }

            if (RobotManager.instance != null)
            {
                RobotManager.instance.OnRobotRegistered -= OnRobotsChanged;
                RobotManager.instance.OnRobotUnregistered -= OnRobotsChanged;
                RobotManager.instance.OnTaskAssigned -= OnRobotTaskChanged;
                RobotManager.instance.OnTaskCompleted -= OnRobotTaskChanged;
            }
        }

        public void Toggle()
        {
            if (popup == null) BuildUI();

            if (IsOpen)
                UIManager.instance.ClosePopup(POPUP_ID);
            else
            {
                RefreshAvailableNodeTypes();
                UIManager.instance.TogglePopup(POPUP_ID, popup);
                RefreshUI();

                if (playerInventory != null)
                {
                    playerInventory.OnItemChanged += OnInventoryChanged;
                    playerInventory.OnItemAdded += OnInventoryChanged;
                    playerInventory.OnItemRemoved += OnInventoryChanged;
                }

                if (RobotManager.instance != null)
                {
                    RobotManager.instance.OnRobotRegistered += OnRobotsChanged;
                    RobotManager.instance.OnRobotUnregistered += OnRobotsChanged;
                    RobotManager.instance.OnTaskAssigned += OnRobotTaskChanged;
                    RobotManager.instance.OnTaskCompleted += OnRobotTaskChanged;
                }
            }
        }

        private void RefreshAvailableNodeTypes()
        {
            availableNodeTypes = RobotManager.instance?.GetAllNodeTypes() ?? new List<NodeType>();
        }

        private VisualElement CreateRobotListPanel()
        {
            var panel = UIBuilder.CreateContainer()
                .WithName("robot-list-panel")
                .WithGrow(1)
                .WithBackgroundColor(PanelBgColor)
                .WithBorders(3, BorderColor, 12)
                .WithPaddings(15)
                .WithMargins(0, 10, 0, 0)
                .Build();

            var header = UIBuilder.CreateContainer()
                .WithSize(Length.Percent(100), 40)
                .WithBackgroundColor(HeaderColor)
                .WithBorders(0, 0, 2, 0, Color.clear, Color.clear, new Color(0.4f, 0.3f, 0.2f), Color.clear)
                .WithBorderRadius(8, 8, 0, 0)
                .WithDisplay(UIDisplay.FLEX)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(-15, -15, 15, -15)
                .WithPaddings(0, 15, 0, 15)
                .Build();

            var titleLabel = UIBuilder.CreateLabel()
                .WithText("Active Robots")
                .WithColor(new Color(1f, 0.9f, 0.7f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .WithTextShadow(new Color(0.2f, 0.15f, 0.1f), new Vector2(1, 1))
                .Build<Label>();
            header.Add(titleLabel);
            panel.Add(header);

            robotListScroll = new ScrollView(ScrollViewMode.Vertical);
            robotListScroll.style.flexGrow = 1;
            robotListScroll.style.maxHeight = Length.Percent(100);
            panel.Add(robotListScroll);

            return panel;
        }

        private VisualElement CreateRightPanel()
        {
            var panel = UIBuilder.CreateContainer()
                .WithName("right-panel")
                .WithWidth(350)
                .WithFlexDirection(FlexDirection.Column)
                .Build();

            var recipePanel = UIBuilder.CreateContainer()
                .WithBackgroundColor(PanelBgColor)
                .WithBorders(3, BorderColor, 12)
                .WithPaddings(15)
                .WithGrow(1)
                .WithMargins(0, 0, 10, 0)
                .Build();

            var header = UIBuilder.CreateContainer()
                .WithSize(Length.Percent(100), 40)
                .WithBackgroundColor(new Color(0.15f, 0.1f, 0.2f, 1f))
                .WithBorders(0, 0, 2, 0, Color.clear, Color.clear, new Color(0.3f, 0.2f, 0.4f), Color.clear)
                .WithBorderRadius(8, 8, 0, 0)
                .WithDisplay(UIDisplay.FLEX)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.Center)
                .WithMargins(-15, -15, 15, -15)
                .WithPaddings(0, 15, 0, 15)
                .Build();

            var recipeTitle = UIBuilder.CreateLabel()
                .WithText("Crafting")
                .WithColor(new Color(0.9f, 0.8f, 1f))
                .WithFontSize(20)
                .WithFontStyle(FontStyle.Bold)
                .WithTextShadow(new Color(0.15f, 0.1f, 0.2f), new Vector2(1, 1))
                .Build<Label>();
            header.Add(recipeTitle);
            recipePanel.Add(header);

            recipeContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .Build();
            recipePanel.Add(recipeContainer);

            panel.Add(recipePanel);

            var buttonRow = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithJustifyContent(Justify.SpaceBetween)
                .Build();

            recallAllButton = CreateStyledButton("Recall All", WarningButtonColor)
                .OnClick(() => computer.RecallAllRobots())
                .Build<Button>();

            closeButton = CreateStyledButton("Close", ButtonBgColor)
                .OnClick(() => computer.OnInteract())
                .Build<Button>();

            buttonRow.Add(recallAllButton);
            buttonRow.Add(closeButton);
            panel.Add(buttonRow);

            return panel;
        }

        private UIBuilder CreateStyledButton(string text, Color bgColor)
        {
            return UIBuilder.CreateButton()
                .WithText(text)
                .WithBackgroundColor(bgColor)
                .WithColor(Color.white)
                .WithFontSize(14)
                .WithPaddings(8, 16, 8, 16)
                .WithBorders(2, new Color(0.5f, 0.5f, 0.5f), 6)
                .WithMargins(0, 4, 0, 4);
        }

        private VisualElement CreateSmallButton(string text, Color bgColor, System.Action onClick)
        {
            return UIBuilder.CreateButton()
                .WithText(text)
                .WithFontSize(11)
                .WithPaddings(4, 10, 4, 10)
                .WithMargins(2, 2, 2, 2)
                .WithBackgroundColor(bgColor)
                .WithColor(Color.white)
                .WithBorders(2, new Color(0.5f, 0.5f, 0.5f), 4)
                .OnClick(onClick)
                .Build<Button>();
        }

        private void RefreshUI()
        {
            RefreshRobotList();
            RefreshRecipeList();
        }

        private void RefreshRobotList()
        {
            robotListScroll.Clear();
            var robots = RobotManager.instance?.GetRobots();
            if (robots == null || robots.Count == 0)
            {
                var emptyLabel = UIBuilder.CreateLabel()
                    .WithText("No robots active.")
                    .WithColor(Color.gray)
                    .Build<Label>();
                robotListScroll.Add(emptyLabel);
                return;
            }

            foreach (var robot in robots)
            {
                var robotEntry = CreateRobotEntry(robot);
                robotListScroll.Add(robotEntry);
            }
        }

        private VisualElement CreateRobotEntry(Robot robot)
        {
            var container = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithBackgroundColor(selectedRobot == robot ? new Color(0.3f, 0.3f, 0.2f) : new Color(0.2f, 0.2f, 0.2f))
                .WithPaddings(10)
                .WithMargins(0, 0, 5, 0)
                .WithBorders(2, new Color(0.4f, 0.4f, 0.4f), 6)
                .Build();

            container.RegisterCallback<ClickEvent>(evt =>
            {
                selectedRobot = robot;
                RefreshRobotList();
            });

            var iconLabel = UIBuilder.CreateLabel()
                .WithText(GetRobotTypeSymbol(robot.Type))
                .WithFontSize(24)
                .WithWidth(35)
                .Build<Label>();
            container.Add(iconLabel);

            var infoContainer = UIBuilder.CreateContainer()
                .WithGrow(1)
                .WithMargins(10, 0, 0, 0)
                .Build();

            var nameLabel = UIBuilder.CreateLabel()
                .WithText($"{robot.Type} - {robot.name}")
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .Build<Label>();
            infoContainer.Add(nameLabel);

            var statusLabel = UIBuilder.CreateLabel()
                .WithText($"State: {robot.CurrentState} | Energy: {robot.EnergyPercentage:P0}")
                .WithFontSize(12)
                .WithColor(GetStateColor(robot.CurrentState))
                .Build<Label>();
            infoContainer.Add(statusLabel);

            if (robot.QueuedTaskCount > 0)
            {
                var taskLabel = UIBuilder.CreateLabel()
                    .WithText($"Tasks: {robot.QueuedTaskCount} queued")
                    .WithFontSize(11)
                    .WithColor(new Color(0.7f, 0.7f, 0.5f))
                    .Build<Label>();
                infoContainer.Add(taskLabel);
            }

            // Priority dropdown for MINER robots (always visible)
            if (robot.Type == RobotType.MINER)
            {
                var priorityContainer = UIBuilder.CreateContainer()
                    .WithFlexDirection(FlexDirection.Row)
                    .WithAlignItems(Align.Center)
                    .WithMargins(0, 5, 0, 0)
                    .Build();

                var priorityLabel = new Label("Priority: ");
                priorityLabel.style.color = Color.white;
                priorityLabel.style.fontSize = 11;
                priorityLabel.style.marginRight = 5;
                priorityContainer.Add(priorityLabel);

                var dropdown = new DropdownField(GetPriorityOptions(), robot.PrioritizedNodeType?.name ?? "None");
                // Stylize the dropdown
                dropdown.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                dropdown.style.color = Color.white;
                dropdown.style.borderTopWidth = 1;
                dropdown.style.borderBottomWidth = 1;
                dropdown.style.borderLeftWidth = 1;
                dropdown.style.borderRightWidth = 1;
                dropdown.style.borderTopColor = new Color(0.5f, 0.5f, 0.5f);
                dropdown.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
                dropdown.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
                dropdown.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
                dropdown.style.borderTopLeftRadius = 4;
                dropdown.style.borderTopRightRadius = 4;
                dropdown.style.borderBottomLeftRadius = 4;
                dropdown.style.borderBottomRightRadius = 4;
                dropdown.style.paddingLeft = 6;
                dropdown.style.paddingRight = 6;
                dropdown.style.fontSize = 11;
                dropdown.style.width = 120;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    NodeType selected = evt.newValue == "None" ? null : availableNodeTypes.Find(t => t.name == evt.newValue);
                    robot.PrioritizedNodeType = selected;
                    RefreshRobotList(); // refresh to update dropdown
                });
                priorityContainer.Add(dropdown);
                infoContainer.Add(priorityContainer);
            }

            container.Add(infoContainer);

            var energyBarContainer = UIBuilder.CreateContainer()
                .WithWidth(50)
                .WithHeight(8)
                .WithBackgroundColor(Color.black)
                .WithBorders(1, Color.gray, 2)
                .WithMargins(0, 10, 0, 0)
                .Build();
            var energyFill = UIBuilder.CreateContainer()
                .WithWidth(Length.Percent(robot.EnergyPercentage * 100))
                .WithHeight(Length.Percent(100))
                .WithBackgroundColor(Color.Lerp(Color.red, Color.green, robot.EnergyPercentage))
                .Build();
            energyBarContainer.Add(energyFill);
            container.Add(energyBarContainer);

            if (selectedRobot == robot)
            {
                var actionContainer = UIBuilder.CreateContainer()
                    .WithFlexDirection(FlexDirection.Row)
                    .WithMargins(10, 0, 0, 0)
                    .Build();

                var workBtn = CreateSmallButton("Work", new Color(0.3f, 0.5f, 0.3f), () =>
                {
                    robot.ClearAllTasks();
                    robot.FindWork();
                    RefreshUI();
                });
                actionContainer.Add(workBtn);

                var recallBtn = CreateSmallButton("Recall", new Color(0.5f, 0.3f, 0.2f), () =>
                {
                    robot.ClearAllTasks();
                    robot.ReturnToBaseToRecharge();
                    RefreshUI();
                });

                actionContainer.Add(recallBtn);
                container.Add(actionContainer);
            }

            return container;
        }

        private List<string> GetPriorityOptions()
        {
            var options = new List<string> { "None" };
            options.AddRange(availableNodeTypes.ConvertAll(t => t.name));
            return options;
        }

        private void RefreshRecipeList()
        {
            recipeContainer.Clear();

            foreach (var recipe in recipes)
            {
                var recipeEntry = CreateRecipeEntry(recipe);
                recipeContainer.Add(recipeEntry);
            }
        }

        private VisualElement CreateRecipeEntry(RobotRecipe recipe)
        {
            // Main container: vertical layout
            var container = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithBackgroundColor(selectedRecipe == recipe ? new Color(0.3f, 0.2f, 0.3f) : new Color(0.2f, 0.2f, 0.2f))
                .WithPaddings(10)
                .WithMargins(0, 0, 8, 0)
                .WithBorders(2, new Color(0.4f, 0.4f, 0.4f), 6)
                .Build();

            container.RegisterCallback<ClickEvent>(evt =>
            {
                selectedRecipe = recipe;
                RefreshRecipeList();
            });

            // Top row: icon + name (left), requirements (right)
            var topRow = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .WithJustifyContent(Justify.SpaceBetween)
                .Build();

            // Left side: icon and name
            var leftInfo = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Row)
                .WithAlignItems(Align.Center)
                .Build();

            var iconLabel = UIBuilder.CreateLabel()
                .WithText(GetRobotTypeSymbol(recipe.robotType))
                .WithFontSize(20)
                .WithWidth(30)
                .Build<Label>();
            leftInfo.Add(iconLabel);

            var nameLabel = UIBuilder.CreateLabel()
                .WithText(recipe.displayName)
                .WithFontSize(14)
                .WithFontStyle(FontStyle.Bold)
                .WithMargins(10, 0, 0, 0)
                .Build<Label>();
            leftInfo.Add(nameLabel);

            topRow.Add(leftInfo);

            // Right side: requirements list (vertical)
            var requirementsContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithAlignItems(Align.FlexEnd)
                .WithMargins(0, 0, 0, 0)
                .Build();

            bool canCraft = playerInventory != null;

            foreach (var req in recipe.requirements)
            {
                bool hasEnough = playerInventory != null && playerInventory.HasItem(req.item.Create(1), req.quantity);
                if (!hasEnough) canCraft = false;

                var reqRow = UIBuilder.CreateContainer()
                    .WithFlexDirection(FlexDirection.Row)
                    .WithAlignItems(Align.Center)
                    .WithMargins(0, 0, 2, 0)
                    .Build();

                var reqText = UIBuilder.CreateLabel()
                    .WithText($"{req.item.ItemName} x{req.quantity}")
                    .WithFontSize(11)
                    .WithColor(hasEnough ? Color.green : new Color(1f, 0.7f, 0.4f))
                    .WithMargins(0, 0, 0, 8)
                    .Build<Label>();

                var checkMark = UIBuilder.CreateLabel()
                    .WithText(hasEnough ? " ✓" : " ✗")
                    .WithFontSize(11)
                    .WithColor(hasEnough ? Color.green : Color.red)
                    .Build<Label>();

                reqRow.Add(reqText);
                reqRow.Add(checkMark);
                requirementsContainer.Add(reqRow);
            }

            topRow.Add(requirementsContainer);
            container.Add(topRow);

            // Craft button
            var craftButton = CreateStyledButton("Craft", canCraft ? CraftButtonColor : ButtonBgColor)
                .WithFontSize(12)
                .WithPaddings(6, 12, 6, 12)
                .OnClick(() => { if (computer.TryCraftRobot(recipe)) RefreshUI(); })
                .Build<Button>();

            craftButton.SetEnabled(canCraft);
            container.Add(craftButton);

            return container;
        }

        private string GetRobotTypeSymbol(RobotType type)
        {
            return type switch
            {
                RobotType.PLANTER => "🌱",
                RobotType.HARVESTER => "🌾",
                RobotType.MINER => "⛏️",
                RobotType.GARDENER => "💧",
                _ => "🤖"
            };
        }

        private Color GetStateColor(RobotState state)
        {
            return state switch
            {
                RobotState.IDLE => Color.cyan,
                RobotState.MOVING => Color.yellow,
                RobotState.WORKING => Color.green,
                RobotState.RETURNING => new Color(1f, 0.5f, 0f),
                RobotState.RECHARGING => Color.blue,
                _ => Color.gray
            };
        }

        private void OnInventoryChanged(Item item, int slot) => RefreshUI();
        private void OnRobotsChanged(Robot robot) => RefreshUI();
        private void OnRobotTaskChanged(Robot robot, RobotTask task) => RefreshUI();
    }
}