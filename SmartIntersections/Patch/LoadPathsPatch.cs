using HarmonyLib;
using SmartIntersections.Detours;
using SmartIntersections.SharedEnvironment;
using SmartIntersections.Utils;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace SmartIntersections.Patches
{
    //public static void LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
    [HarmonyPatch(typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths))]
    public class LoadPathsPatch
    {
        internal static WrappersDictionary _networkDictionary;
        internal static ActionGroup _actionGroup;
        internal static HashSet<ConnectionPoint> borderNodes;

        // Called before intersection is built
        internal static void ReleaseCollidingSegments()
        {
            // We obtain a list of nodes adjacent to the deleted segment to know where to reconnect
            HashSet<ConnectionPoint> borderNodes = new HashSet<ConnectionPoint>();
            if (ToolControllerDetour.CollidingSegmentsCache2 == null)
                return;
            foreach (ushort segment in ToolControllerDetour.CollidingSegmentsCache2)
            {
                try
                {
                    //Debug.Log("Releasing segment " + segment);
                    NetSegment netSegment = NetUtil.Segment(segment);

                    // We keep untouchable segments
                    if ((netSegment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                        continue;

                    bool inverted = ((netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);

                    borderNodes.Add(new ConnectionPoint(netSegment.m_startNode, netSegment.m_startDirection, netSegment.Info, inverted));
                    borderNodes.Add(new ConnectionPoint(netSegment.m_endNode, netSegment.m_endDirection, netSegment.Info, !inverted));

                    WrappedSegment segmentW = _networkDictionary.RegisterSegment(segment);
                    _actionGroup.Actions.Add(segmentW);
                    segmentW.IsBuildAction = false;
                    segmentW.Release();

                    if (segmentW.StartNode.TryRelease())
                    {
                        _actionGroup.Actions.Add(segmentW.StartNode);
                        segmentW.StartNode.IsBuildAction = false;
                    }

                    if (segmentW.EndNode.TryRelease())
                    {
                        _actionGroup.Actions.Add(segmentW.EndNode);
                        segmentW.EndNode.IsBuildAction = false;
                    }

                    //NetUtil.ReleaseSegment(segment, true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            borderNodes.RemoveWhere(n => !NetUtil.ExistsNode(n.Node));

            ToolControllerDetour.CollidingSegmentsCache2 = null;

            //Debug.Log("Border nodes (1): " + borderNodes.Count);

            borderNodes = borderNodes;
        }

        public static void Prefix(BuildingInfo info)
        {
            if (!SmartIntersections.instance.Active) return;
            if (info.m_paths != null)
            {
                // ns start
                if (info.m_paths.Length > 0)
                {
                    _networkDictionary = new WrappersDictionary();
                    _actionGroup = new ActionGroup("Build intersection");
                    ReleaseCollidingSegments();
                }
                // ns end
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions)
        {
            var fTempNodeBuffer = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempNodeBuffer))
                ?? throw new Exception("cound not find NetManager.m_tempNodeBuffer");
            var mClear = AccessTools.DeclaredMethod(fTempNodeBuffer.FieldType, nameof(FastList<ushort>.Clear))
                ?? throw new Exception("cound not find m_tempNodeBuffer.Clear");
            var mAfterIntersectionBuilt = AccessTools.DeclaredMethod(
                typeof(LoadPathsPatch), nameof(AfterIntersectionBuilt))
                ?? throw new Exception("cound not find AfterIntersectionBuilt()");

            List<CodeInstruction> codes = TranspilerUtils.ToCodeList(instructions);
            bool comp(int i) =>
                codes[i].opcode == OpCodes.Ldfld && codes[i].operand == fTempNodeBuffer &&
                codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 1].operand == mClear;
            int index = TranspilerUtils.SearchGeneric(codes, comp, index: 0, counter: 2);
            index -= 1; // index to insert instructions.

            var newInstructions = new[] {
                new CodeInstruction(OpCodes.Ldarg_0), // load argument info
                new CodeInstruction(OpCodes.Call, mAfterIntersectionBuilt),
            };

            TranspilerUtils.InsertInstructions(codes, newInstructions, index);
            return codes;
        }

        // Called after intersection is built
        internal static void AfterIntersectionBuilt(BuildingInfo info)
        {
            if (!SmartIntersections.instance.Active) return;
            var m_tempNodeBuffer = NetManager.instance.m_tempNodeBuffer;
            var m_tempSegmentBuffer =  NetManager.instance.m_tempSegmentBuffer;
            if (info.m_paths.Length > 0)
            {
                foreach (ushort node in m_tempNodeBuffer)
                {
                    var nodeW = _networkDictionary.RegisterNode(node);
                    _actionGroup.Actions.Add(nodeW);
                }
                foreach (ushort segment in m_tempSegmentBuffer)
                {
                    var segmentW = _networkDictionary.RegisterSegment(segment);
                    _actionGroup.Actions.Add(segmentW);
                }
                ReleaseQuestionableSegments(m_tempNodeBuffer, m_tempSegmentBuffer);
                new MakeConnections(borderNodes, m_tempNodeBuffer, _networkDictionary, _actionGroup);
                _actionGroup.IsDone = true;

                SmartIntersections.instance.PushGameAction(_actionGroup);
            }
        }

        /* Sometimes the intersection end snaps to an existing road. But it can happen that the intersection road and the road it snaps to are (more or
         * less) parallel. Then we are left with a piece of old road overlapping the new road because the old segment for some reason doesn't show up as
         * colliding. We have to find it and release it. I think that it shouldn't happen more than once per intersection tho. */
        private static void ReleaseQuestionableSegments(FastList<ushort> newNodes, FastList<ushort> newSegments)
        {
            foreach (ushort node in newNodes)
            {
                NetNode netNode = NetUtil.Node(node);
                ushort foundSegment = 0;
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = netNode.GetSegment(i);
                    if (segment != 0 && newSegments.Contains(segment))
                    {
                        if (foundSegment != 0)
                            goto continueOuterLoop;
                        else
                            foundSegment = segment;
                    }
                }

                Vector3 direction = NetUtil.Segment(foundSegment).GetDirection(node);
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = netNode.GetSegment(i);
                    if (segment != 0 && segment != foundSegment)
                    {
                        float angle = Vector3.Angle(direction, NetUtil.Segment(segment).GetDirection(node));
                        if (angle < 10)
                        {
                            //Debug.Log("Releasing questionable segment " + segment);
                            //NetUtil.ReleaseSegment(segment);
                            WrappedSegment segmentW = _networkDictionary.RegisterSegment(segment);
                            _actionGroup.Actions.Add(segmentW);
                            segmentW.IsBuildAction = false;
                            segmentW.Release();

                            if (segmentW.StartNode.TryRelease())
                            {
                                _actionGroup.Actions.Add(segmentW.StartNode);
                                segmentW.StartNode.IsBuildAction = false;
                            }

                            if (segmentW.EndNode.TryRelease())
                            {
                                _actionGroup.Actions.Add(segmentW.EndNode);
                                segmentW.EndNode.IsBuildAction = false;
                            }
                            /*WrappedSegment segmentW = _networkDictionary.RegisterSegment(segment);
                            segmentW.Release();*/

                            goto breakOuterLoop;
                        }
                    }
                }

            continueOuterLoop:;
            }
        breakOuterLoop:;
        }
    }
}
