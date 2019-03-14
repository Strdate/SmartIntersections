using ColossalFramework;
using ColossalFramework.Math;
using Redirection;
using SmartIntersections.Tools;
using SmartIntersections.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SmartIntersections.Detours
{
    /* Hooks to BuildingDecoration.LoadPaths to intercept intersection/toll both creation. When triggered it deletes the overlapping segments
     * and obrains the list of new ones. Then MakeConnections class is called to connect the roads */

    [TargetType(typeof(BuildingDecoration))]
    public class BuildingDecorationDetour
    {
        #region DETOUR

        /*public static void Apply(HarmonyInstance harmony)
        {
            var fix = typeof(BuildingDecorationDetour).GetMethod("LoadPaths");
            harmony.Patch(OriginalMethod, new HarmonyMethod(fix), null, null);

        }*/

        /*public static void Revert(HarmonyInstance harmony)
        {
            harmony.Unpatch(OriginalMethod, HarmonyPatchType.Prefix);
        }*/

        /*private static MethodInfo OriginalMethod => typeof(BuildingDecoration).GetMethod("LoadPaths");*/

        #endregion DETOUR

        private static HashSet<ConnectionPoint> ReleaseCollidingSegments()
        {
            // We obtain a list of nodes adjacent to the deleted segment to know where to reconnect
            HashSet<ConnectionPoint> borderNodes = new HashSet<ConnectionPoint>();
            if (ToolControllerDetour.CollidingSegmentsCache2 == null)
                return null;
            foreach (ushort segment in ToolControllerDetour.CollidingSegmentsCache2)
            {
                //Debug.Log("Releasing segment " + segment);
                NetSegment netSegment = NetAccess.GetSegment(segment);

                // We keep untouchable segments
                if ((netSegment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                    continue;

                bool inverted = ((netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);

                borderNodes.Add(new ConnectionPoint(netSegment.m_startNode, netSegment.m_startDirection, netSegment.Info, inverted));
                borderNodes.Add(new ConnectionPoint(netSegment.m_endNode, netSegment.m_endDirection, netSegment.Info, !inverted));
                NetAccess.ReleaseSegment(segment, true);
            }

            borderNodes.RemoveWhere(n => !NetAccess.ExistsNode(n.Node));

            ToolControllerDetour.CollidingSegmentsCache2 = null;

            //Debug.Log("Border nodes (1): " + borderNodes.Count);

            return borderNodes;
        }

        // Solved by ReleaseQuestionableSegments
        /*private static void CheckSplitSegmentAngle(ref NetTool.ControlPoint point, FastList<ushort> createdSegments, HashSet<ConnectionPoint> borderNodes)
        {
            if(point.m_segment != 0)
            {
                if (createdSegments.Contains(point.m_segment))
                    return;

                if (point.m_node != 0)
                    return;

                Debug.Log("CheckSplitSegmentAngle: Snapping detected");
                NetSegment netSegment = NetAccess.GetSegment(point.m_segment);
                netSegment.GetClosestPositionAndDirection(point.m_position, out Vector3 pos, out Vector3 dir);
                float angle = Vector3.Angle(point.m_direction, dir);
                if(angle < 5 || 180 - angle < 5)
                {
                    Debug.Log("CheckSplitSegmentAngle: Releasing (" + angle + " deg)");
                    bool inverted = ((netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);
                    ConnectionPoint p1 = new ConnectionPoint(netSegment.m_startNode, netSegment.m_startDirection, netSegment.Info, inverted);
                    ConnectionPoint p2 = new ConnectionPoint(netSegment.m_endNode, netSegment.m_endDirection, netSegment.Info, !inverted);
                    NetAccess.ReleaseSegment(point.m_segment, true);
                    if(NetAccess.ExistsNode(p1.Node) && NetAccess.ExistsNode(p2.Node))
                    {
                        borderNodes.Add(p1);
                        borderNodes.Add(p2);
                    }
                }
            }
        }*/

        /* Sometimes the intersection end snaps to an existing road. But it can happen that the intersection road and the road it snaps to are (more or
         * less) parallel. Then we are left with a piece of old road overlapping the new road because the old segment for some reason doesn't show up as
         * colliding. We have to find it and release it. I think that it shouldn't happen more than once per intersection tho. */
        private static void ReleaseQuestionableSegments(FastList<ushort> newNodes, FastList<ushort> newSegments)
        {
            foreach (ushort node in newNodes)
            {
                NetNode netNode = NetAccess.GetNode(node);
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

                Vector3 direction = NetAccess.GetSegment(foundSegment).GetDirection(node);
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = netNode.GetSegment(i);
                    if (segment != 0 && segment != foundSegment)
                    {
                        float angle = Vector3.Angle(direction, NetAccess.GetSegment(segment).GetDirection(node));
                        if (angle < 10)
                        {
                            //Debug.Log("Releasing questionable segment " + segment);
                            NetAccess.ReleaseSegment(segment);
                            goto breakOuterLoop;
                        }
                    }
                }

                continueOuterLoop:;
            }
            breakOuterLoop:;
        }

        /* === STOCK CODE START === */

        [RedirectMethod]
        public static bool LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
        {
            if (info.m_paths != null)
            {
                // ns start
                HashSet<ConnectionPoint> borderNodes = null;
                if (info.m_paths.Length > 0)
                {
                    //Debug.Log("LoadPaths detour");
                    borderNodes = ReleaseCollidingSegments();
                }
                // ns end

                NetManager instance = Singleton<NetManager>.instance;
                instance.m_tempNodeBuffer.Clear();
                instance.m_tempSegmentBuffer.Clear();
                for (int i = 0; i < info.m_paths.Length; i++)
                {
                    BuildingInfo.PathInfo pathInfo = info.m_paths[i];
                    if (pathInfo.m_finalNetInfo != null && pathInfo.m_nodes != null && pathInfo.m_nodes.Length != 0)
                    {
                        Vector3 vector = data.CalculatePosition(pathInfo.m_nodes[0]);
                        bool flag = /*BuildingDecoration.*/RequireFixedHeight(info, pathInfo.m_finalNetInfo, pathInfo.m_nodes[0]);
                        if (!flag)
                        {
                            vector.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, vector, false, pathInfo.m_nodes[0].y + elevation);
                        }
                        Ray ray = new Ray(vector + new Vector3(0f, 8f, 0f), Vector3.down);
                        NetTool.ControlPoint controlPoint;
                        if (!FindConnectNode(instance.m_tempNodeBuffer, vector, pathInfo.m_finalNetInfo, out controlPoint))
                        {
                            if (NetTool.MakeControlPoint(ray, 16f, pathInfo.m_finalNetInfo, true, NetNode.Flags.Untouchable, NetSegment.Flags.Untouchable, Building.Flags.All, pathInfo.m_nodes[0].y + elevation - pathInfo.m_finalNetInfo.m_buildHeight, true, out controlPoint))
                            {
                                Vector3 vector2 = controlPoint.m_position - vector;
                                if (!flag)
                                {
                                    vector2.y = 0f;
                                }
                                float sqrMagnitude = vector2.sqrMagnitude;
                                if (sqrMagnitude > pathInfo.m_maxSnapDistance * pathInfo.m_maxSnapDistance)
                                {
                                    controlPoint.m_position = vector;
                                    controlPoint.m_elevation = 0f;
                                    controlPoint.m_node = 0;
                                    controlPoint.m_segment = 0;
                                }
                                else
                                {
                                    controlPoint.m_position.y = vector.y;
                                }
                            }
                            else
                            {
                                controlPoint.m_position = vector;
                            }
                        }

                        // ns start
                        /*if (!instance.m_tempNodeBuffer.Contains(controlPoint.m_node))
                        {
                            controlPoint.m_position = vector;
                            controlPoint.m_elevation = 0f;
                            controlPoint.m_node = 0;
                            controlPoint.m_segment = 0;
                        }*/
                        // ns end

                        //CheckSplitSegmentAngle(ref controlPoint, instance.m_tempSegmentBuffer, borderNodes); // ns

                        ushort node;
                        ushort num2;
                        int num3;
                        int num4;
                        if (controlPoint.m_node != 0)
                        {
                            instance.m_tempNodeBuffer.Add(controlPoint.m_node);
                        }
                        else if (NetTool.CreateNode(pathInfo.m_finalNetInfo, controlPoint, controlPoint, controlPoint, NetTool.m_nodePositionsSimulation, 0, false, false, false, false, pathInfo.m_invertSegments, false, 0, out node, out num2, out num3, out num4) == ToolBase.ToolErrors.None)
                        {
                            instance.m_tempNodeBuffer.Add(node);
                            controlPoint.m_node = node;
                            if (pathInfo.m_forbidLaneConnection != null && pathInfo.m_forbidLaneConnection.Length > 0 && pathInfo.m_forbidLaneConnection[0])
                            {
                                NetNode[] buffer = instance.m_nodes.m_buffer;
                                ushort num5 = node;
                                buffer[(int)num5].m_flags = (buffer[(int)num5].m_flags | NetNode.Flags.ForbidLaneConnection);
                            }
                            if (pathInfo.m_trafficLights != null && pathInfo.m_trafficLights.Length > 0)
                            {
                                /*BuildingDecoration.*/
                                TrafficLightsToFlags(pathInfo.m_trafficLights[0], ref instance.m_nodes.m_buffer[(int)node].m_flags);
                            }
                        }
                        for (int j = 1; j < pathInfo.m_nodes.Length; j++)
                        {
                            vector = data.CalculatePosition(pathInfo.m_nodes[j]);
                            bool flag2 = /*BuildingDecoration.*/RequireFixedHeight(info, pathInfo.m_finalNetInfo, pathInfo.m_nodes[j]);
                            if (!flag2)
                            {
                                vector.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, vector, false, pathInfo.m_nodes[j].y + elevation);
                            }
                            ray = new Ray(vector + new Vector3(0f, 8f, 0f), Vector3.down);
                            NetTool.ControlPoint controlPoint2;
                            if (!/*BuildingDecoration.*/FindConnectNode(instance.m_tempNodeBuffer, vector, pathInfo.m_finalNetInfo, out controlPoint2))
                            {
                                if (NetTool.MakeControlPoint(ray, 16f, pathInfo.m_finalNetInfo, true, NetNode.Flags.Untouchable, NetSegment.Flags.Untouchable, Building.Flags.All, pathInfo.m_nodes[j].y + elevation - pathInfo.m_finalNetInfo.m_buildHeight, true, out controlPoint2))
                                {
                                    Vector3 vector3 = controlPoint2.m_position - vector;
                                    if (!flag2)
                                    {
                                        vector3.y = 0f;
                                    }
                                    float sqrMagnitude2 = vector3.sqrMagnitude;
                                    if (sqrMagnitude2 > pathInfo.m_maxSnapDistance * pathInfo.m_maxSnapDistance)
                                    {
                                        controlPoint2.m_position = vector;
                                        controlPoint2.m_elevation = 0f;
                                        controlPoint2.m_node = 0;
                                        controlPoint2.m_segment = 0;
                                    }
                                    else
                                    {
                                        controlPoint2.m_position.y = vector.y;
                                    }
                                }
                                else
                                {
                                    controlPoint2.m_position = vector;
                                }
                            }
                            NetTool.ControlPoint middlePoint = controlPoint2;
                            if (pathInfo.m_curveTargets != null && pathInfo.m_curveTargets.Length >= j)
                            {
                                middlePoint.m_position = data.CalculatePosition(pathInfo.m_curveTargets[j - 1]);
                                if (!flag || !flag2)
                                {
                                    middlePoint.m_position.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, middlePoint.m_position, false, pathInfo.m_curveTargets[j - 1].y + elevation);
                                }
                            }
                            else
                            {
                                middlePoint.m_position = (controlPoint.m_position + controlPoint2.m_position) * 0.5f;
                            }
                            middlePoint.m_direction = VectorUtils.NormalizeXZ(middlePoint.m_position - controlPoint.m_position);
                            controlPoint2.m_direction = VectorUtils.NormalizeXZ(controlPoint2.m_position - middlePoint.m_position);
                            ushort num6;
                            ushort num7;
                            ushort num8;
                            int num9;
                            int num10;
                            if (NetTool.CreateNode(pathInfo.m_finalNetInfo, controlPoint, middlePoint, controlPoint2, NetTool.m_nodePositionsSimulation, 1, false, false, false, false, false, pathInfo.m_invertSegments, false, 0, out num6, out num7, out num8, out num9, out num10) == ToolBase.ToolErrors.None)
                            {
                                instance.m_tempNodeBuffer.Add(num7);
                                instance.m_tempSegmentBuffer.Add(num8);
                                controlPoint2.m_node = num7;
                                if (pathInfo.m_forbidLaneConnection != null && pathInfo.m_forbidLaneConnection.Length > j && pathInfo.m_forbidLaneConnection[j])
                                {
                                    NetNode[] buffer2 = instance.m_nodes.m_buffer;
                                    ushort num11 = num7;
                                    buffer2[(int)num11].m_flags = (buffer2[(int)num11].m_flags | NetNode.Flags.ForbidLaneConnection);
                                }
                                if (pathInfo.m_trafficLights != null && pathInfo.m_trafficLights.Length > j)
                                {
                                    /*BuildingDecoration.*/
                                    TrafficLightsToFlags(pathInfo.m_trafficLights[j], ref instance.m_nodes.m_buffer[(int)num7].m_flags);
                                }
                                if (pathInfo.m_yieldSigns != null && pathInfo.m_yieldSigns.Length >= j * 2)
                                {
                                    if (pathInfo.m_yieldSigns[j * 2 - 2])
                                    {
                                        NetSegment[] buffer3 = instance.m_segments.m_buffer;
                                        ushort num12 = num8;
                                        buffer3[(int)num12].m_flags = (buffer3[(int)num12].m_flags | NetSegment.Flags.YieldStart);
                                    }
                                    if (pathInfo.m_yieldSigns[j * 2 - 1])
                                    {
                                        NetSegment[] buffer4 = instance.m_segments.m_buffer;
                                        ushort num13 = num8;
                                        buffer4[(int)num13].m_flags = (buffer4[(int)num13].m_flags | NetSegment.Flags.YieldEnd);
                                    }
                                }
                            }
                            controlPoint = controlPoint2;
                            flag = flag2;
                        }
                    }
                }
                for (int k = 0; k < instance.m_tempNodeBuffer.m_size; k++)
                {
                    ushort num14 = instance.m_tempNodeBuffer.m_buffer[k];
                    if ((instance.m_nodes.m_buffer[(int)num14].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                    {
                        if (buildingID != 0)
                        {
                            if ((data.m_flags & Building.Flags.Active) == Building.Flags.None && instance.m_nodes.m_buffer[(int)num14].Info.m_canDisable)
                            {
                                NetNode[] buffer5 = instance.m_nodes.m_buffer;
                                ushort num15 = num14;
                                buffer5[(int)num15].m_flags = (buffer5[(int)num15].m_flags | NetNode.Flags.Disabled);
                            }
                            NetNode[] buffer6 = instance.m_nodes.m_buffer;
                            ushort num16 = num14;
                            buffer6[(int)num16].m_flags = (buffer6[(int)num16].m_flags | NetNode.Flags.Untouchable);
                            instance.UpdateNode(num14);
                            instance.m_nodes.m_buffer[(int)num14].m_nextBuildingNode = data.m_netNode;
                            data.m_netNode = num14;
                        }
                        else
                        {
                            instance.UpdateNode(num14);
                        }
                    }
                }
                for (int l = 0; l < instance.m_tempSegmentBuffer.m_size; l++)
                {
                    ushort num17 = instance.m_tempSegmentBuffer.m_buffer[l];
                    if ((instance.m_segments.m_buffer[(int)num17].m_flags & NetSegment.Flags.Untouchable) == NetSegment.Flags.None)
                    {
                        if (buildingID != 0)
                        {
                            NetSegment[] buffer7 = instance.m_segments.m_buffer;
                            ushort num18 = num17;
                            buffer7[(int)num18].m_flags = (buffer7[(int)num18].m_flags | NetSegment.Flags.Untouchable);
                            instance.UpdateSegment(num17);
                        }
                        else
                        {
                            if ((Singleton<ToolManager>.instance.m_properties.m_mode & ItemClass.Availability.AssetEditor) != ItemClass.Availability.None)
                            {
                                NetInfo info2 = instance.m_segments.m_buffer[(int)num17].Info;
                                if ((info2.m_availableIn & ItemClass.Availability.AssetEditor) == ItemClass.Availability.None)
                                {
                                    NetSegment[] buffer8 = instance.m_segments.m_buffer;
                                    ushort num19 = num17;
                                    buffer8[(int)num19].m_flags = (buffer8[(int)num19].m_flags | NetSegment.Flags.Untouchable);
                                }
                            }
                            instance.UpdateSegment(num17);
                        }
                    }
                }
                // ns start
                if (info.m_paths.Length > 0)
                {
                    ReleaseQuestionableSegments(instance.m_tempNodeBuffer, instance.m_tempSegmentBuffer);
                    new MakeConnections(borderNodes, instance.m_tempNodeBuffer);
                }
                // ns end    
                instance.m_tempNodeBuffer.Clear();
                instance.m_tempSegmentBuffer.Clear();
            }

            return false; // ns
        }

        private static void TrafficLightsToFlags(BuildingInfo.TrafficLights trafficLights, ref NetNode.Flags flags)
        {
            if (trafficLights != BuildingInfo.TrafficLights.Default)
            {
                if (trafficLights != BuildingInfo.TrafficLights.ForceOn)
                {
                    if (trafficLights == BuildingInfo.TrafficLights.ForceOff)
                    {
                        flags = ((flags & ~NetNode.Flags.TrafficLights) | NetNode.Flags.CustomTrafficLights);
                    }
                }
                else
                {
                    flags |= (NetNode.Flags.TrafficLights | NetNode.Flags.CustomTrafficLights);
                }
            }
            else
            {
                flags &= (NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Original | NetNode.Flags.Disabled | NetNode.Flags.End | NetNode.Flags.Middle | NetNode.Flags.Bend | NetNode.Flags.Junction | NetNode.Flags.Moveable | NetNode.Flags.Untouchable | NetNode.Flags.Outside | NetNode.Flags.Temporary | NetNode.Flags.Double | NetNode.Flags.Fixed | NetNode.Flags.OnGround | NetNode.Flags.Ambiguous | NetNode.Flags.Water | NetNode.Flags.Sewage | NetNode.Flags.ForbidLaneConnection | NetNode.Flags.Underground | NetNode.Flags.Transition | NetNode.Flags.LevelCrossing | NetNode.Flags.OneWayOut | NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn | NetNode.Flags.Heating | NetNode.Flags.Electricity | NetNode.Flags.Collapsed | NetNode.Flags.DisableOnlyMiddle | NetNode.Flags.AsymForward | NetNode.Flags.AsymBackward);
            }
        }

        private static bool FindConnectNode(FastList<ushort> buffer, Vector3 pos, NetInfo info2, out NetTool.ControlPoint point)
        {
            point = default(NetTool.ControlPoint);
            NetManager instance = Singleton<NetManager>.instance;
            ItemClass.Service service = info2.m_class.m_service;
            ItemClass.SubService subService = info2.m_class.m_subService;
            ItemClass.Layer layer = info2.m_class.m_layer;
            ItemClass.Service service2 = ItemClass.Service.None;
            ItemClass.SubService subService2 = ItemClass.SubService.None;
            ItemClass.Layer layer2 = ItemClass.Layer.Default;
            if (info2.m_intersectClass != null)
            {
                service2 = info2.m_intersectClass.m_service;
                subService2 = info2.m_intersectClass.m_subService;
                layer2 = info2.m_intersectClass.m_layer;
            }
            if (info2.m_netAI.SupportUnderground() || info2.m_netAI.IsUnderground())
            {
                layer |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
                layer2 |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
            }
            for (int i = 0; i < buffer.m_size; i++)
            {
                ushort num = buffer.m_buffer[i];
                if (Vector3.SqrMagnitude(pos - instance.m_nodes.m_buffer[(int)num].m_position) < 0.001f)
                {
                    NetInfo info3 = instance.m_nodes.m_buffer[(int)num].Info;
                    ItemClass connectionClass = info3.GetConnectionClass();
                    if (((service == ItemClass.Service.None || connectionClass.m_service == service) && (subService == ItemClass.SubService.None || connectionClass.m_subService == subService) && (layer == ItemClass.Layer.None || (connectionClass.m_layer & layer) != ItemClass.Layer.None)) || (info3.m_intersectClass != null && (service == ItemClass.Service.None || info3.m_intersectClass.m_service == service) && (subService == ItemClass.SubService.None || info3.m_intersectClass.m_subService == subService) && (layer == ItemClass.Layer.None || (info3.m_intersectClass.m_layer & layer) != ItemClass.Layer.None)) || (connectionClass.m_service == service2 && (subService2 == ItemClass.SubService.None || connectionClass.m_subService == subService2) && (layer2 == ItemClass.Layer.None || (connectionClass.m_layer & layer2) != ItemClass.Layer.None)))
                    {
                        point.m_position = instance.m_nodes.m_buffer[(int)num].m_position;
                        point.m_direction = Vector3.zero;
                        point.m_node = num;
                        point.m_segment = 0;
                        if (info3.m_netAI.IsUnderground())
                        {
                            point.m_elevation = (float)(-(float)instance.m_nodes.m_buffer[(int)num].m_elevation);
                        }
                        else
                        {
                            point.m_elevation = (float)instance.m_nodes.m_buffer[(int)num].m_elevation;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool RequireFixedHeight(BuildingInfo buildingInfo, NetInfo info2, Vector3 pos)
        {
            if (info2.m_useFixedHeight)
            {
                return true;
            }
            ItemClass.Service service = info2.m_class.m_service;
            ItemClass.SubService subService = info2.m_class.m_subService;
            ItemClass.Layer layer = info2.m_class.m_layer;
            ItemClass.Service service2 = ItemClass.Service.None;
            ItemClass.SubService subService2 = ItemClass.SubService.None;
            ItemClass.Layer layer2 = ItemClass.Layer.Default;
            if (info2.m_intersectClass != null)
            {
                service2 = info2.m_intersectClass.m_service;
                subService2 = info2.m_intersectClass.m_subService;
                layer2 = info2.m_intersectClass.m_layer;
            }
            if (info2.m_netAI.SupportUnderground() || info2.m_netAI.IsUnderground())
            {
                layer |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
                layer2 |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
            }
            for (int i = 0; i < buildingInfo.m_paths.Length; i++)
            {
                BuildingInfo.PathInfo pathInfo = buildingInfo.m_paths[i];
                if (pathInfo.m_finalNetInfo != null && pathInfo.m_finalNetInfo.m_useFixedHeight && pathInfo.m_nodes != null && pathInfo.m_nodes.Length != 0)
                {
                    for (int j = 0; j < pathInfo.m_nodes.Length; j++)
                    {
                        if (Vector3.SqrMagnitude(pos - pathInfo.m_nodes[j]) < 0.001f)
                        {
                            NetInfo finalNetInfo = pathInfo.m_finalNetInfo;
                            ItemClass connectionClass = finalNetInfo.GetConnectionClass();
                            if (((service == ItemClass.Service.None || connectionClass.m_service == service) && (subService == ItemClass.SubService.None || connectionClass.m_subService == subService) && (layer == ItemClass.Layer.None || (connectionClass.m_layer & layer) != ItemClass.Layer.None)) || (finalNetInfo.m_intersectClass != null && (service == ItemClass.Service.None || finalNetInfo.m_intersectClass.m_service == service) && (subService == ItemClass.SubService.None || finalNetInfo.m_intersectClass.m_subService == subService) && (layer == ItemClass.Layer.None || (finalNetInfo.m_intersectClass.m_layer & layer) != ItemClass.Layer.None)) || (finalNetInfo.m_netAI.CanIntersect(info2) && connectionClass.m_service == service2 && (subService2 == ItemClass.SubService.None || connectionClass.m_subService == subService2) && (layer2 == ItemClass.Layer.None || (connectionClass.m_layer & layer2) != ItemClass.Layer.None)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
