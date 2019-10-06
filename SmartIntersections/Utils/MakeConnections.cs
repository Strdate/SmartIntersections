using SmartIntersections.SharedEnvironment;
using SmartIntersections.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartIntersections.Utils
{
    /* This class tries to reconnect the roads */

    public class MakeConnections
    {
        public static readonly float MAX_NODE_DISTANCE = 150;
        public static readonly float MIN_SEGMENT_LENGTH = 35;
        public static readonly float MAX_ANGLE = 45; // in deg

        private WrappersDictionary _networkDictionary;
        private ActionGroup _actionGroup;

        public MakeConnections(HashSet<ConnectionPoint> borderNodes, FastList<ushort> createdNodes, WrappersDictionary dictionary, ActionGroup actionGroup)
        {
            if (!UIWindow.instance.m_connectRoadsCheckBox.isChecked)
                return;

            if (borderNodes == null)
                return;

            _networkDictionary = dictionary;
            _actionGroup = actionGroup;

            HashSet<ushort> filteredCreatedNodes = FilterCreatedNodes(createdNodes);
            // (Border nodes are already filtered)

            HashSet<ConnectionPoint> connectedPrimaryPoints = new HashSet<ConnectionPoint>();

            /* For each pair of nodes we make an attempt to connect them */
            foreach (ushort node1 in filteredCreatedNodes)
            {
                foreach(ConnectionPoint cpoint in borderNodes)
                {
                    if (connectedPrimaryPoints.Contains(cpoint))
                        continue;

                    if(TryConnect(node1,cpoint))
                    {
                        connectedPrimaryPoints.Add(cpoint);
                        break;
                    }
                }
            }
        }

        /* We keep only nodes with exactly one segment connected (dead ends). */
        private static HashSet<ushort> FilterCreatedNodes(FastList<ushort> createdNodes)
        {
            HashSet<ushort> filteredNodes = new HashSet<ushort>();
            foreach(ushort node in createdNodes)
            {
                if (NetUtil.Node(node).CountSegments() == 1)
                    filteredNodes.Add(node);
            }
            return filteredNodes;
        }

        private bool TryConnect(ushort node1, ConnectionPoint cpoint)
        {
            NetNode netNode1 = NetUtil.Node(node1);
            NetNode netNode2 = NetUtil.Node(cpoint.Node);

            Vector3 differenceVector = netNode2.m_position - netNode1.m_position;

            //Debug.Log("TryConnect: " + node1 + ", " + cpoint.Node + ", dist: " + differenceVector.magnitude);

            // 1) Check max distance
            if (differenceVector.magnitude > MAX_NODE_DISTANCE)
                return false;

            ushort segment1 = NetUtil.GetFirstSegment(netNode1);
            //ushort segment2 = NetAccess.GetFirstSegment(netNode2);
            NetSegment netSegment1 = NetUtil.Segment(segment1);
            //NetSegment netSegment2 = NetAccess.GetSegment(segment2);

            // 2) Check if both segments are roads
            if(!((netSegment1.Info.m_hasForwardVehicleLanes || netSegment1.Info.m_hasBackwardVehicleLanes) && (cpoint.NetInfo.m_hasForwardVehicleLanes || cpoint.NetInfo.m_hasBackwardVehicleLanes)))
            {
                //Debug.Log("Not roads!");
                return false;
            }

            // 3) Check max angle (if segments are too close we skip this as it won't give good data)
            Vector3 direction1 = netSegment1.GetDirection(node1);
            Vector3 direction2 = cpoint.Direction;
            float angle1 = Vector3.Angle(direction1, -differenceVector);
            float angle2 = Vector3.Angle(direction2, -differenceVector);

            if (differenceVector.magnitude > MIN_SEGMENT_LENGTH)
            {
                if (angle1 > MAX_ANGLE || angle2 > MAX_ANGLE)
                {
                    //Debug.Log("Angle too big: " + angle1 + " " + angle2);
                    return false;
                }
            }

            // 4) Check if directions of one-way roads match (if so connect)
            if ( NetUtil.IsOneWay(netSegment1.Info) && cpoint.NetInfo )
            {
                if (SegmentDirectionBool(netSegment1, node1) ^ cpoint.DirectionBool)
                {
                    Connect(node1, cpoint.Node, - netSegment1.GetDirection(node1), cpoint.Direction, cpoint.NetInfo, !cpoint.DirectionBool);
                    return true;
                }
                else return false; // We won't connect one-way roads whose directions don't match
            }

            // 5) We will favor roads with same NetIfno (if so connect)
            if(netSegment1.Info == cpoint.NetInfo)
            {
                Connect(node1, cpoint.Node, -netSegment1.GetDirection(node1), cpoint.Direction, cpoint.NetInfo, !cpoint.DirectionBool);
                return true;
            }
            
            // 6) Lastly we set smaller max distance and angle and try again
            if(differenceVector.magnitude < (MAX_NODE_DISTANCE*3/4) && angle1 < MAX_ANGLE/2 && angle2 < MAX_ANGLE / 2)
            {
                Connect(node1, cpoint.Node, -netSegment1.GetDirection(node1), cpoint.Direction, cpoint.NetInfo, !cpoint.DirectionBool);
                return true;
            }

            return false;
        }

        private void Connect(ushort node1, ushort node2, Vector3 startDir, Vector3 endDir, NetInfo Info, bool invert = false)
        {
            //Debug.Log("Connectiong nodes " + node1 + " and " + node2);

            NetNode netNode1 = NetUtil.Node(node1);
            NetNode netNode2 = NetUtil.Node(node2);

            if ((netNode1.m_position - netNode2.m_position).magnitude < MIN_SEGMENT_LENGTH )
            {
                RepairShortSegment(ref endDir, ref node2);
            }

            //NetAccess.CreateSegment(node1,node2,(netNode2.m_position-netNode1.m_position).normalized, (netNode1.m_position - netNode2.m_position).normalized, info, invert);
            try
            {
                WrappedSegment segmentW = new WrappedSegment();
                segmentW.StartNode = _networkDictionary.RegisterNode(node1);
                segmentW.EndNode = _networkDictionary.RegisterNode(node2);
                segmentW.StartDirection = startDir;
                segmentW.EndDirection = endDir;
                segmentW.NetInfo = Info;
                segmentW.Invert = invert;
                _actionGroup.Actions.Add(segmentW);

                segmentW.Create();
                //NetUtil.CreateSegment(node1, node2, startDir, endDir, Info, invert);
            }
            catch(Exception e)
            {
                Debug.LogWarning(e);
            }
        }

        /* If node distance is too short, we travel one segment up from the border node and set the new node as the one to connect to */
        private void RepairShortSegment(ref Vector3 direction, ref ushort node)
        {
            //Debug.Log("Repairing short segment...");

            NetNode netNode = NetUtil.Node(node);

            // If there is more than one segment we cannot safely delete it (we don't even know from which segment we should pick)
            if (netNode.CountSegments() != 1)
                return;

            ushort segmentId = NetUtil.GetFirstSegment(netNode);
            NetSegment netSegment = NetUtil.Segment(segmentId);

            WrappedNode nodeW = _networkDictionary.RegisterNode(node);

            if (node == netSegment.m_startNode)
            {
                direction = netSegment.m_endDirection;
                node = netSegment.m_endNode;
            } else
            {
                direction = netSegment.m_startDirection;
                node = netSegment.m_startNode;
            }

            WrappedSegment segmentW = _networkDictionary.RegisterSegment(segmentId);
            _actionGroup.Actions.Add(segmentW);
            _actionGroup.Actions.Add(nodeW);
            segmentW.Release();
            nodeW.Release();
            segmentW.IsBuildAction = false;
            nodeW.IsBuildAction = false;

            //NetUtil.ReleaseSegment(segmentId, true);        
        }

        private static bool SegmentDirectionBool(NetSegment segment, ushort node)
        {
            return (segment.m_startNode == node) ^ ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);
        }

    }

    /* As the segments underneath the intersection get deleted, we need some place where to save their direction for when we reconnect the nodes */
    public class ConnectionPoint
    {
        public ushort Node;
        public Vector3 Direction;
        public NetInfo NetInfo;
        public bool DirectionBool;

        public ConnectionPoint(ushort node, Vector3 direction, NetInfo netInfo, bool directionBool)
        {
            Node = node;
            Direction = direction;
            NetInfo = netInfo;
            DirectionBool = directionBool;
        }

        public override int GetHashCode()
        {
            var hashCode = 1587965759;
            hashCode = hashCode * -1521134295 + Node.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(Direction);
            return hashCode;
        }
    }
}
