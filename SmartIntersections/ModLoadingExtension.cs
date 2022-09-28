using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using System;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace SmartIntersections
{
    public class ModLoadingExtension : ILoadingExtension
    {
        public static bool roadAnarchyDetected = false;

        //public static readonly UInt64[] FineRoadAnarchy_IDs = { 802066100, 1844440354 };
        public static readonly UInt64[] NETWORK_ANARCHY_IDs = { 2862881785 };

        public void OnCreated(ILoading loading)
        {
            foreach (PluginManager.PluginInfo current in PluginManager.instance.GetPluginsInfo())
            {
                if (current.isEnabled && (current.name.Contains("NetworkAnarchy") || NETWORK_ANARCHY_IDs.Contains(current.publishedFileID.AsUInt64))) {
                    roadAnarchyDetected = true;
                    break;
                }
            }

            if(!roadAnarchyDetected)
            {
                Debug.LogWarning("Mod 'Smart Intersection Builder' requires 'Network Anarchy' to work.");
            }
        }

        public void OnLevelLoaded(LoadMode mode)
        {
            if(!roadAnarchyDetected)
            {
                ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                panel.SetMessage("Smart Intersection Builder", "Mod 'Smart Intersection Builder' requires mod 'Network Anarchy' as dependency. " +
                    "Make sure it is installed and enabled in content manager. (If you are using local version of Network Anarchy, make sure it is located in a folder " +
                    "called 'NetworkAnarchy')", true);
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

            InstallHarmony();
        }

        public void OnLevelUnloading()
        {
            UninstallHarmony();
        }

        public void OnReleased()
        {
            
        }

        #region HARMONY
        public const string HARMONY_ID = "strad.smartintersections";
        public static void InstallHarmony()
        {
            new Harmony(HARMONY_ID).PatchAll();
        }
        public static void UninstallHarmony()
        {
            new Harmony(HARMONY_ID).UnpatchAll(HARMONY_ID);
        }
        #endregion
    }
}
