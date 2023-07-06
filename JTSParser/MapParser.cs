
using System.Collections.Generic;
using System;
using System.Linq;

namespace YYZ.JTS.NB
{


    public class EdgeState
    {
        public bool[] RawData = new bool[6];
        public bool ByDirection(HexDirection d) => RawData[(int)d];

        public static EdgeState FromIntString(string s)
        {
            // "124" => {True, True, False, True, False, False}
            var ret = new EdgeState();
            foreach(var c in s)
            {
                switch(c)
                {
                    case '1':
                        ret.RawData[0] = true;
                        break;
                    case '2':
                        ret.RawData[1] = true;
                        break;
                    case '3':
                        ret.RawData[2] = true;
                        break;
                    case '4':
                        ret.RawData[3] = true;
                        break;
                    case '5':
                        ret.RawData[4] = true;
                        break;
                    case '6':
                        ret.RawData[5] = true;
                        break;
                }
            }
            return ret;
        }

        public override string ToString()
        {
            var s = string.Join(",", RawData);
            return $"EdgeState({s})";
        }
    }

    // For road, river, wall, ...
    public class EdgeLayer
    {
        public static Dictionary<char, EdgeState> CodeMap;

        static EdgeLayer() // Static Constructors 
        {
            CodeMap = new(){{' ', new EdgeState()}};
            foreach(var line in StaticData.EdgeCodeString.Split("\n"))
            {
                foreach(var record in line.Split("\t"))
                {
                    if(record.Length == 0)
                        continue;
                    var _d_code = record.Split(",", 2);
                    var d = _d_code[0];
                    var code = _d_code[1];
                    if(code == "")
                        continue;
                    CodeMap[code[0]] = EdgeState.FromIntString(d);
                }
            }
        }

        public bool Defined;
        public EdgeState[,] Data;

        public bool HasEdge(int i, int j, HexDirection direction) => Defined && Data[i, j].ByDirection(direction);

        public override string ToString()
        {
            if(!Defined)
                return $"EdgeLayer(Not Defined)";
            return $"EdgeLayer({Data.GetLength(0)}, {Data.GetLength(1)})";
        }
    }

    public class MapLabel
    {
        public double X;
        public double Y;
        public int Size; // 2 => small, 3 => important
        public int V2;
        public int Color; // 0 => Land (Black), 1 => Sea (Blue)
        public string Name;

        public static MapLabel ParseLine(string s)
        {
            // 38.6538 16.6333 2 1 0 Castillo de San Diego
            var ss = s.Split(" ", 6);
            return new MapLabel()
            {
                X = double.Parse(ss[0]),
                Y = double.Parse(ss[1]),
                Size = int.Parse(ss[2]),
                V2 = int.Parse(ss[3]),
                Color = int.Parse(ss[4]),
                Name = ss[5]
            };
        }

        public override string ToString()
        {
            return $"MapLabel({X}, {Y}, {Name})";
        }
    }

    public class MapFile
    {
        public override string ToString()
        {
            return $"MapFile(Width={Width}, Height={Height}, EdgeLayersN={EdgeLayers.Count}, LabelsN={Labels.Count})";
        }

        static Dictionary<char, TerrainType> TerrainCodeMap = new()
        {
            {'w', TerrainType.Water},
            {'r', TerrainType.Rough},
            {'s', TerrainType.Marsh},
            {'v', TerrainType.Village},
            {'b', TerrainType.Building},
            {'c', TerrainType.Chateau},
            {'o', TerrainType.Orchard},
            {' ', TerrainType.Clear},
            {'e', TerrainType.Field},
            {'f', TerrainType.Forest},
            {'x', TerrainType.Blocked},
            // Civil War Battles
            {'d', TerrainType.Field},
            {'t', TerrainType.Town},
            // Panzer Campaign
            {'p', TerrainType.Town},
            {'m', TerrainType.Rough},
            {'q', TerrainType.City}
            // {'o', TerrainType.Village} TODO: in PZC map Village will be mapped to "Orchard" incorrectly now.
        };

        public int Version; // ?
        public int Width;
        public int Height;
        // -40 40 0 44370 0
        public TerrainType[,] TerrainMap;
        public int[,] HeightMap;

        public List<EdgeLayer> EdgeLayers;
        /*
        Road Layers:
            Path, Road, Pike, Railroad,
        River Layers:
            Stream, Creek
        Other Layers (EX.Wall):
        */

        public List<MapLabel> Labels;

        static int ParseHeight(char c) // '0' => 0, '1' => 1, ..., 'a' => 10, 'b' => 11, ...
        {
            if(c >= '0' && c <= '9')
                return c - '0';
            return c - 'a' + 10;
        }

        public static MapFile Parse(string s)
        {
            var lines = s.Split("\n"); // Trim should not be called here since space is used to represent clear terrain.
            var version = int.Parse(lines[0]);

            var sizeStr = lines[1].Split(" "); // "170 168" (NB/CWB) or "346 160 260938" PZC
            var width = int.Parse(sizeStr[0]);
            var height = int.Parse(sizeStr[1]);

            var terrainMap = new TerrainType[height, width];
            var heightMap = new int[height, width];

            // Skip some unknown data until terrain matrix
            // TODO: Separated parsers or more robust method
            var matOffset = 2;
            while(int.TryParse(lines[matOffset].Split()[0], out _))
            {
                matOffset++;
            }

            for(int i=0; i<height; i++)
                for(int j=0; j<width; j++)
                {
                    var terrainCode = lines[matOffset+i][j];
                    if(!TerrainCodeMap.TryGetValue(terrainCode, out terrainMap[i, j]))
                    {
                        throw new ArgumentException($"Unknown Terrain Code: {terrainCode} in ({i}, {j}), x={j}, y={i}");
                    }
                    terrainMap[i, j] = TerrainCodeMap[terrainCode];
                    var hc = lines[3+i+height][j];
                    heightMap[i, j] = ParseHeight(hc); // TODO: Performance Issue?
                }
            
            var edgeLayers = new List<EdgeLayer>();
            var idx = matOffset + height * 2;
            while(idx < lines.Length)
            {
                var test = lines[idx];
                
                if(test.Trim().Length == 1)
                {
                    idx += 1;
                    var defined = int.Parse(test) == 0 ? false : true;
                    var edgeLayer = new EdgeLayer(){Defined = defined}; 
                    if(defined)
                    {
                        var data = edgeLayer.Data = new EdgeState[height, width];
                        for(var i=0; i<height; i++)
                            for(var j=0; j<width; j++)
                                data[i, j] = EdgeLayer.CodeMap[lines[idx+i][j]];
                        idx += height;
                    }
                    edgeLayers.Add(edgeLayer);
                }
                else{
                    break;
                }
            }

            var labels = new List<MapLabel>();
            while(idx < lines.Length)
            {
                if(lines[idx].Length == 0)
                    break;

                labels.Add(MapLabel.ParseLine(lines[idx]));
                idx += 1;
            }

            return new MapFile()
            {
                Version=version,
                Width=width,
                Height=height,
                TerrainMap=terrainMap,
                HeightMap=heightMap,
                EdgeLayers=edgeLayers,
                Labels=labels
            };
        }

        public EdgeLayer GetEdgeLayer(RoadType t)
        {
            switch(t)
            {
                case RoadType.Path:
                    return EdgeLayers[0];
                case RoadType.Road:
                    return EdgeLayers[1];
                case RoadType.Pike:
                    return EdgeLayers[2];
                case RoadType.Railway:
                    return EdgeLayers[3];
            }
            throw new ArgumentException($"Undefined RoadType {t}");
        }

        public EdgeLayer GetEdgeLayer(RiverType t)
        {
            switch(t)
            {
                case RiverType.Stream:
                    return EdgeLayers[4];
                case RiverType.Creek:
                    return EdgeLayers[5];
            }
            throw new ArgumentException($"Undefined RiverType {t}");
        }
    }

    public class HexEdge
    {
        public bool[] RiverArr = new bool[Enum.GetNames(typeof(RiverType)).Length];
        public bool[] RoadArr = new bool[Enum.GetNames(typeof(RoadType)).Length];

        public bool Contains(RiverType t) => RiverArr[(int)t];
        public bool Contains(RoadType t) => RoadArr[(int)t];
        public bool Set(RiverType t, bool v) => RiverArr[(int)t] = v;
        public bool Set(RoadType t, bool v) => RoadArr[(int)t] = v;

        public override string ToString()
        {
            return $"HexEdge(Stream={Contains(RiverType.Stream)}, Creek={Contains(RiverType.Creek)}, Path={Contains(RoadType.Path)}, Road={Contains(RoadType.Road)})";
        }
    }

    public class Hex
    {
        public int I;
        public int J;
        public int X{get => J;}
        public int Y{get => I;}
        public TerrainType Terrain;
        public int Height;
        public Dictionary<Hex, HexEdge> EdgeMap = new();

        public override string ToString()
        {
            
            var es = string.Join(",", EdgeMap.Select(KV => $"(I={KV.Key.I}, J={KV.Key.J}): {KV.Value}"));
            return $"Hex(I={I}, J={J}, X={X}, Y={Y}, {Terrain}, {Height}, {es})";
            
        }
    }
    

    public class InfantryColumnGraph: YYZ.AI.IGraphEnumerable<Hex>
    {
        public static Dictionary<TerrainType, float> BaseCostMap = new()
        {
            {TerrainType.Clear, 2},
            {TerrainType.Building, 2},
            {TerrainType.Field, 2},
            {TerrainType.Forest, 5},
            {TerrainType.Orchard, 3},
            {TerrainType.Rough, 3},
            {TerrainType.Marsh, 4},
            {TerrainType.Village, 2},
            {TerrainType.Chateau, 2},
            {TerrainType.Water, 0}, // 0 => non-movable
            {TerrainType.Blocked, 0},
            // Civil War Battles
            {TerrainType.Town, 2}, // 1? TODO: Dedicated Parser for CWB
            // Panzer Campaign
            {TerrainType.City, 3} // 8 
        };

        public static Dictionary<RoadType, float> RoadCostMap = new()
        {
            {RoadType.Path, 2},
            {RoadType.Road, 1},
            {RoadType.Pike, 1},
            {RoadType.Railway, 2}
        };

        public static Dictionary<RiverType, float> RiverCostMap = new()
        {
            {RiverType.Creek, 1}, // extra 1
            {RiverType.Stream, 0} // blocked if no bridge
        };
        
        // (i, j) offsets
        static HexDirection[] directions = new []{HexDirection.Top, HexDirection.TopRight, HexDirection.BottomRight, HexDirection.Bottom, HexDirection.BottomLeft, HexDirection.TopLeft};
        static int[][] evenNeighborOffsets = new int[][]{new int[]{-1, 0}, new int[]{0, 1}, new int[]{1, 1}, new int[]{1, 0}, new int[]{1, -1}, new int[]{0, -1}}; // Follwing HexDirection order
        static int[][] oddNeighborOffsets = new int[][]{new int[]{-1, 0}, new int[]{-1, 1}, new int[]{0, 1}, new int[]{1, 0}, new int[]{0, -1}, new int[]{-1, -1}};

        public Hex[,] HexMat;

        bool HasEdge(EdgeLayer layer, int i, int j, HexDirection direction) => layer.Defined && layer.Data[i, j].ByDirection(direction);

        public static InfantryColumnGraph FromMapFile(MapFile map)
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

                            foreach(RiverType el in Enum.GetValues(typeof(RiverType)))
                                edge.Set(el, map.GetEdgeLayer(el).HasEdge(i, j, direction));

                            foreach(RoadType t in Enum.GetValues(typeof(RoadType)))
                                edge.Set(t, map.GetEdgeLayer(t).HasEdge(i, j, direction));
                        }
                    }
                }

            return new InfantryColumnGraph(){HexMat=hexMat};
        }

        public IEnumerable<Hex> Neighbors(Hex pos)
        {
            foreach((var nei, var edge) in pos.EdgeMap)
            {
                if(BaseCostMap[nei.Terrain] > 0 && !edge.Contains(RiverType.Creek))
                    yield return nei;
            }
        }

        public float MoveCost(Hex src, Hex dst)
        {
            // dst is assumed to be in the EdgeMap
            var edge = src.EdgeMap[dst];

            var roads = RoadCostMap.Where(KV => edge.Contains(KV.Key));
            if(roads.Count() > 0) // use road
                return roads.Select(KV => KV.Value).Min();

            var baseCost = BaseCostMap[dst.Terrain];
            
            var rivers = RiverCostMap.Where(KV => edge.Contains(KV.Key));
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
            return $"InfantryColumnGraph(Height={HexMat.GetLength(0)}, Width={HexMat.GetLength(1)})";
        }

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

        public List<List<Hex>> SimplifyRoad(RoadType T)
        {
            var roadMap = new Dictionary<Hex, List<Hex>>();

            foreach(var src in Nodes())
            {
                foreach(var KV in src.EdgeMap)
                {
                    var dst = KV.Key;
                    var edge = KV.Value;
                    if(edge.Contains(T))
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
    }
}