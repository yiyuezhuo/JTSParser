namespace YYZ.JTS
{
    using System.Collections.Generic;
    using System.Linq;
    // using YYZ.PathFinding;
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
        public TerrainSystem TerrainSystem;

        // public MapFile MapFile;

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

            return new HexNetwork(){Width=map.Width, Height=map.Height, TerrainSystem=map.CurrentTerrainSystem, HexMat=hexMat};
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
}