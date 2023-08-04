// PathFinding2 will focus on performance rather than easy of use

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using YYZ.PathFinding;

namespace YYZ.PathFinding2
{
    public class Edge2D
    {
        public int Target;
        public float Cost;
    }

    public class Node2D
    {
        // public int Id;
        public float X;
        public float Y;
        public Edge2D[] Edges;
    }

    public class Graph2D
    {
        public Node2D[] Nodes;
        public float EstimateCostCoef = 1f;

        public float EstimateCost(int _src, int _dst)
        {
            var src = Nodes[_src];
            var dst = Nodes[_dst];

            var dx = src.X - dst.X;
            var dy = src.Y - dst.Y;
            return MathF.Sqrt(dx*dx + dy*dy) * EstimateCostCoef;
        }

        static List<int> ReconstructPath(int[] cameFrom, int dst)
        {
            var idx = dst;
            var ret = new List<int>();
            while(idx != -1)
            {
                ret.Add(idx);
                idx = cameFrom[idx];
            }
            return  ret;
        }

        public (float, List<int>) AStar(int src, int dst) // return (cost, waypoints)
        {
            var openSet = new HashSet<int>();

            // TODO: Is it better than sparse representation of Dictionary? A lot of hash can be avoided though
            var cameFrom = new int[Nodes.Length]; // -1
            var gScore = new float[Nodes.Length]; // float.PositiveInfinity
            var fScore = new float[Nodes.Length]; // float.PositiveInfinity

            for(var i=0; i<Nodes.Length; i++)
            {
                cameFrom[i] = -1;
                gScore[i] = float.PositiveInfinity;
                fScore[i] = float.PositiveInfinity;
            }

            gScore[src] = 0;
            fScore[src] = EstimateCost(src, dst);

            while(openSet.Count > 0)
            {
                int current = -1;
                float lowestFScore = float.PositiveInfinity;

                foreach(var idx in openSet)
                {
                    var test = fScore[idx];
                    if(test < lowestFScore)
                    {
                        current = idx;
                        lowestFScore = test;
                    }
                }

                if(current == dst)
                {
                    return (gScore[dst], ReconstructPath(cameFrom, dst));
                }

                openSet.Remove(current);
                var currentNode = Nodes[current];
                foreach(var edge in currentNode.Edges)
                {
                    var tentativeGScore = gScore[current] + edge.Cost;
                    var neighbor = edge.Target;
                    if(tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + EstimateCost(neighbor, dst);
                        
                        openSet.Add(neighbor);
                    }
                }
            }

            return (-1, new());
        }

        public class Arrow
        {
            public int Prev;
            public float Cost;
        }

        public Dictionary<int, Arrow> Dijkstra(IEnumerable<int> srcIter, float budget) // return: nodeToArrow
        {
            var nodeToArrow = new Dictionary<int, Arrow>();

            var openSet = new HashSet<int>();
            var closedMask = new bool[Nodes.Length];
            var costArr = new float[Nodes.Length];

            foreach(var closed in srcIter)
            {
                closedMask[closed] = true;
                nodeToArrow[closed] = new(){Prev=-1};
            }

            foreach(var closed in srcIter)
                foreach(var nei in Nodes[closed].Edges)
                    if(!closedMask[nei.Target])
                        openSet.Add(nei.Target);

            while(openSet.Count > 0)
            {
                var picked = -1;
                var pickedNei = -1;
                float pickedCost = float.PositiveInfinity;

                foreach(var open in openSet)
                {
                    var openNode = Nodes[open];
                    foreach(var edge in openNode.Edges)
                    {
                        if(closedMask[edge.Target])
                        {
                            var cost = edge.Cost + costArr[edge.Target];

                            if(cost < pickedCost)
                            {
                                picked = open;
                                pickedNei = edge.Target;
                                pickedCost = cost;
                            }
                        }
                    }
                }

                nodeToArrow[picked] = new Arrow(){Cost = pickedCost, Prev=pickedNei};
                costArr[picked] = pickedCost;

                openSet.Remove(picked);
                closedMask[picked] = true;

                // Negative value is allowed, but it will not be allowed to "propagate".
                if(budget - pickedCost <= 0)
                    continue; 

                foreach(var edge in Nodes[picked].Edges)
                    if(!closedMask[edge.Target])
                        openSet.Add(edge.Target);
            }

            return nodeToArrow;
        }

        // public EarlyStopDijkstra // support Predicate
    }
}