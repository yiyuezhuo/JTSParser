using System;
using YYZ.PathFinding;
using YYZ.PathFinding2;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YYZ.JTS.AI
{
    public class BlockedGraph<T> : PathFinding.IGraphEnumerable<T>
    {
        public IGraphEnumerable<T> Graph;
        public HashSet<T> BlockedSet;
        public IEnumerable<T> Neighbors(T hex)
        {
            if(!BlockedSet.Contains(hex))
            {
                foreach(var nei in Graph.Neighbors(hex))
                {
                    if(!BlockedSet.Contains(nei))
                        yield return nei;
                }
            }
        }

        public float MoveCost(T src, T dst) => Graph.MoveCost(src, dst);
        public float EstimateCost(T src, T dst) => Graph.EstimateCost(src, dst);
        public IEnumerable<T> Nodes() => Graph.Nodes();
    }

    public class SoftBlockGraph<T> : PathFinding.IGraphEnumerable<T>
    {
        public IGraphEnumerable<T> Graph;
        public Dictionary<T, float> ResistanceMap;
        public float ExtraCostCoef = 1f / 600; // 600 men/resistance => x2 cost, 1200 men => x3 cost, ...
        public IEnumerable<T> Neighbors(T pos) => Graph.Neighbors(pos);
        public float MoveCost(T src, T dst) => Graph.MoveCost(src, dst) * (1 + ResistanceMap.GetValueOrDefault(src) * ExtraCostCoef);
        public float EstimateCost(T src, T dst) => Graph.EstimateCost(src, dst);
        public IEnumerable<T> Nodes() => Graph.Nodes();
    }

}