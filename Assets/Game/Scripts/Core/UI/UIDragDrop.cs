using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.UI {
    public class DragDropData {
        public VisualElement DragElement { get; set; }
        public object Data { get; set; }
        public Vector2 StartPosition { get; set; }
        public VisualElement SourceContainer { get; set; }
        public int SourceIndex { get; set; }
        public int SourceInventoryIndex { get; set; }
    }

    public static class UIDragDrop {
        private static VisualElement dragGhost;
        private static DragDropData currentDrag;
        private static bool isDragging;
        private static VisualElement dropHighlight;

        public static void MakeDraggable(VisualElement element, object data = null, int sourceIndex = -1, int sourceInventoryIndex = -1) {
            element.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0 || isDragging) return; // Left click only
                StartDrag(element, data, evt.position, sourceIndex, sourceInventoryIndex);
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerMoveEvent>(OnDrag);
            element.RegisterCallback<PointerUpEvent>(OnDragEnd);
        }

        public static void RegisterDropTarget(VisualElement target, System.Action<DragDropData> onDrop, string highlightClass = "drop-highlight") {
            target.RegisterCallback<PointerEnterEvent>(evt => {
                if (isDragging && currentDrag != null) {
                    target.AddToClassList(highlightClass);
                    dropHighlight = target;
                }
            });

            target.RegisterCallback<PointerLeaveEvent>(evt => {
                if (dropHighlight == target) {
                    target.RemoveFromClassList(highlightClass);
                    dropHighlight = null;
                }
            });

            target.RegisterCallback<PointerUpEvent>(evt => {
                if (isDragging && dropHighlight == target && onDrop != null) {
                    onDrop(currentDrag);
                    ResetDrag();
                }
            });
        }

        private static void StartDrag(VisualElement element, object data, Vector2 position, int sourceIndex, int sourceInventoryIndex) {
            isDragging = true;
            currentDrag = new DragDropData {
                DragElement = element,
                Data = data,
                StartPosition = element.worldBound.position,
                SourceContainer = element.parent,
                SourceIndex = sourceIndex,
                SourceInventoryIndex = sourceInventoryIndex
            };

            // Create ghost element
            dragGhost = UIBuilder.CreateContainer()
                .WithName("drag-ghost")
                .WithPosition(UIPosition.ABSOLUTE)
                .WithSize(element.resolvedStyle.width, element.resolvedStyle.height)
                .WithBackgroundColor(new Color(1, 1, 1, 0.5f))
                .WithBorders(2, Color.yellow, 8)
                .WithOpacity(0.8f)
                .Build();

            // Copy styles from original element
            var originalImage = element.Q<Image>();
            if (originalImage != null && originalImage.sprite != null) {
                dragGhost.style.backgroundImage = new StyleBackground(originalImage.sprite);
                dragGhost.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }

            var root = element.panel.visualTree;
            root.Add(dragGhost);
            dragGhost.BringToFront();

            // Hide original element slightly
            element.style.opacity = 0.5f;

            UpdateGhostPosition(position);
        }

        private static void OnDrag(PointerMoveEvent evt) {
            if (!isDragging) return;
            UpdateGhostPosition(evt.position);
            evt.StopPropagation();
        }

        private static void OnDragEnd(PointerUpEvent evt) {
            if (!isDragging) return;

            // If not dropped on a target, return to original position
            if (currentDrag != null && currentDrag.DragElement != null) {
                currentDrag.DragElement.style.opacity = 1f;
            }

            ResetDrag();
            evt.StopPropagation();
        }

        private static void UpdateGhostPosition(Vector2 position) {
            if (dragGhost == null) return;
            dragGhost.style.left = position.x - dragGhost.resolvedStyle.width / 2;
            dragGhost.style.top = position.y - dragGhost.resolvedStyle.height / 2;
        }

        private static void ResetDrag() {
            dragGhost?.RemoveFromHierarchy();
            dragGhost = null;

            if (dropHighlight != null) {
                dropHighlight.RemoveFromClassList("drop-highlight");
                dropHighlight = null;
            }

            currentDrag = null;
            isDragging = false;
        }

        public static bool IsDragging() => isDragging;
        public static DragDropData GetCurrentDrag() => currentDrag;
    }
}