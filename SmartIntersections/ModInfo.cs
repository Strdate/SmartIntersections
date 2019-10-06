using ColossalFramework;
using ICities;
using System;
using UnityEngine;

namespace SmartIntersections
{
    public class ModInfo : IUserMod
    {
        public static readonly string SettingsFileName = "SmartIntersections";
        public static readonly string VERSION = "BETA 0.3.0";

        public string Name => "Smart Intersection Builder";

        public string Description => "Allows you to build intersections easily [" + VERSION + "]";

        //public static readonly Vector2 defWindowPosition = new Vector2(-100,0);
        public static readonly SavedInt savedWindowX = new SavedInt("windowX", SettingsFileName, -1, true);
        public static readonly SavedInt savedWindowY = new SavedInt("windowY", SettingsFileName, 0, true);

        public ModInfo()
        {
            try
            {
                // Creating setting file - from SamsamTS
                if (GameSettings.FindSettingsFileByName(SettingsFileName) == null)
                {
                    GameSettings.AddSettingsFile(new SettingsFile[] { new SettingsFile() { fileName = SettingsFileName } });
                }
            }
            catch (Exception e)
            {
                Debug.Log("Couldn't load/create the setting file.");
                Debug.LogException(e);
            }
        }
    }
}
