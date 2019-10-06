using ColossalFramework;
using Redirection;
using SmartIntersections.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace SmartIntersections.Detours
{
    
    /* This detour hooks to ToolController.EndColliding to obtain list of colliding segments. Only segments reported by BuildingTool.SimulationStep are
     * registered. (I could hook to SimulationStep method directly, but that may cause more incompatibilities with other mods) */

    [TargetType(typeof(ToolController))]
    public class ToolControllerDetour
    {
        private static List<ushort> CollidingSegments = new List<ushort>();
        public static ushort[] CollidingSegmentsCache2; // public access

        /*public static void Apply(HarmonyInstance harmony)
        {
            var prefix = typeof(ToolControllerDetour).GetMethod("Prefix");
            harmony.Patch(OriginalMethod, new HarmonyMethod(prefix), null, null);
        }*/

        /*public static void Revert(HarmonyInstance harmony)
        {
            harmony.Unpatch(OriginalMethod, HarmonyPatchType.Prefix);
        }*/

        /*private static MethodInfo OriginalMethod => typeof(ToolController).GetMethod("EndColliding");*/

        // With triple underscore you can read private fields of target class
        [RedirectMethod]
        public static void EndColliding(/*ulong[] ___m_collidingSegments1*/)
        {
            StackTrace stackTrace = new StackTrace();
            ToolController targetInstance = Singleton<ToolManager>.instance.m_properties;
            if (stackTrace.GetFrame(1).GetMethod().Name == "SimulationStep")
            {
                ulong[] ___m_collidingSegments1 = typeof(ToolController).GetField("m_collidingSegments1", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(targetInstance) as ulong[];

                //UnityEngine.Debug.Log("EndColliding caller: " + stackTrace.GetFrame(2).GetMethod().GetUnderlyingType() + " " + stackTrace.GetFrame(2).GetMethod().Name + " " + ", count: " + CollidingSegments.Count);

                CollidingSegments.Clear();
                foreach (int segment in NetUtil.SegmentsFromMask(___m_collidingSegments1))
                {

                    CollidingSegments.Add((ushort)segment);
                }
                CollidingSegmentsCache2 = CollidingSegments.ToArray();
            }

            Redirector<ToolControllerDetour>.Revert();
            targetInstance.EndColliding();
            Redirector<ToolControllerDetour>.Deploy();

            //UnityEngine.Debug.Log("EndColliding: " + CollidingSegmentsCache2.Length);

        }
    }
}
