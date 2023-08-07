namespace YYZ.JTS
{
    using YYZ.AI;
    using YYZ.PathFinding;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using YYZ.PathFinding2;

    public class NetworkSegment
    {
        public Hex BeginNode;
        public HashSet<Hex> Nodes = new();
        public int Count{get => Nodes.Count;}
        public HashSet<NetworkSegment> Neighbors = new();
        public Dictionary<NetworkSegment, AStarResult<Hex>> PathFindingResultMap = new();
        public float XMean;
        public float YMean;
        public Hex Center;

        public override string ToString()
        {
            return $"NetworkSegment({XMean}, {YMean}, {Center}, [{Count}], nei:{Neighbors.Count})";
        }
    }

    public class SegmentGraph: IGraphEnumerable<NetworkSegment>
    {
        public Dictionary<Hex, NetworkSegment> SegmentMap;
        public List<NetworkSegment> Segments;

        public IEnumerable<NetworkSegment> Neighbors(NetworkSegment seg) => seg.Neighbors;
        public float MoveCost(NetworkSegment src, NetworkSegment dst) => src.PathFindingResultMap[dst].Cost;
        public float EstimateCost(NetworkSegment src, NetworkSegment dst) => Utils.Distance(src.XMean, src.YMean, dst.XMean, dst.YMean);
        public IEnumerable<NetworkSegment> Nodes() => Segments;
        public override string ToString()
        {
            return $"SegmentGraph(Nodes:[{Segments.Count}], Edges:[{SegmentMap.Count}])";
        }
    }

    public class HexGraphDivider
    {
        // public MapFile Map;
        // public HexNetwork Network;
        // public DistanceSystem Distance;
        public DistanceGraph DynamicGraph;
        // public IPathFinder<Hex> StaticPathFinder;
        public EdgeTerrainSystem RoadSystem;

        public float RoadLevelCoef = 0.4f;

        public int MaxSize = GetMaxSizeByRadius(5); // MaxSize=HexGraphDivider.GetMaxSizeByRadius(3)

        public Dictionary<Hex, NetworkSegment> SegmentMap = new();
        public List<NetworkSegment> Segments = new();

        public static int GetMaxSizeByRadius(int radius)
        {
            // 0 => 1, 1 => 1+6, 2=>1+6+12, 3=>1+6+12+18=37
            // 0 => 1, 1 => 1+(6+0*6), 2=>1+(6+0*6)+(6+2*6)
            // Removed 1, n*6, (6+n*6)*n/2=n^2*3+3n
            // n^2*3+3n+1
            return radius * radius * 3 + 3 * radius + 1;
        }

        public NetworkSegment GetSegment(Hex hex) => SegmentMap[hex];

        public SegmentGraph GetGraph()
        {
            Segment();
            AnalyzeCenter();
            AssignMoveCost();
            return new SegmentGraph(){SegmentMap=SegmentMap, Segments=Segments};
        }


        public void Segment()
        {
            // Collect degree statistics
            var roadDegreeMaps = new Dictionary<Hex, int>[RoadSystem.Names.Count];
            for(var level=0; level<RoadSystem.Count; level++)
                roadDegreeMaps[level] = new();

            foreach(var node in DynamicGraph.Nodes())
            {
                foreach(var edge in node.EdgeMap.Values)
                {
                    for(var level=0; level<RoadSystem.Count; level++)
                    {
                        if(edge.ContainsRoad(RoadSystem.Terrains[level]))
                        {
                            var layer = roadDegreeMaps[level];
                            if(layer.TryGetValue(node, out var deg))
                            {
                                layer[node] = deg + 1;
                            }
                            else
                            {
                                layer[node] = 1;
                            }
                        }
                    }
                }
            }

            // Create areas around road hexes according road level and degree

            for(var level=RoadSystem.Count-1; level >=0; level--)
            {
                var layer = roadDegreeMaps[level];

                foreach(var node in layer.OrderByDescending(KV => KV.Value).Select(KV => KV.Key))
                {
                    if(!SegmentMap.ContainsKey(node))
                    {
                        // var segment = segmentMap[node] = new NetworkSegment(){BeginNode = node, Nodes=new(){node}};
                        FloodingSegment(node);
                    }
                }

                // var nodes = layer.Keys.ToList();
                // nodes.sort
            }

            // Create area for remaining non-blocking nodes
            foreach(var node in DynamicGraph.Nodes())
            {
                if(!SegmentMap.ContainsKey(node) && !DynamicGraph.IsIsolated(node))
                {
                    FloodingSegment(node);
                }
            }
        }

        public void AnalyzeCenter()
        {
            // TODO: Try to use the stationary distribution of Markov Chain? Btw it looks too costly. Here we simply use the heuristic.
            foreach(var segment in SegmentMap.Values)
            {
                var x = 0f;
                var y = 0f;
                foreach(var node in segment.Nodes)
                {
                    x += node.X;
                    y += node.Y;
                }
                segment.XMean = x / segment.Count;
                segment.YMean = y / segment.Count;

                var nodes = segment.Nodes.ToArray(); // Is the iteration order of HashSet ensured to be deterministic? 

                var distances = nodes.Select(h => Utils.Distance(h.X, h.Y, segment.XMean, segment.YMean)).ToArray();
                var minD = distances.Min();
                var maxD = distances.Max();
                var rD = maxD - minD;
                var scores = rD > 0 ? distances.Select(d => 1 - (d - minD)/rD).ToArray() : distances.Select(d => 1f).ToArray();
                
                for(var i=0; i<segment.Count; i++)
                {
                    var node = nodes[i];

                    var realRoadLevel = 0;
                    var roadLevel = 1;
                    foreach(var terrain in RoadSystem.Terrains)
                    {
                        if(node.EdgeMap.Values.Any(e => e.RoadSet.Contains(terrain)))
                            realRoadLevel = roadLevel;
                        roadLevel += 1;
                    }

                    scores[i] += RoadLevelCoef * realRoadLevel;
                    /*
                    if(node.EdgeMap.Values.Any(e => e.HasRoad()))
                    {
                        scores[i] += 0.34f;
                    }
                    */
                }
                // TODO: Consider terrain BaseCost and road level?
                
                var minIdx = Utils.MinIndexBy(scores, x => -x);
                segment.Center = nodes[minIdx];
            }
        }

        public void AssignMoveCost()
        {
            foreach(var src in Segments)
            {
                foreach(var dst in src.Neighbors)
                {
                    if(src == dst)
                        continue;
                    if(!src.PathFindingResultMap.ContainsKey(dst))
                    {
                        var graph = new LimitedGraph(){Graph=DynamicGraph, LeftSet=src.Nodes, RightSet=dst.Nodes};
                        var astarResult = PathFinding<Hex>.AStar3(graph, src.Center, dst.Center); // TODO: Leverage StaticPathFinder?
                        src.PathFindingResultMap[dst] = astarResult;
                        dst.PathFindingResultMap[src] = astarResult.Reverse();
                    }
                }
            }
        }

        public void FloodingSegment(Hex node)
        {
            var segment = SegmentMap[node] = new NetworkSegment(){BeginNode = node, Nodes=new(){node}};
            Segments.Add(segment);
            var openSet = new HashSet<Hex>(){node};
            
            while(openSet.Count > 0 && segment.Count < MaxSize)
            {
                var newOpenSet = new HashSet<Hex>();
                foreach(var testNode in openSet)
                {
                    if(segment.Count == MaxSize)
                        break;

                    foreach(var neiNode in DynamicGraph.Neighbors(testNode))
                    {
                        if(SegmentMap.TryGetValue(neiNode, out var segmentNei))
                        {
                            if(segmentNei != segment)
                            {
                                segment.Neighbors.Add(segmentNei);
                                segmentNei.Neighbors.Add(segment);
                            }
                        }
                        else
                        {
                            SegmentMap[neiNode] = segment;
                            segment.Nodes.Add(neiNode);
                            newOpenSet.Add(neiNode);
                            if(segment.Count == MaxSize)
                                break;
                        }
                    }
                }
                openSet = newOpenSet;
            }
            // return segment;
        }

        public class LimitedGraph: IGraph<Hex>
        {
            public IGraph<Hex> Graph;
            public HashSet<Hex> LeftSet;
            public HashSet<Hex> RightSet;
            public IEnumerable<Hex> Neighbors(Hex p) => Graph.Neighbors(p).Where(p => LeftSet.Contains(p) || RightSet.Contains(p));
            public float MoveCost(Hex src, Hex dst) => Graph.MoveCost(src, dst);
            public float EstimateCost(Hex src, Hex dst) => Graph.EstimateCost(src, dst);
        }


    }
}