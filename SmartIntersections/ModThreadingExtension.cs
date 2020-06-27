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
        private bool m_init = true;
        private int counter = 0;
        private int skipCounter = 0;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if ((skipCounter & 3) != 0)
            {
                skipCounter++;
                return;
            }

            skipCounter = 1;

            if (m_init)
            {
                counter++;
                if (counter > 3000)
                {
                    m_init = false;
                }
                UIComponent findItPanel = UIView.Find("FindItDefaultPanel");
                if (findItPanel != null)
                {
                    m_init = false;
                    findItPanel.eventVisibilityChanged += (comp, value) =>
                    {
                        SmartIntersections.instance.FollowPrefabSelection = value;
                    };
                }
            }
            if (SmartIntersections.instance != null && SmartIntersections.instance.FollowPrefabSelection)
            {
                SmartIntersections.instance.FollowFindItSelection();
            }
        }
    }
}
