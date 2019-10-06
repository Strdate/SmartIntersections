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
        public static readonly string HarmonyID = "strad.smartintersections";

        //private HarmonyInstance _harmony;

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
                    if(value)
                    {
                        FineRoadAnarchy.Redirection.Redirector<FineRoadAnarchy.Detours.NetInfoDetour>.Revert();
                        ApplySnapping();
                    }
                    else
                    {
                        ApplySnapping();
                        FineRoadAnarchy.Redirection.Redirector<FineRoadAnarchy.Detours.NetInfoDetour>.Deploy();
                    }
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
                        //ToolControllerDetour.Apply(_harmony);
                        //BuildingDecorationDetour.Apply(_harmony);
                        Redirector<ToolControllerDetour>.Deploy();
                        Redirector<BuildingDecorationDetour>.Deploy();
                    }
                    else
                    {
                        //Debug.Log("Reverting detour...");
                        Redirector<BuildingDecorationDetour>.Revert();
                        Redirector<ToolControllerDetour>.Revert();
                        //_harmony.UnpatchAll(HarmonyID);
                        // ToolControllerDetour.Revert(_harmony);
                        //NetManagerDetour.Revert(_harmony);
                        //NetToolDetour.Revert(_harmony);
                        //BuildingDecorationDetour.Revert(_harmony);
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
            //_harmony = HarmonyInstance.Create(HarmonyID);
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
            m_tempAnarchy = FineRoadAnarchy.FineRoadAnarchy.anarchy;
            FineRoadAnarchy.FineRoadAnarchy.anarchy = true;
            m_tempCollision = FineRoadAnarchy.FineRoadAnarchy.collision;
            FineRoadAnarchy.FineRoadAnarchy.collision = true;            
        }

        private void RevertAnarchy()
        {
            FineRoadAnarchy.FineRoadAnarchy.anarchy = m_tempAnarchy;
            FineRoadAnarchy.FineRoadAnarchy.collision = m_tempCollision;
        }

        public void OnDestroy()
        {
            Redirector<BuildingDecorationDetour>.Revert();
            Redirector<ToolControllerDetour>.Revert();
            //_harmony.UnpatchAll(HarmonyID);
            /*ToolControllerDetour.Revert(_harmony);
            NetManagerDetour.Revert(_harmony);
            NetToolDetour.Revert(_harmony);*/
        }

        public enum SnappingMode
        {
            Enabled = 0,
            Low = 1,
            Off = 2
        }

    }
}
