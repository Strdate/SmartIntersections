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

            HashSet<ConnectionPoint> usedPoints = new HashSet<ConnectionPoint>();
            HashSet<ushort> usedNodes = new HashSet<ushort>();

            List<ConnectionPair> connectionPairs = new List<ConnectionPair>();

            /* For each pair of nodes we make an attempt to connect them */
            foreach (ushort node1 in filteredCreatedNodes)
            {
                foreach(ConnectionPoint cpoint in borderNodes)
                {
                    if(RankConnection(node1,cpoint, out int rank))
                    {
                        connectionPairs.Add(new ConnectionPair(node1, cpoint, rank));
                    }
                }
            }

            connectionPairs.Sort((a, b) => -a.rank.CompareTo(b.rank));

            foreach(var pair in connectionPairs) {

                if(!usedPoints.Contains(pair.point2) && !usedNodes.Contains(pair.node1)) {

                    ushort segment1 = NetUtil.GetFirstSegment(NetUtil.Node(pair.node1));
                    NetSegment netSegment1 = NetUtil.Segment(segment1);
                    Connect(pair.node1, pair.point2.Node, -netSegment1.GetDirection(pair.node1), pair.point2.Direction, pair.point2.NetInfo, !pair.point2.DirectionBool);

                    usedNodes.Add(pair.node1);
                    usedPoints.Add(pair.point2);
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

        private bool RankConnection(ushort node1, ConnectionPoint cpoint, out int rank)
        {
            NetNode netNode1 = NetUtil.Node(node1);
            NetNode netNode2 = NetUtil.Node(cpoint.Node);

            Vector3 differenceVector = netNode2.m_position - netNode1.m_position;

            //Debug.Log("TryConnect: " + node1 + ", " + cpoint.Node + ", dist: " + differenceVector.magnitude);

            rank = 0;

            // 1) Check max distance
            if (differenceVector.magnitude > MAX_NODE_DISTANCE)
                return false;

            ushort segment1 = NetUtil.GetFirstSegment(netNode1);
            //ushort segment2 = NetAccess.GetFirstSegment(netNode2);
            NetSegment netSegment1 = NetUtil.Segment(segment1);
            //NetSegment netSegment2 = NetAccess.GetSegment(segment2);

            bool quays = netSegment1.Info.m_netAI is QuayAI && cpoint.NetInfo.m_netAI is QuayAI;

            // 2) Check if both segments are roads
            if (!((netSegment1.Info.m_hasForwardVehicleLanes || netSegment1.Info.m_hasBackwardVehicleLanes) && (cpoint.NetInfo.m_hasForwardVehicleLanes || cpoint.NetInfo.m_hasBackwardVehicleLanes))
                && !quays)
            {
                //Debug.Log("Not roads!");
                return false;
            }
            
            // 3) Check max angle (if segments are too close we skip this as it won't give good data)
            Vector3 direction1 = netSegment1.GetDirection(node1).normalized;
            Vector3 direction2 = cpoint.Direction.normalized;
            float angle1 = Vector3.Angle(direction1, -differenceVector);
            float angle2 = Vector3.Angle(direction2, -differenceVector);

            float differenceDot = Vector2.Dot(direction1.xz(), direction2.xz());
            float shearDot = Mathf.Abs(Vector2.Dot(direction1.xz(), differenceVector.xz().normalized) * Vector2.Dot(direction2.xz(), differenceVector.xz().normalized));
            float weight = differenceDot * shearDot;

            if (differenceVector.magnitude > MIN_SEGMENT_LENGTH)
            {
                if (angle1 > MAX_ANGLE || angle2 > MAX_ANGLE)
                {
                    //Debug.Log("Angle too big: " + angle1 + " " + angle2);
                    return false;
                }
            }

            // 4) Check if directions of one-way roads match (if so connect)
            if ( NetUtil.IsOneWay(netSegment1.Info) && NetUtil.IsOneWay(cpoint.NetInfo) )
            {
                if (SegmentDirectionBool(netSegment1, node1) ^ cpoint.DirectionBool)
                {
                    if(netSegment1.Info == cpoint.NetInfo) {
                        rank = (int)(weight * 2000);
                    } else {
                        rank = (int)(weight * 1000);
                    }
                    return true;
                }
                else return false; // We won't connect one-way roads whose directions don't match
            }

            // 5) We will favor roads with same NetIfno (if so connect)
            if(netSegment1.Info == cpoint.NetInfo)
            {
                rank = (int)(weight * 1000);
                return true;
            }

            if(quays) {
                return false;
            }
            
            // 6) Lastly we set smaller max distance and angle and try again
            if(differenceVector.magnitude < (MAX_NODE_DISTANCE*3/4) && angle1 < MAX_ANGLE/2 && angle2 < MAX_ANGLE / 2)
            {
                rank = (int)(weight * 500);
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

    public class ConnectionPair
    {
        public int rank;

        public ushort node1;
        public ConnectionPoint point2;

        public ConnectionPair(ushort node1, ConnectionPoint point2, int rank)
        {
            this.node1 = node1;
            this.point2 = point2;
            this.rank = rank;
        }
    }
}
