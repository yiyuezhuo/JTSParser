using System.Collections.Generic;
using System.Linq;
using YYZ.PathFinding;
using System;


namespace YYZ.JTS.AI
{
    public class DistanceGraph: IGraphEnumerable<Hex>
    {
        public HexNetwork Network;
        public DistanceSystem Distance;

        // bool HasEdge(EdgeLayer layer, int i, int j, HexDirection direction) => layer.Defined && layer.Data[i, j].ByDirection(direction);

        public IEnumerable<Hex> Neighbors(Hex pos)
        {
            foreach((var nei, var edge) in pos.EdgeMap)
            {
                if(edge.HasRoad() || Distance.BaseCost(nei.Terrain) > 0) // TODO: Handle Cost 0 edge block in some level
                    yield return nei;
            }
        }

        public bool IsIsolated(Hex hex)
        {
            return Distance.BaseCost(hex.Terrain) == 0 && (hex.EdgeMap.Count == 0 || !hex.EdgeMap.Values.Any(edge => edge.HasRoad()));
        }

        public float MoveCost(Hex src, Hex dst)
        {
            // dst is assumed to be in the EdgeMap
            var edge = src.EdgeMap[dst];

            var roads = Distance.RoadCostMap.Where(KV => edge.ContainsRoad(KV.Key));
            if(roads.Count() > 0) // use road
                return roads.Select(KV => KV.Value).Min();

            var baseCost = Distance.BaseCostMap[dst.Terrain];
            
            var rivers = Distance.RiverCostMap.Where(KV => edge.ContainsRiver(KV.Key));
            var riversCost = rivers.Count() == 0 ? 0 : rivers.Select(KV => KV.Value).Max();

            return baseCost + riversCost;
        }

        public float EstimateCost(Hex src, Hex dst)
        {
            // TODO: even offset?
            var dI = src.I - dst.I;
            var dJ = src.J - dst.J; // Based on Road Cost
            return MathF.Sqrt(dI * dI + dJ * dJ); // TODO: Use Hex Distance (derived from Cube representation) instead of Euclidian distance?
        }

        public override string ToString()
        {
            return $"DistanceGraph({Network}, {Distance})";
        }

        public IEnumerable<Hex> Nodes() => Network.Nodes();

        public SparseProxyGraph GetSparseProxyGraph(List<Hex> selectedHexes)
        {
            var hexSets = selectedHexes.ToHashSet();
            var originMap = selectedHexes.ToDictionary(h =>h, h => new ProxyHex(){X = h.X, Y=h.Y});
            foreach(var src in selectedHexes)
            {
                var srcProxy = originMap[src];
                foreach(var dst in selectedHexes)
                {
                    if(src == dst)
                        continue;
                    var dstProxy = originMap[dst];

                    var cost = PathFinding.PathFinding<Hex>.AStar2(this, src, dst, out var path);
                    var cache = new ProxyHex.Cache(){Path=path, Cost=cost};
                    srcProxy.DenseCacheMap[dstProxy] = cache;
                    if(path.All(n => n == src || n == dst || !hexSets.Contains(n)))
                    {
                        srcProxy.SparseCacheMap[dstProxy] = cache;
                    }
                }
            }

            return new SparseProxyGraph(){OriginMap=originMap};
        }
    }

    public class ProxyHex
    {
        public int X;
        public int Y;
        public int I{get=>Y;}
        public int J{get=>X;}
        
        public class Cache
        {
            public List<Hex> Path;
            public float Cost;
            public override string ToString() => $"Cache({Cost}, [{Path.Count}])";
        }

        public Dictionary<ProxyHex, Cache> SparseCacheMap = new(); // edges containing other ProxyHex removed
        public Dictionary<ProxyHex, Cache> DenseCacheMap = new();
        public override string ToString()
        {
            return $"ProxyHex(IJ=({I},{J},XY=({X},{Y}),[{SparseCacheMap.Count}, {DenseCacheMap.Count}]))";
        }
    }

    public class SparseProxyGraph: IGraph<ProxyHex>
    {
        public Dictionary<Hex, ProxyHex> OriginMap;
        public IEnumerable<ProxyHex> Nodes() => OriginMap.Values;

        public IEnumerable<ProxyHex> Neighbors(ProxyHex hex) => hex.SparseCacheMap.Keys;
        public float MoveCost(ProxyHex src, ProxyHex dst) => src.SparseCacheMap[dst].Cost;
        public float EstimateCost(ProxyHex src, ProxyHex dst)
        {
            var dx = src.X - dst.X;
            var dy = src.Y - dst.Y;
            return MathF.Sqrt(dx * dx + dy + dy);
        }
        public override string ToString()
        {
            return $"SparseProxyGraph({OriginMap.Count})";
        }
    }

    public class MegaHex
    {

    }

    public class GraphWrapper
    {
        public DistanceSystem Distance;
        public DistanceGraph DynamicGraph;
        // public FrozenGraph<Hex> StaticGraph;
        public IPathFinder<Hex> StaticPathFinder;

        public static GraphWrapper Create(HexNetwork network, string parameterDataStr, string movementCostName)
        {
            var distance = new DistanceSystem(){Name=movementCostName};
            var param = ParameterData.Parse(parameterDataStr);
            distance.Extract(network.TerrainSystem, param.Data[movementCostName]);
            var dynamicGraph = new DistanceGraph(){Network=network, Distance=distance};

            /*
            var staticGraph = FrozenGraph<Hex>.GetGraph(DynamicGraph, hex => (hex.X, hex.Y));
            StaticPathFinder = staticGraph.GetPathFinder();
            */
            var staticPathFinder = FrozenGraph2D<Hex>.GetPathFinder(dynamicGraph, hex => (hex.X, hex.Y));
            return new()
            {
                Distance=distance,
                DynamicGraph=dynamicGraph,
                StaticPathFinder=staticPathFinder
            };
        }
    }

}