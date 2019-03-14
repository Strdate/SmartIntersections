using Redirection;
using SmartIntersections.Detours;
using UnityEngine;

namespace SmartIntersections
{
    /* This class applies the detours and sets up road anarchy settings */

    public class SmartIntersections : MonoBehaviour
    {
        public static SmartIntersections instance;
        public static readonly string HarmonyID = "strad.smartintersections";

        //private HarmonyInstance _harmony;

        private bool m_tempAnarchy;
        private bool m_tempCollision;
        private bool m_tempSnapping;

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

        public SmartIntersections()
        {
            instance = this;
            //_harmony = HarmonyInstance.Create(HarmonyID);
        }

        private void SetupAnarchy()
        {
            m_tempAnarchy = FineRoadAnarchy.FineRoadAnarchy.anarchy;
            FineRoadAnarchy.FineRoadAnarchy.anarchy = true;
            m_tempCollision = FineRoadAnarchy.FineRoadAnarchy.collision;
            FineRoadAnarchy.FineRoadAnarchy.collision = true;
            m_tempSnapping = FineRoadAnarchy.FineRoadAnarchy.snapping;
            FineRoadAnarchy.FineRoadAnarchy.snapping = UIWindow.SavedSnapping.value;
        }

        private void RevertAnarchy()
        {
            FineRoadAnarchy.FineRoadAnarchy.anarchy = m_tempAnarchy;
            FineRoadAnarchy.FineRoadAnarchy.collision = m_tempCollision;
            FineRoadAnarchy.FineRoadAnarchy.snapping = m_tempSnapping;
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

    }
}
