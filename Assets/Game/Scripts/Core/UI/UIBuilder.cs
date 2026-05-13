// UIBuilder.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Scripts.Core.UI {
    #region Enums

    public enum UIAnchor {
        TOP_LEFT,
        TOP_CENTER,
        TOP_RIGHT,
        MIDDLE_LEFT,
        MIDDLE_CENTER,
        MIDDLE_RIGHT,
        BOTTOM_LEFT,
        BOTTOM_CENTER,
        BOTTOM_RIGHT
    }

    public enum UIDisplay {
        FLEX,
        ABSOLUTE,
        NONE
    }

    public enum UIPosition {
        RELATIVE,
        ABSOLUTE
    }

    #endregion

    public class UIBuilder {
        private VisualElement element;

        public static UIBuilder CreateContainer() => new UIBuilder(new VisualElement());
        public static UIBuilder CreateLabel() => new UIBuilder(new Label());
        public static UIBuilder CreateButton() => new UIBuilder(new Button());
        public static UIBuilder CreateImage() => new UIBuilder(new Image());
        public static UIBuilder CreateTextField() => new UIBuilder(new TextField());
        public static UIBuilder CreateScrollView() => new UIBuilder(new ScrollView());
        public static UIBuilder CreateGroup() => new UIBuilder(new GroupBox());
        public static UIBuilder CreateSlider() => new UIBuilder(new Slider());
        public static UIBuilder CreateIntegerField() => new UIBuilder(new IntegerField());


        // Constructor for custom elements
        public UIBuilder(VisualElement element) {
            this.element = element;
        }

        #region Core Methods (Work with all VisualElements)

        public UIBuilder WithName(string name) {
            element.name = name;
            return this;
        }

        public UIBuilder WithClass(string className) {
            element.AddToClassList(className);
            return this;
        }

        public UIBuilder WithClasses(params string[] classes) {
            foreach (var className in classes) {
                element.AddToClassList(className);
            }
            return this;
        }

        public UIBuilder WithSize(StyleLength width, StyleLength height) {
            element.style.width = width;
            element.style.height = height;
            return this;
        }

        public UIBuilder WithSize(int width, int height) {
            element.style.width = width;
            element.style.height = height;
            return this;
        }

        public UIBuilder WithSize(Length width, Length height) {
            element.style.width = width;
            element.style.height = height;
            return this;
        }

        public UIBuilder WithWidth(StyleLength width) {
            element.style.width = width;
            return this;
        }

        public UIBuilder WithWidth(float width) {
            element.style.width = width;
            return this;
        }

        public UIBuilder WithWidth(Length width) {
            element.style.width = width;
            return this;
        }

        public UIBuilder WithHeight(StyleLength height) {
            element.style.height = height;
            return this;
        }

        public UIBuilder WithHeight(float height) {
            element.style.height = height;
            return this;
        }

        public UIBuilder WithHeight(Length height) {
            element.style.height = height;
            return this;
        }

        public UIBuilder WithMinSize(StyleLength minWidth, StyleLength minHeight) {
            element.style.minWidth = minWidth;
            element.style.minHeight = minHeight;
            return this;
        }

        public UIBuilder WithMaxSize(StyleLength maxWidth, StyleLength maxHeight) {
            element.style.maxWidth = maxWidth;
            element.style.maxHeight = maxHeight;
            return this;
        }

        public UIBuilder WithBackgroundColor(Color color) {
            element.style.backgroundColor = color;
            return this;
        }

        public UIBuilder WithColor(Color color) {
            element.style.color = color;
            return this;
        }

        public UIBuilder WithOpacity(float opacity) {
            element.style.opacity = opacity;
            return this;
        }

        public UIBuilder WithPosition(UIPosition position) {
            element.style.position = position == UIPosition.ABSOLUTE ? Position.Absolute : Position.Relative;
            return this;
        }

        public UIBuilder WithPosition(float? top = null, float? bottom = null, float? left = null, float? right = null) {
            element.style.position = Position.Absolute;
            if (top.HasValue) element.style.top = top.Value;
            if (bottom.HasValue) element.style.bottom = bottom.Value;
            if (left.HasValue) element.style.left = left.Value;
            if (right.HasValue) element.style.right = right.Value;
            return this;
        }

        public UIBuilder WithMargins(float all) => WithMargins(all, all, all, all);
        public UIBuilder WithMargins(float vertical, float horizontal) => WithMargins(vertical, horizontal, vertical, horizontal);
        public UIBuilder WithMargins(float top, float right, float bottom, float left) {
            element.style.marginTop = top;
            element.style.marginRight = right;
            element.style.marginBottom = bottom;
            element.style.marginLeft = left;
            return this;
        }

        public UIBuilder WithPaddings(float all) => WithPaddings(all, all, all, all);
        public UIBuilder WithPaddings(float vertical, float horizontal) => WithPaddings(vertical, horizontal, vertical, horizontal);
        public UIBuilder WithPaddings(float top, float right, float bottom, float left) {
            element.style.paddingTop = top;
            element.style.paddingRight = right;
            element.style.paddingBottom = bottom;
            element.style.paddingLeft = left;
            return this;
        }

        public UIBuilder WithBorders(float all, Color color, float? radius = null) => WithBorders(all, all, all, all, color, color, color, color, radius);
        public UIBuilder WithBorders(float vertical, float horizontal, Color color, float? radius = null) => WithBorders(vertical, horizontal, vertical, horizontal, color, color, color, color, radius);
        public UIBuilder WithBorders(float top, float right, float bottom, float left, Color colorTop, Color colorRight, Color colorBottom, Color colorLeft, float? radius = null) {
            element.style.borderTopWidth = top;
            element.style.borderRightWidth = right;
            element.style.borderBottomWidth = bottom;
            element.style.borderLeftWidth = left;
            element.style.borderTopColor = colorTop;
            element.style.borderRightColor = colorRight;
            element.style.borderBottomColor = colorBottom;
            element.style.borderLeftColor = colorLeft;

            if (radius.HasValue) {
                element.style.borderTopLeftRadius = radius.Value;
                element.style.borderTopRightRadius = radius.Value;
                element.style.borderBottomLeftRadius = radius.Value;
                element.style.borderBottomRightRadius = radius.Value;
            }
            return this;
        }


        public UIBuilder WithBorderRadius(float all) =>  WithBorderRadius(all, all, all, all);
        public UIBuilder WithBorderRadius(float topLeft, float topRight, float bottomLeft, float bottomRight) {
            element.style.borderTopLeftRadius = topLeft;
            element.style.borderTopRightRadius = topRight;
            element.style.borderBottomLeftRadius = bottomLeft;
            element.style.borderBottomRightRadius = bottomRight;
            return this;
        }

        public UIBuilder WithFlexDirection(FlexDirection direction) {
            element.style.flexDirection = direction;
            return this;
        }

        public UIBuilder WithAlignItems(Align align) {
            element.style.alignItems = align;
            return this;
        }

        public UIBuilder WithJustifyContent(Justify justify) {
            element.style.justifyContent = justify;
            return this;
        }

        public UIBuilder WithDisplay(UIDisplay display) {
            element.style.display = display == UIDisplay.FLEX ? DisplayStyle.Flex :
                                  display == UIDisplay.ABSOLUTE ? DisplayStyle.Flex :
                                  DisplayStyle.None;
            return this;
        }

        public UIBuilder WithVisibility(bool visible) {
            element.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            return this;
        }

        public UIBuilder WithGrow(float grow = 1) {
            element.style.flexGrow = grow;
            return this;
        }

        public UIBuilder WithShrink(float shrink = 1) {
            element.style.flexShrink = shrink;
            return this;
        }

        public UIBuilder WithWrap(Wrap wrap) {
            element.style.flexWrap = wrap;
            return this;
        }

        #endregion

        #region Type-Specific Methods (Safe casting)

        public UIBuilder WithText(string text) {
            if (element is Label label) label.text = text;
            else if (element is Button button) button.text = text;
            else if (element is TextField textField) textField.value = text;
            else if (element is GroupBox groupBox) groupBox.text = text;
            return this;
        }

        public UIBuilder WithFontSize(int size) {
            element.style.fontSize = size;
            return this;
        }

        public UIBuilder WithFontStyle(FontStyle style) {
            element.style.unityFontStyleAndWeight = style;
            return this;
        }

        public UIBuilder WithTextAlign(TextAnchor align) {
            element.style.unityTextAlign = align;
            return this;
        }

        public UIBuilder WithTextShadow(Color color, Vector2 offset, float blurRadius = 2) {
            if (element is Label label) {
                var shadow = new TextShadow {
                    color = color,
                    offset = offset,
                    blurRadius = blurRadius
                };
                label.style.textShadow = new StyleTextShadow(shadow);
            }
            return this;
        }

        public UIBuilder WithEllipsis() {
            element.style.whiteSpace = WhiteSpace.NoWrap;
            element.style.textOverflow = TextOverflow.Ellipsis;
            element.style.overflow = Overflow.Hidden;
            return this;
        }

        public UIBuilder OnClick(Action action) {
            if (element is Button button) {
                button.clicked += action;
            } else {
                element.RegisterCallback<ClickEvent>(evt => action?.Invoke());
            }
            return this;
        }

        public UIBuilder OnPointerEnter(Action action) {
            element.RegisterCallback<PointerEnterEvent>(evt => action?.Invoke());
            return this;
        }

        public UIBuilder OnPointerLeave(Action action) {
            element.RegisterCallback<PointerLeaveEvent>(evt => action?.Invoke());
            return this;
        }

        public UIBuilder WithSprite(Sprite sprite, ScaleMode scaleMode = ScaleMode.ScaleToFit) {
            if (element is Image image) {
                image.sprite = sprite;
                image.scaleMode = scaleMode;
            } else {
                element.style.backgroundImage = new StyleBackground(sprite);
                element.style.unityBackgroundScaleMode = scaleMode;
            }
            return this;
        }

        public UIBuilder WithTint(Color color) {
            if (element is Image image) {
                image.tintColor = color;
            }
            return this;
        }

        public UIBuilder WithChildren(params VisualElement[] children) {
            foreach (var child in children) {
                element.Add(child);
            }
            return this;
        }

        public UIBuilder WithChildren(IEnumerable<VisualElement> children) {
            foreach (var child in children) {
                element.Add(child);
            }
            return this;
        }

        public UIBuilder WithChild(Func<UIBuilder, VisualElement> childBuilder) {
            var child = childBuilder(new UIBuilder(new VisualElement()));
            element.Add(child);
            return this;
        }

        #endregion

        #region Specialized Methods

        public UIBuilder Anchor(UIAnchor anchor) {
            element.style.position = Position.Absolute;

            switch (anchor) {
                case UIAnchor.TOP_LEFT:
                    element.style.top = 0;
                    element.style.left = 0;
                    break;
                case UIAnchor.TOP_CENTER:
                    element.style.top = 0;
                    element.style.left = new StyleLength(Length.Percent(50));
                    element.style.transformOrigin = new TransformOrigin(Length.Percent(50), 0, 0);
                    element.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
                    break;
                case UIAnchor.TOP_RIGHT:
                    element.style.top = 0;
                    element.style.right = 0;
                    break;
                case UIAnchor.MIDDLE_LEFT:
                    element.style.top = new StyleLength(Length.Percent(50));
                    element.style.left = 0;
                    element.style.transformOrigin = new TransformOrigin(0, Length.Percent(50), 0);
                    element.style.translate = new StyleTranslate(new Translate(0, new Length(-50, LengthUnit.Percent)));
                    break;
                case UIAnchor.MIDDLE_CENTER:
                    element.style.top = new StyleLength(Length.Percent(50));
                    element.style.left = new StyleLength(Length.Percent(50));
                    element.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
                    element.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent)));
                    break;
                case UIAnchor.MIDDLE_RIGHT:
                    element.style.top = new StyleLength(Length.Percent(50));
                    element.style.right = 0;
                    element.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(50), 0);
                    element.style.translate = new StyleTranslate(new Translate(new Length(-100, LengthUnit.Percent), new Length(-50, LengthUnit.Percent)));
                    break;
                case UIAnchor.BOTTOM_LEFT:
                    element.style.bottom = 0;
                    element.style.left = 0;
                    break;
                case UIAnchor.BOTTOM_CENTER:
                    element.style.bottom = 0;
                    element.style.left = new StyleLength(Length.Percent(50));
                    element.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100), 0);
                    element.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent)));
                    break;
                case UIAnchor.BOTTOM_RIGHT:
                    element.style.bottom = 0;
                    element.style.right = 0;
                    break;
            }

            return this;
        }

        public UIBuilder FullScreen() {
            element.style.position = Position.Absolute;
            element.style.top = 0;
            element.style.bottom = 0;
            element.style.left = 0;
            element.style.right = 0;
            return this;
        }

        public UIBuilder CenterScreen(int width = 400, int height = 300) {
            element.style.position = Position.Absolute;
            element.style.width = width;
            element.style.height = height;
            element.style.top = new StyleLength(Length.Percent(50));
            element.style.left = new StyleLength(Length.Percent(50));
            element.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
            element.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent)));
            return this;
        }

        #endregion

        #region Build Methods

        public T Build<T>() where T : VisualElement {
            return (T)element;
        }

        public VisualElement Build() {
            return element;
        }

        #endregion
    }
}