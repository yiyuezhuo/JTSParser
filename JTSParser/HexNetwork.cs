namespace YYZ.JTS
{
    using System.Collections.Generic;
    using System.Linq;
    using YYZ.PathFinding;
    using System;

    public class HexEdge
    {
        public HashSet<EdgeTerrain> RiverSet = new();
        public HashSet<EdgeTerrain> RoadSet = new();

        public bool ContainsRiver(EdgeTerrain e) => RiverSet.Contains(e);
        public bool ContainsRoad(EdgeTerrain e) => RoadSet.Contains(e);
        public void AddRiver(EdgeTerrain e) => RiverSet.Add(e);
        public void AddRoad(EdgeTerrain e) => RoadSet.Add(e);

        public bool HasRoad() => RoadSet.Count > 0;
        public bool HasRiver() => RiverSet.Count > 0;
        
        public override string ToString()
        {
            var river_s = string.Join(",", RiverSet.Select(e => e.Name));
            var road_s = string.Join(",", RoadSet.Select(e => e.Name));
            return $"HexEdge({road_s}, {river_s})";
        }
    }

    public class Hex
    {
        public int I;
        public int J;
        public int X{get => J;}
        public int Y{get => I;}
        public HexTerrain Terrain;
        public int Height;
        public Dictionary<Hex, HexEdge> EdgeMap = new();

        public override string ToString()
        {
            var es = string.Join(",", EdgeMap.Select(KV => $"(I={KV.Key.I}, J={KV.Key.J}): {KV.Value}"));
            return $"Hex(I={I}, J={J}, X={X}, Y={Y}, {Terrain}, {Height}, {es})";
        }
    }

    public class HexNetwork // "Advanced" representation for map
    {
        // (i, j) offsets
        static HexDirection[] directions = new []{HexDirection.Top, HexDirection.TopRight, HexDirection.BottomRight, HexDirection.Bottom, HexDirection.BottomLeft, HexDirection.TopLeft};
        static int[][] evenNeighborOffsets = new int[][]{new int[]{-1, 0}, new int[]{0, 1}, new int[]{1, 1}, new int[]{1, 0}, new int[]{1, -1}, new int[]{0, -1}}; // Follwing HexDirection order
        static int[][] oddNeighborOffsets = new int[][]{new int[]{-1, 0}, new int[]{-1, 1}, new int[]{0, 1}, new int[]{1, 0}, new int[]{0, -1}, new int[]{-1, -1}};

        public Hex[,] HexMat;
        public int Height;
        public int Width;

        public Hex GetHex(Hex src, HexDirection direction) // TODO: de-dupcalite
        {
            var offsets = src.J % 2 == 0 ? evenNeighborOffsets : oddNeighborOffsets;
            var e = (int)direction;
            var offset = offsets[e];

            var ii = src.I + offset[0];
            var jj = src.J + offset[1];
            if(ii >= 0 && ii < Height && jj >= 0 && jj < Width)
                return HexMat[ii, jj];
            return null;
        }

        public static HexNetwork FromMapFile(MapFile map)
        {
            var hexMat = new Hex[map.Height, map.Width];
            for(var i=0; i<map.Height; i++)
                for(var j=0; j<map.Width; j++)
                {
                    hexMat[i, j] = new Hex()
                    {
                        I=i, J=j,
                        Terrain=map.TerrainMap[i, j],
                        Height=map.HeightMap[i, j]
                    };
                }

            for(var i=0; i<map.Height; i++)
                for(var j=0; j<map.Width; j++)
                {
                    var src = hexMat[i, j];
                    var offsets = j % 2 == 0 ? evenNeighborOffsets : oddNeighborOffsets;
                    for(var e=0; e<6; e++)
                    {
                        var offset = offsets[e];
                        var direction = directions[e];

                        var ii = i + offset[0];
                        var jj = j + offset[1];
                        if(ii >= 0 && ii < map.Height && jj >= 0 && jj < map.Width)
                        {
                            var dst = hexMat[ii, jj];
                            var edge = src.EdgeMap[dst] = new HexEdge();

                            // TODO: Avoid Hash overhead which is not necessary? Move it to outer level?
                            foreach(var edgeType in map.CurrentTerrainSystem.Road.Name2Terrain.Values)
                            {
                                var edgeLayer = map.EdgeLayerMap[edgeType];
                                if(edgeLayer.HasEdge(i, j, direction))
                                    edge.AddRoad(edgeType);
                            }

                            foreach(var edgeType in map.CurrentTerrainSystem.River.Name2Terrain.Values)
                            {
                                var edgeLayer = map.EdgeLayerMap[edgeType];
                                if(edgeLayer.HasEdge(i, j, direction))
                                    edge.AddRiver(edgeType);
                            }
                        }
                    }
                }

            return new HexNetwork(){Height=map.Height, Width=map.Width, HexMat=hexMat};
        }

        bool HasEdge(EdgeLayer layer, int i, int j, HexDirection direction) => layer.Defined && layer.Data[i, j].ByDirection(direction);

        public IEnumerable<Hex> Nodes()
        {
            var height = HexMat.GetLength(0);
            var width = HexMat.GetLength(1);
            for(var i=0; i<height; i++)
            {
                for(var j=0; j<width; j++)
                {
                    yield return HexMat[i, j];
                }
            }
        }

        /*
        Generate long sequence of road from edge, for example:
        (0, 1), (1, 2), (2, 3), (3, 4), (3, 5), (3, 6) => [(0, 1), (1, 2), (2, 3)], [(3, 4)], [(3, 5), (3, 6)] 
        */
        public List<List<Hex>> SimplifyRoad(EdgeTerrain T)
        {
            var roadMap = new Dictionary<Hex, List<Hex>>();

            foreach(var src in Nodes())
            {
                foreach(var KV in src.EdgeMap)
                {
                    var dst = KV.Key;
                    var edge = KV.Value;
                    if(edge.ContainsRoad(T))
                    {
                        if(roadMap.TryGetValue(src, out var roads))
                        {
                            roads.Add(dst);
                        }
                        else
                        {
                            roadMap[src] = new List<Hex>{dst};
                        }
                    }
                }
            }

            var stationSet = new HashSet<Hex>();
            var relaySet = new HashSet<Hex>();
            foreach(var KV in roadMap)
            {
                if(KV.Value.Count == 2)
                    relaySet.Add(KV.Key);
                else
                    stationSet.Add(KV.Key);
            }

            var ret = new List<List<Hex>>();
            foreach(var station in stationSet)
            {
                foreach(var firstDst in roadMap[station])
                {
                    var left = station;
                    var right = firstDst;
                    var road = new List<Hex>(){left, right};
                    while(relaySet.Contains(right))
                    {
                        var next = roadMap[right][0] == left ? roadMap[right][1] : roadMap[right][0];
                        road.Add(next);
                        left = right;
                        right = next;
                    }

                    ret.Add(road);
                }
            }
            return ret;
        }


        public override string ToString()
        {
            return $"HexNetwork(Height={HexMat.GetLength(0)}, Width={HexMat.GetLength(1)})";
        }
    }



    public class TerrainSystem
    {
        public HexTerrainSystem Hex;
        public EdgeTerrainSystem Road;
        public EdgeTerrainSystem River;
        public override string ToString() => $"TerrainSystem({Hex.Count}, {Road.Count}, {River.Count})";
    }

    public class DistanceSystem
    {
        // Infantry, Motorized, ...
        public string Name; // Motorized, Tracked, ...
        public Dictionary<HexTerrain, float> BaseCostMap = new();
        public Dictionary<EdgeTerrain, float> RoadCostMap = new();
        public Dictionary<EdgeTerrain, float> RiverCostMap = new();

        public void Extract(TerrainSystem ts, Dictionary<string, string> dict)
        {
            // {{"Blocked", "0"}, {"Clear", "3"}, ...}
            foreach((var terrainName, var costStr) in dict)
            {
                var cost = ParseCost(costStr);
                if(ts.Hex.TryGetValue(terrainName, out var terrain))
                {
                    BaseCostMap[terrain] = cost;
                }
                if(ts.Road.TryGetValue(terrainName, out var road))
                {
                    RoadCostMap[road] = cost;
                }
                if(ts.River.TryGetValue(terrainName, out var river))
                {
                    RiverCostMap[river] = cost;
                }
            }
        }

        public int ParseCost(string s)
        {
            if(s.Contains("MP")) // PZC
                return int.Parse(s.Replace("MP", "").Trim());
            return int.Parse(s);
        }

        public override string ToString() => $"DistanceSystem({Name}, {BaseCostMap.Count}, {RoadCostMap.Count}, {RiverCostMap.Count})";

        public float BaseCost(HexTerrain t) => BaseCostMap[t];
        public float RoadCostCost(EdgeTerrain t) => RoadCostMap[t];
        public float RiverCostCost(EdgeTerrain t) => RiverCostMap[t];
    }
    

    public class DistanceGraph: IGraphEnumerable<Hex>
    {
        public HexNetwork Network;
        public DistanceSystem Distance;

        bool HasEdge(EdgeLayer layer, int i, int j, HexDirection direction) => layer.Defined && layer.Data[i, j].ByDirection(direction);

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
}