using System;
using System.Collections.Generic;
using Game.Scripts.Core.UI;
using Game.Scripts.Inventory;
using Game.Scripts.Inventory.Items;
using Game.Scripts.Plants;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Planting {
    public class SeedSelectionPopup {
        private string popupId;
        private UIPopup popup;
        private Action<Item> onSeedSelected;

        public static void Show(InventoryComponent inventory, Action<Item> onSelected) {
            var popupInstance = new SeedSelectionPopup();
            popupInstance.Build(inventory, onSelected);
        }

        private void Build(InventoryComponent inventory, Action<Item> onSelected) {
            onSeedSelected = onSelected;
            popupId = $"seed_selection_{Guid.NewGuid()}";

            popup = UIManager.instance.CreatePopup(popupId, "Select Seed", draggable: true, closable: true)
                .SetSize(400, 500)
                .OnClose(() => {
                    onSeedSelected?.Invoke(null);
                    UIManager.instance.UnregisterPopup(popup);
                });

            var content = UIBuilder.CreateContainer()
                .WithFlexDirection(FlexDirection.Column)
                .WithPaddings(10)
                .Build();

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
                content.Add(label);
                // Auto close after 1.5 seconds
                popup.root.schedule.Execute(() => popup.Close()).StartingIn(1500);
            } else {
                var scrollView = new ScrollView();
                scrollView.style.flexGrow = 1;

                foreach (var seedItem in seedItems) {
                    var row = UIBuilder.CreateContainer()
                        .WithFlexDirection(FlexDirection.Row)
                        .WithMargins(0, 0, 8, 0)
                        .WithPaddings(5)
                        .WithBackgroundColor(new Color(0.2f, 0.2f, 0.2f, 0.8f))
                        .WithBorderRadius(6)
                        .Build();

                    var iconImg = UIBuilder.CreateImage()
                        .WithSprite(seedItem.details.Icon)
                        .WithSize(48, 48)
                        .Build<Image>();

                    var details = UIBuilder.CreateContainer()
                        .WithFlexDirection(FlexDirection.Column)
                        .WithMargins(5, 0, 0, 10)
                        .WithGrow(1)
                        .Build();

                    var nameLabel = UIBuilder.CreateLabel()
                        .WithText(seedItem.details.ItemName)
                        .WithFontSize(16)
                        .WithFontStyle(FontStyle.Bold)
                        .Build<Label>();

                    var quantityLabel = UIBuilder.CreateLabel()
                        .WithText($"x{seedItem.quantity}")
                        .WithColor(Color.gray)
                        .Build<Label>();

                    details.Add(nameLabel);
                    details.Add(quantityLabel);
                    row.Add(iconImg);
                    row.Add(details);

                    var selectBtn = UIBuilder.CreateButton()
                        .WithText("Plant")
                        .WithSize(80, 40)
                        .WithBackgroundColor(new Color(0.3f, 0.5f, 0.2f))
                        .WithBorderRadius(6)
                        .OnClick(() => {
                            onSeedSelected?.Invoke(seedItem);
                            popup.Close();
                        })
                        .Build<Button>();

                    row.Add(selectBtn);
                    scrollView.Add(row);
                }
                content.Add(scrollView);
            }

            popup.SetContent(content);
            popup.Show();
        }
    }
}