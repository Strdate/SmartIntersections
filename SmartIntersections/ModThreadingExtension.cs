using ColossalFramework.UI;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartIntersections
{
    public class ModThreadingExtension : ThreadingExtensionBase
    {
        private bool m_firstRun = true;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (m_firstRun)
            {
                m_firstRun = false;
                UIComponent findItPanel = UIView.Find("FindItDefaultPanel");
                if (findItPanel != null)
                {
                    findItPanel.eventVisibilityChanged += (comp, value) =>
                    {
                        SmartIntersections.instance.FollowPrefabSelection = value;
                    };
                }
            }
            if(SmartIntersections.instance != null && SmartIntersections.instance.FollowPrefabSelection)
            {
                SmartIntersections.instance.FollowFindItSelection();
            }
        }
    }
}
