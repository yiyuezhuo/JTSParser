// PathFinding2 will focus on performance rather than easy of use

using System;
using System.Collections.Generic;
using YYZ.PathFinding;

namespace YYZ.PathFinding2
{
    public struct Edge2D
    {
        public Node2D Target;
        public float Weight;
    }

    public struct Node2D
    {
        public float X;
        public float Y;
        public Edge2D[] Edges;
    }

    public class Graph2D
    {
        public Node2D[] Nodes;
        public float EstimateCostCoef = 1f;

        public float EstimateCost(Node2D src, Node2D dst)
        {
            var dx = src.X - dst.X;
            var dy = src.Y - dst.Y;
            return MathF.Sqrt(dx*dx + dy*dy) * EstimateCostCoef;
        }

        public void AStar(Node2D src)
        {
            var openSet = new HashSet<int>();
            /*
            var cameFrom = new int[NumNodes];
            var gScore = new int[NumNodes];
            var fScore = new int[NumNodes];

            gScore[src] = 0;
            fScore[src] = 
            */
        }
    }

    public class Graph2DMapped
    {

    }
}