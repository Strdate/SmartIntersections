using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SmartIntersections.Utils
{
    public static class UI
    {
        /* The code below was copied from Fine Road Tool and More Shortcuts mod by SamsamTS.Thanks! */

        public static UICheckBox CreateCheckBox(UIComponent parent)
        {
            UICheckBox checkBox = (UICheckBox)parent.AddUIComponent<UICheckBox>();

            checkBox.width = 300f;
            checkBox.height = 20f;
            checkBox.clipChildren = true;

            UISprite sprite = checkBox.AddUIComponent<UISprite>();
            sprite.atlas = ResourceLoader.GetAtlas("Ingame");
            sprite.spriteName = "ToggleBase";
            sprite.size = new Vector2(16f, 16f);
            sprite.relativePosition = Vector3.zero;

            checkBox.checkedBoxObject = sprite.AddUIComponent<UISprite>();
            ((UISprite)checkBox.checkedBoxObject).atlas = ResourceLoader.GetAtlas("Ingame");
            ((UISprite)checkBox.checkedBoxObject).spriteName = "ToggleBaseFocused";
            checkBox.checkedBoxObject.size = new Vector2(16f, 16f);
            checkBox.checkedBoxObject.relativePosition = Vector3.zero;

            checkBox.label = checkBox.AddUIComponent<UILabel>();
            checkBox.label.text = " ";
            checkBox.label.textScale = 0.9f;
            checkBox.label.relativePosition = new Vector3(22f, 2f);

            checkBox.playAudioEvents = true;

            return checkBox;
        }

        public static UIButton CreateButton(UIComponent parent)
        {
            UIButton button = (UIButton)parent.AddUIComponent<UIButton>();

            button.atlas = ResourceLoader.GetAtlas("Ingame");
            button.size = new Vector2(90f, 30f);
            button.textScale = 0.9f;
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.canFocus = false;
            button.playAudioEvents = true;

            return button;
        }

        public static UIDropDown CreateDropDown(UIComponent parent)
        {
            UIDropDown dropDown = parent.AddUIComponent<UIDropDown>();

            dropDown.atlas = GetAtlas("Ingame");
            dropDown.size = new Vector2(90f, 30f);
            dropDown.listBackground = "GenericPanelLight";
            dropDown.itemHeight = 30;
            dropDown.itemHover = "ListItemHover";
            dropDown.itemHighlight = "ListItemHighlight";
            dropDown.normalBgSprite = "ButtonMenu";
            dropDown.disabledBgSprite = "ButtonMenuDisabled";
            dropDown.hoveredBgSprite = "ButtonMenuHovered";
            dropDown.focusedBgSprite = "ButtonMenu";
            dropDown.listWidth = 90;
            dropDown.listHeight = 500;
            dropDown.foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            dropDown.popupColor = new Color32(45, 52, 61, 255);
            dropDown.popupTextColor = new Color32(170, 170, 170, 255);
            dropDown.zOrder = 1;
            dropDown.textScale = 0.8f;
            dropDown.verticalAlignment = UIVerticalAlignment.Middle;
            dropDown.horizontalAlignment = UIHorizontalAlignment.Left;
            dropDown.selectedIndex = 0;
            dropDown.textFieldPadding = new RectOffset(8, 0, 8, 0);
            dropDown.itemPadding = new RectOffset(14, 0, 8, 0);
            dropDown.builtinKeyNavigation = true;

            UIButton button = dropDown.AddUIComponent<UIButton>();
            dropDown.triggerButton = button;
            button.atlas = GetAtlas("Ingame");
            button.text = "";
            button.size = dropDown.size;
            button.relativePosition = new Vector3(0f, 0f);
            button.textVerticalAlignment = UIVerticalAlignment.Middle;
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            button.normalFgSprite = "IconDownArrow";
            button.hoveredFgSprite = "IconDownArrowHovered";
            button.pressedFgSprite = "IconDownArrowPressed";
            button.focusedFgSprite = "IconDownArrowFocused";
            button.disabledFgSprite = "IconDownArrowDisabled";
            button.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            button.horizontalAlignment = UIHorizontalAlignment.Right;
            button.verticalAlignment = UIVerticalAlignment.Middle;
            button.zOrder = 0;
            button.textScale = 0.8f;

            dropDown.eventSizeChanged += new PropertyChangedEventHandler<Vector2>((c, t) =>
            {
                button.size = t; dropDown.listWidth = (int)t.x;
            });

            return dropDown;
        }

        private static Dictionary<string, UITextureAtlas> _atlases;

        public static UITextureAtlas GetAtlas(string name)
        {
            if (_atlases == null)
            {
                _atlases = new Dictionary<string, UITextureAtlas>();

                UITextureAtlas[] atlases = Resources.FindObjectsOfTypeAll(typeof(UITextureAtlas)) as UITextureAtlas[];
                for (int i = 0; i < atlases.Length; i++)
                {
                    if (!_atlases.ContainsKey(atlases[i].name))
                        _atlases.Add(atlases[i].name, atlases[i]);
                }
            }

            return _atlases[name];
        }
    }
}
