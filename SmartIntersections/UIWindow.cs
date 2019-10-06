using ColossalFramework;
using ColossalFramework.UI;
using SmartIntersections.Utils;
using FineRoadAnarchy;
using UnityEngine;

namespace SmartIntersections
{
    public class UIWindow : UIPanel
    {
        public static UIWindow instance;

        private UIDropDown m_dropDown;
        private UICheckBox m_enabledCheckBox;
        public UICheckBox m_connectRoadsCheckBox;
        public UIButton m_undoButton;

        public static readonly SavedBool SavedEnabled = new SavedBool("savedEnabled", ModInfo.SettingsFileName, true, true);
        public static readonly SavedInt SavedSnapping = new SavedInt("savedSnapping", ModInfo.SettingsFileName, 0, true);
        public static readonly SavedBool SavedConnectRoads = new SavedBool("savedConnectRoads", ModInfo.SettingsFileName, true, true);

        public UIComponent m_intersectionPanel;
        public UIComponent m_tollPanel;

        public UIWindow()
        {
            instance = this;
        }

        /* The mod window turns on when RoadsIntersectionPanel or RoadsRoadTollsPanel is visible. */

        public override void Start()
        {
            name = "SmartIntersectionsPanel";

            if (!ModLoadingExtension.roadAnarchyDetected)
            {
                enabled = false;
                return;
            }

            atlas = ResourceLoader.GetAtlas("Ingame");
            backgroundSprite = "SubcategoriesPanel";
            size = new Vector2(204, 100);
            //Vector2 resolution = GetUIView().GetScreenResolution();
            absolutePosition = new Vector3(ModInfo.savedWindowX.value, ModInfo.savedWindowY.value);
            isVisible = false;
            clipChildren = true;

            eventPositionChanged += (c, p) =>
            {
                if (absolutePosition.x < 0)
                    absolutePosition = new Vector2(100, GetUIView().GetScreenResolution().y - height - 150);

                Vector2 resolution = GetUIView().GetScreenResolution();

                absolutePosition = new Vector2(
                    Mathf.Clamp(absolutePosition.x, 0, resolution.x - width),
                    Mathf.Clamp(absolutePosition.y, 0, resolution.y - height));

                ModInfo.savedWindowX.value = (int)absolutePosition.x;
                ModInfo.savedWindowY.value = (int)absolutePosition.y;
            };

            UIDragHandle dragHandle = AddUIComponent<UIDragHandle>();
            dragHandle.width = width;
            dragHandle.relativePosition = Vector3.zero;
            dragHandle.target = parent;

            float cumulativeHeight = 8;

            UILabel label = AddUIComponent<UILabel>();
            label.textScale = 0.9f;
            label.text = "Smart Intersections";
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height + 8;
            dragHandle.height = cumulativeHeight;

            m_enabledCheckBox = UI.CreateCheckBox(this);
            m_enabledCheckBox.name = "SI_Enabled";
            m_enabledCheckBox.label.text = "Enabled";
            m_enabledCheckBox.tooltip = "Enable Smart Intersections Tool";
            m_enabledCheckBox.isChecked = SavedEnabled.value;
            m_enabledCheckBox.relativePosition = new Vector3(8, cumulativeHeight);
            m_enabledCheckBox.eventCheckChanged += (c, state) =>
            {
                SmartIntersections.instance.Active = state;
                SavedEnabled.value = state;
            };
            cumulativeHeight += m_enabledCheckBox.height + 8;

            m_connectRoadsCheckBox = UI.CreateCheckBox(this);
            m_connectRoadsCheckBox.name = "SI_ConnectRoads";
            m_connectRoadsCheckBox.label.text = "Connect roads";
            m_connectRoadsCheckBox.tooltip = "Try connecting dead ends of roads";
            m_connectRoadsCheckBox.isChecked = SavedConnectRoads.value;
            m_connectRoadsCheckBox.relativePosition = new Vector3(8, cumulativeHeight);
            m_connectRoadsCheckBox.eventCheckChanged += (c, state) =>
            {
                SavedConnectRoads.value = state;
            };
            cumulativeHeight += m_connectRoadsCheckBox.height + 8;

            label = AddUIComponent<UILabel>();
            label.textScale = 0.9f;
            label.text = "Snapping";
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height + 8;

            m_dropDown = UI.CreateDropDown(this);
            m_dropDown.AddItem("Enabled");
            m_dropDown.AddItem("Low");
            m_dropDown.AddItem("Off");
            m_dropDown.relativePosition = new Vector3(8, cumulativeHeight);
            m_dropDown.width = width - 16;
            m_dropDown.eventSelectedIndexChanged += (component, state) =>
            {
                SmartIntersections.instance.Snapping = (SmartIntersections.SnappingMode) state;
                SavedSnapping.value = state;
            };
            m_dropDown.selectedIndex = SavedSnapping.value;
            m_dropDown.listPosition = UIDropDown.PopupListPosition.Above;
            cumulativeHeight += m_dropDown.height + 8;

            m_undoButton = UI.CreateButton(this);
            m_undoButton.text = "Undo";
            m_undoButton.tooltip = "Remove last built intersection";
            m_undoButton.relativePosition = new Vector2(8, cumulativeHeight);
            m_undoButton.width = width - 16;
            m_undoButton.isEnabled = false;
            m_undoButton.eventClick += (c, p) =>
            {
                SmartIntersections.instance.Undo();
            };
            cumulativeHeight += m_undoButton.height + 8;

            height = cumulativeHeight;
            //absolutePosition = ModInfo.defWindowPosition;

            m_intersectionPanel = UIView.Find("RoadsIntersectionPanel");
            m_intersectionPanel.eventVisibilityChanged += (comp, value) =>
            {
                //Debug.Log("Roads panel: visibility " + value);
                isVisible = (m_intersectionPanel.isVisible || m_tollPanel.isVisible);
            };
            m_tollPanel = UIView.Find("RoadsRoadTollsPanel");
            m_tollPanel.eventVisibilityChanged += (comp, value) =>
            {
                //Debug.Log("Tolls panel: visibility " + value);
                isVisible = (m_intersectionPanel.isVisible || m_tollPanel.isVisible);
            };
        }

        /* Activates the tool if window is visible and the 'Enabled' checkbox checked */
        protected override void OnVisibilityChanged()
        {
            SmartIntersections.instance.WindowOnScreen = isVisible;
            SmartIntersections.instance.Active = isVisible && SavedEnabled;
        }
    }
}
