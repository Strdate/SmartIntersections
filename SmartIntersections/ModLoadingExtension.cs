using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace SmartIntersections
{
    public class ModLoadingExtension : ILoadingExtension
    {
        public static bool roadAnarchyDetected = false;

        public void OnCreated(ILoading loading)
        {
            foreach (PluginManager.PluginInfo current in PluginManager.instance.GetPluginsInfo())
            {
                if ((current.publishedFileID.AsUInt64 == 802066100 || current.name == "FineRoadAnarchy") && current.isEnabled) // Fine road anarchy dependency
                {
                    roadAnarchyDetected = true;
                    break;
                    //Debug.Log("[Fine Road Anarchy detected!]");
                }
            }

            if(!roadAnarchyDetected)
            {
                Debug.LogWarning("Mod 'Smart Intersection Builder' requires 'Fine Road Anarchy' to work.");
            }
        }

        public void OnLevelLoaded(LoadMode mode)
        {
            if (SmartIntersections.instance == null)
            {
                // Creating the instance
                SmartIntersections.instance = new GameObject("SmartIntersections").AddComponent<SmartIntersections> ();
            }

            //instatiate UI
            if (UIWindow.instance == null)
            {
                UIView.GetAView().AddUIComponent(typeof(UIWindow));                
            }
        }

        public void OnLevelUnloading()
        {
            
        }

        public void OnReleased()
        {
            
        }
    }
}
