using ColossalFramework;
using ColossalFramework.UI;
using Redirection;
using SmartIntersections.Detours;
using SmartIntersections.SharedEnvironment;
using UnityEngine;

namespace SmartIntersections
{
    /* This class applies the detours and sets up road anarchy settings */

    public class SmartIntersections : MonoBehaviour
    {
        public static SmartIntersections instance;

        private GameAction m_lastAction;

        private bool m_tempAnarchy;
        private bool m_tempCollision;

        private bool m_windowOnScreen;
        public bool WindowOnScreen
        {
            get => m_windowOnScreen;
            set
            {
                if (!ModLoadingExtension.roadAnarchyDetected)
                    return;

                if(value != m_windowOnScreen)
                {
                    m_windowOnScreen = value;
                    ApplySnapping();
                }
            }
        }

        private bool m_active = false;
        public bool Active
        {
            get => m_active;
            set
            {
                if (!ModLoadingExtension.roadAnarchyDetected)
                    return;

                if(value != m_active)
                {
                    m_active = value;
                    if(m_active)
                    {
                        //Debug.Log("Deploying detour...");
                        SetupAnarchy();
                        Redirector<ToolControllerDetour>.Deploy();
                    }
                    else
                    {
                        //Debug.Log("Reverting detour...");
                        Redirector<ToolControllerDetour>.Revert();
                        RevertAnarchy();
                    }
                }
            }
        }

        private SnappingMode m_snapping;
        public SnappingMode Snapping
        {
            get => (WindowOnScreen ? m_snapping : 0);
            set
            {
                if(value != m_snapping)
                {
                    m_snapping = value;
                    ApplySnapping();
                }
            }
        }

        private bool m_followPrefabSelection = false;
        public bool FollowPrefabSelection
        {
            get => m_followPrefabSelection;
            set
            {
                m_followPrefabSelection = value;
                if (!value)
                {
                    UIWindow.instance.isVisible = false;
                }
            }
        }

        private void ApplySnapping()
        {
            //Debug.Log("Is anarchy snapping deployed? " + FineRoadAnarchy.Redirection.Redirector<FineRoadAnarchy.Detours.NetInfoDetour>.IsDeployed());
            //Debug.Log("Apply snapping: value - " + Snapping);
            if(Snapping == SnappingMode.Low)
            {
                NetInfoDetour.MinNodeDistance = 3f;
            } else if(Snapping == SnappingMode.Off)
            {
                NetInfoDetour.MinNodeDistance = 0f;
            }

            if(Snapping == SnappingMode.Enabled)
            {
                Redirector<NetInfoDetour>.Revert();
            }
            else
            {
                Redirector<NetInfoDetour>.Deploy();
            }
        }

        public SmartIntersections()
        {
            instance = this;
        }

        public void FollowFindItSelection()
        {
            ToolBase currentTool = ToolsModifierControl.toolController.CurrentTool;
            BuildingTool buildingTool = currentTool as BuildingTool;
            if(buildingTool?.m_prefab != null)
            {
                var prefab = buildingTool.m_prefab;
                UIWindow.instance.isVisible = prefab.category == "RoadsIntersection" || prefab.category == "RoadsRoadTolls";
            } else
            {
                if(UIWindow.instance != null)
                {
                    UIWindow.instance.isVisible = UIWindow.instance.IsIntersetionsPanelVisible(); // This check is probably useless, it should be enough to set false
                }
            }
        }

        // SIMULATION THREAD
        public void PushGameAction(GameAction action)
        {
            if(UIWindow.instance.m_intersectionPanel.isVisible) // Toll booths don't support undo
            {
                m_lastAction = action;
                UIWindow.instance.m_undoButton.isEnabled = true;
            }
        }

        public void Undo()
        {
            if(m_lastAction != null)
            {
                UIWindow.instance.m_undoButton.isEnabled = false;
                Singleton<SimulationManager>.instance.AddAction(() => {
                    m_lastAction.Undo();
                    m_lastAction = null;
                });
            }
        }

        private void SetupAnarchy()
        {
            m_tempAnarchy = NetworkAnarchy.NetworkAnarchy.Anarchy;
            NetworkAnarchy.NetworkAnarchy.Anarchy = true;
            m_tempCollision = NetworkAnarchy.NetworkAnarchy.Collision;
            NetworkAnarchy.NetworkAnarchy.Collision = true;            
        }

        private void RevertAnarchy()
        {
            NetworkAnarchy.NetworkAnarchy.Anarchy = m_tempAnarchy;
            NetworkAnarchy.NetworkAnarchy.Collision = m_tempCollision;
        }

        public void OnDestroy()
        {
            Redirector<ToolControllerDetour>.Revert();
        }

        public enum SnappingMode
        {
            Enabled = 0,
            Low = 1,
            Off = 2
        }
    }
}
