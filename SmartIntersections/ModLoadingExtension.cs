using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using System;
using System.Linq;
using UnityEngine;

namespace SmartIntersections
{
    public class ModLoadingExtension : ILoadingExtension
    {
        public static bool roadAnarchyDetected = false;

        public static readonly UInt64[] FineRoadAnarchy_IDs = { 802066100, 1844440354 };

        public void OnCreated(ILoading loading)
        {
            foreach (PluginManager.PluginInfo current in PluginManager.instance.GetPluginsInfo())
            {
                if ((current.isEnabled && FineRoadAnarchy_IDs.Contains( current.publishedFileID.AsUInt64 ) || current.name.Contains("FineRoadAnarchy"))) // Fine road anarchy dependency
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
            if(!roadAnarchyDetected)
            {
                ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                panel.SetMessage("Smart Intersection Builder", "Mod 'Smart Intersection Builder' requires mod 'Fine Road Anarchy' or 'Fine Road Anarchy 2' as dependency. " +
                    "Make sure it is installed and enabled in content manager. (If you are using local version of Fine Road Anarchy, make sure it is located in a folder " +
                    "called 'FineRoadAnarchy')", true);
            }

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
