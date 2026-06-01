using System;
using System.Collections.Generic;
using Game.Scripts.Core.Environment;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Plants;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Planting {
    public class SeedSelectionPopup {
        private const string POPUP_ID = "seed_selection";

        private UIPopup popup;
        private Action<Item> onSeedSelected;

        public static void Show(InventoryComponent inventory, Action<Item> onSelected) {
            var popupInstance = new SeedSelectionPopup();
            popupInstance.Build(inventory, onSelected);
        }

        private void Build(InventoryComponent inventory, Action<Item> onSelected) {
            onSeedSelected = onSelected;

            popup = UIManager.instance.CreatePopup(POPUP_ID, "Select Seed", draggable: true, closable: true)
                .SetSize(600, 700)
                .OnClose(() => {
                    onSeedSelected?.Invoke(null);
                });

            var mainContainer = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithPaddings(15)
                .Build();

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            var seedItems = new List<Item>();
            for (int i = 0; i < inventory.GetCapacity(); i++) {
                var item = inventory.GetItem(i);
                if (item != null && item.details.PlantsToGrow != null) {
                    seedItems.Add(item);
                }
            }

            if (seedItems.Count == 0) {
                var label = UIBuilder.CreateLabel()
                    .WithText("No seeds in inventory.")
                    .WithColor(Color.gray)
                    .WithFontSize(14)
                    .Build<Label>();
                mainContainer.Add(label);
                popup.root.schedule.Execute(() => popup.Close()).StartingIn(1500);
            } else {
                foreach (var seedItem in seedItems) {
                    PlantSO plant = seedItem.details.PlantsToGrow;
                    bool atmosOk = AtmosphereManager.instance != null && AtmosphereManager.instance.IsAtmosphereWithin(plant);
                    Color atmosColor = atmosOk ? Color.green : Color.red;

                    var card = UIBuilder.CreateContainer()
                        .WithFlexDirection(FlexDirection.Row)
                        .WithMargins(0, 0, 10, 0)
                        .WithPaddings(10)
                        .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f, 0.9f))
                        .WithBorderRadius(8)
                        .Build();

                    // Icon
                    var iconImg = UIBuilder.CreateImage()
                        .WithSprite(seedItem.details.Icon)
                        .WithSize(64, 64)
                        .Build<Image>();

                    // Info panel
                    var infoPanel = UIBuilder.CreateContainer()
                        .WithFlexDirection(FlexDirection.Column)
                        .WithMargins(10, 0, 0, 0)
                        .WithGrow(1)
                        .Build();

                    var nameLabel = UIBuilder.CreateLabel()
                        .WithText(seedItem.details.ItemName)
                        .WithFontSize(18)
                        .WithFontStyle(FontStyle.Bold)
                        .Build<Label>();

                    var qtyLabel = UIBuilder.CreateLabel()
                        .WithText($"x{seedItem.quantity}")
                        .WithColor(Color.gray)
                        .Build<Label>();

                    // Atmospheric requirements display
                    var atmosLabel = UIBuilder.CreateLabel()
                        .WithText($"O₂: {plant.oxygenMin}–{plant.oxygenMax}%  CO₂: {plant.carbonMin}–{plant.carbonMax}%  N₂: {plant.nitrogenMin}–{plant.nitrogenMax}%")
                        .WithFontSize(12)
                        .WithColor(atmosColor)
                        .Build<Label>();

                    // Water requirement
                    string waterInfo = plant.requiresWatering ? $"Needs water (max {plant.maxWater})" : "No watering needed";
                    var waterLabel = UIBuilder.CreateLabel()
                        .WithText(waterInfo)
                        .WithFontSize(12)
                        .WithColor(Color.cyan)
                        .Build<Label>();

                    infoPanel.Add(nameLabel);
                    infoPanel.Add(qtyLabel);
                    infoPanel.Add(atmosLabel);
                    infoPanel.Add(waterLabel);

                    // Plant button
                    var plantBtn = UIBuilder.CreateButton()
                        .WithText("Plant")
                        .WithSize(100, 50)
                        .WithBackgroundColor(atmosOk ? new Color(0.3f, 0.7f, 0.2f) : new Color(0.5f, 0.2f, 0.2f))
                        .WithBorderRadius(6)
                        .OnClick(() => {
                            if (!atmosOk) {
                                Debug.LogWarning("Atmosphere conditions not suitable for this plant!");
                                return;
                            }

                            onSeedSelected?.Invoke(seedItem);
                            popup.Close();
                        })
                        .Build<Button>();

                    card.Add(iconImg);
                    card.Add(infoPanel);
                    card.Add(plantBtn);
                    scrollView.Add(card);
                }
                mainContainer.Add(scrollView);
            }

            popup.SetContent(mainContainer);
            UIManager.instance.TogglePopup(POPUP_ID, popup);
        }
    }
}