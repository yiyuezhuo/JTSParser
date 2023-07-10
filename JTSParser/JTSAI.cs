namespace YYZ.JTS
{
    using YYZ.AI;
    using YYZ.PathFinding;
    using System.Collections.Generic;
    using System;
    using System.Linq;


    public class InfluenceController
    {

        public static InfluenceMap<Hex> Setup(List<UnitState> unitStates, DistanceGraph graph, float momentum, float decay, int step=0)
        {
            var map = new InfluenceMap<Hex>(){Graph=graph, Momentum=momentum, Decay=decay};
            foreach(var node in graph.Nodes())
            {
                map.InfluenceDict[node] = 0;
            }
            foreach(var unitState in unitStates)
            {
                var node = graph.Network.HexMat[unitState.Y, unitState.X];
                map.InfluenceDict[node] = unitState.CurrentStrength;
            }
            for(var i=0; i<step; i++)
            {
                map.Step();
            }
            return map;
        }
    }

    public class InfluenceController2
    {
        public HashSet<string> FriendlyCountries; // 
        // public HashSet<string> EnemyCountries;

        public override string ToString()
        {
            var fl = string.Join(",", FriendlyCountries);
            // var el = string.Join(",", EnemyCountries);
            return $"InfluenceController2([{fl}], {Scenario}, {Map}, {Network}, {Graph}, {RootOOBUnit}, {UnitStates})";
        }

        public JTSScenario Scenario;
        public MapFile Map;
        public HexNetwork Network;
        public DistanceGraph Graph;
        public UnitGroup RootOOBUnit;
        public JTSUnitStates UnitStates;
        public float VPBudget = 100;
        public float VPDecay = 0.1f;
        public float FriendlyDecay = 0.5f;
        public float EnemyDecay = 0.5f;
        // public float FriendlyBudget = 50;
        // public float EnemyBudget = 50;
        public float StrengthBudget = 50;
        public float TargetInfluenceThreshold = 1000;

        public float[,] VPMat;
        public float[,] FriendlyMat;
        public float[,] EnemyMat;
        public float[,] EngagementMat;
        public float[,] ControlMat;
        public float[,] AssignValueMat;
        public float MaxFriendly;
        public Tuple<int, int> MaxFriendlyLoc;
        public float MaxEnemy;
        public Tuple<int, int> MaxEnemyLoc;

        public void Extract(string code, string scenarioStr, string mapStr, string oobStr)
        {
            var parser = JTSParser.FromCode(code);
            Scenario = parser.ParseScenario(scenarioStr);
            Map = parser.ParseMap(mapStr);
            Network = HexNetwork.FromMapFile(Map);
            /*
            var distance = new DistanceSystem(){Name="Column Infantry Movement Costs"};
            var nbParam = ParameterData.Parse(StaticData.NBParameterData);
            var graph = new DistanceGraph(){Network=network, Distance=distance};
            */
            RootOOBUnit = parser.ParseOOB(oobStr);
            UnitStates = new JTSUnitStates();
            UnitStates.ExtractByLines(RootOOBUnit, Scenario.DynamicCommandBlock);
        }
        public void AssignGraphByStaticData(string parameterDataStr, string movementCostName)
        {
            // AssignGraphByStaticData(StaticData.NBParameterData, "Column Infantry Movement Costs");
            var distance = new DistanceSystem(){Name=movementCostName};
            var param = ParameterData.Parse(parameterDataStr);
            distance.Extract(Map.CurrentTerrainSystem, param.Data[movementCostName]);
            Graph = new DistanceGraph(){Network=Network, Distance=distance};
        }

        public float[,] Zeros() => new float[Map.Height, Map.Width];

        public void ComputeVPMap()
        {
            VPMat = Zeros();

            foreach(var objective in Scenario.Objectives)
            {
                var pt = objective.VP + (objective.VPPerTurn1 + objective.VPPerTurn2) * 5; // TODO: Use a formula with more discretion
                // mat[objective.I, objective.J] = pt;
                var vpHex = Network.HexMat[objective.I, objective.J];
                var res = PathFinding.PathFinding<Hex>.GetReachable(Graph, vpHex, VPBudget);
                foreach((var hex, var path) in res.nodeToPath)
                {
                    VPMat[hex.I, hex.J] += pt * MathF.Exp(-VPDecay * path.cost);
                }
            }
        }
        
        /*
        public static UnitState GetCenterUnitFrom(List<UnitState> states, out float strengthSum)
        {
            var x = 0f;
            var y = 0f;
            strengthSum = 0f;
            foreach(var state in states)
            {
                x += state.CurrentStrength * state.X;
                y += state.CurrentStrength * state.Y;
                strengthSum += state.CurrentStrength;
            }
            x /= strengthSum;
            y /= strengthSum;

            UnitState minState = null;
            var minValue = float.MaxValue;
            foreach(var state in states)
            {
                var dx = x - state.X;
                var dy = y - state.Y;
                var d2 = dx * dx + dy * dy;
                if(d2 < minValue)
                {
                    minValue = d2;
                    minState = state;
                }
            }
            return minState;
        }
        */

        /*
        public class StampRecord
        {
            public bool Friendly;
            public int X;
            public int Y;
            public int I{get=>Y;}
            public int J{get=>X;}
            public float Value;
        }
        */

        public class StampRecord
        {
            public PathFinding.PathFinding<Hex>.DijkstraResult Result;
            public float Friendly;
            public float Enemy;
        }

        public static void Stamp(PathFinding.PathFinding<Hex>.DijkstraResult result, float[,] mat, float source, float decay)
        {
            foreach((var node, var path) in result.nodeToPath)
            {
                mat[node.I, node.J] += source * MathF.Exp(-decay * path.cost);    
            }
        }

        public void ComputeBrigadeMap()
        {
            FriendlyMat = Zeros();
            EnemyMat = Zeros();

            var relatedHexMap = new Dictionary<Hex, StampRecord>(); // hex => (friendly, enemy)
            // foreach((var brigade, var states) in UnitStates.GroupByBrigade())
            foreach(var brigadeFormation in UnitStates.GetBrigadeFormations())
            {
                // var centerState = GetCenterUnitFrom(states, out var strengthSum);
                var centerState = brigadeFormation.GetCenterUnitSum(out var strengthSum);
                var hex = Network.HexMat[centerState.I, centerState.J];
                if(!relatedHexMap.TryGetValue(hex, out var record))
                {
                    var res = PathFinding.PathFinding<Hex>.GetReachable(Graph, hex, StrengthBudget);
                    record = relatedHexMap[hex] = new StampRecord(){Result=res};
                }
                var friendly = FriendlyCountries.Contains(centerState.OobItem.Country);
                if(friendly)
                    record.Friendly += strengthSum;
                else
                    record.Enemy += strengthSum;
            }

            foreach((var hex, var record) in relatedHexMap)
            {
                if(record.Friendly > 0)
                {
                    Stamp(record.Result, FriendlyMat, record.Friendly, FriendlyDecay);
                }
                if(record.Enemy > 0)
                {
                    Stamp(record.Result, EnemyMat, record.Enemy, EnemyDecay);
                }
            }
        }

        public void ComputeUnitMap()
        {
            FriendlyMat = Zeros();
            EnemyMat = Zeros();

            var hex2pathXUnits = new Dictionary<Hex, Tuple<PathFinding.PathFinding<Hex>.DijkstraResult, List<UnitState>>>();
            foreach(var state in UnitStates.UnitStates)
            {
                var hex = Network.HexMat[state.I, state.J];
                if(hex2pathXUnits.TryGetValue(hex, out var unitsXPath))
                {
                    unitsXPath.Item2.Add(state);
                }
                else
                {
                    var res = PathFinding.PathFinding<Hex>.GetReachable(Graph, hex, StrengthBudget);
                    var t2 = new List<UnitState>(){state};
                    hex2pathXUnits[hex] = new (res, t2);
                }
            }

            foreach((var hex, (var pathFindingRes, var states)) in hex2pathXUnits)
            {
                foreach(var state in states)
                {
                    var friendly = FriendlyCountries.Contains(state.OobItem.Country);
                    var mat = friendly ? FriendlyMat : EnemyMat;
                    foreach((var node, var path) in pathFindingRes.nodeToPath)
                    {
                        mat[node.I, node.J] += state.CurrentStrength * MathF.Exp(-FriendlyDecay * path.cost);    
                    }
                }
            }
        }

        public void ComputeDerivedMap()
        {
            EngagementMat = Zeros();
            ControlMat = Zeros();
            AssignValueMat = Zeros();

            MaxFriendly = FindMax(FriendlyMat, out MaxFriendlyLoc);
            MaxEnemy = FindMax(EnemyMat, out MaxEnemyLoc);

            for(var i=0; i<Map.Height; i++)
                for(var j=0; j<Map.Width; j++)
                {
                    EngagementMat[i ,j] = FriendlyMat[i, j] * EnemyMat[i, j]; // / MaxFriendly / MaxEnemy
                    ControlMat[i, j] = FriendlyMat[i, j] - EnemyMat[i, j];
                    AssignValueMat[i, j] = VPMat[i, j] - 0.1f * FriendlyMat[i, j] + 0.2f * EnemyMat[i, j];
                }
        }
        
        public void ComputeInfluenceMap()
        {
            ComputeVPMap();
            ComputeUnitMap();
            ComputeDerivedMap();
        }

        public void ComputeInfluenceMap2()
        {
            ComputeVPMap();
            ComputeBrigadeMap();
            ComputeDerivedMap();
        }

        float FindMax(float[,] mat, out Tuple<int, int> maxLoc)
        {
            var maxValue = mat[0, 0];
            var maxI = 0;
            var maxJ = 0;
            for(var i=0; i<mat.GetLength(0); i++)
                for(var j=0; j<mat.GetLength(1); j++)
                {
                    if(mat[i, j] > maxValue)
                    {
                        maxValue = mat[i,j];
                        maxI = i;
                        maxJ = j;
                    }
                }
            maxLoc = new(maxI, maxJ);
            return maxValue;
        }

        public string Transform(List<AIOrder> orders, string name)
        {
            var aiScript = new AIScript(){Name=name, Orders=orders};
            var aiStatus = new AIStatus(){AIScripts=new List<AIScript>(){aiScript}};
            return aiStatus.Transform(Scenario, RootOOBUnit);
        }

        public AIOrder MakeHoldDefendOrder(Formation formation, UnitState centerState)
        {
            // var centerState = formation.GetCenterUnitSum(out var strengthSum);
            var order = new AIOrder()
            {
                Unit=formation.Group,
                Time=Scenario.Time,
                X=centerState.X,
                Y=centerState.Y,
                Type=AIOrderType.Defend
            };
            return order;
        }
        public AIOrder MakeHoldDefendOrder(Formation formation)
        {
            var centerState = formation.GetCenterUnitSum(out var _);
            return MakeHoldDefendOrder(formation, centerState);
        }

        public string TransformAllHold()
        {
            var orders = new List<AIOrder>();
            foreach(var brigadeFormation in UnitStates.GetBrigadeFormations())
            {
                orders.Add(MakeHoldDefendOrder(brigadeFormation));
            }
            return Transform(orders, "All Hold AI Script");
        }

        public string TransformAttackNearest(List<string> attackers)
        {
            var orders = new List<AIOrder>();

            var attackerSet = attackers.ToHashSet();
            // var targetFormations = new List<Formation>();
            var targetCenters = new List<UnitState>();
            var attackFormations = new List<Formation>();
            var centerMap = new Dictionary<Formation, UnitState>();
            foreach(var brigadeFormation in UnitStates.GetBrigadeFormations())
            {
                var center = brigadeFormation.GetCenterUnitSum(out var _);
                if(attackerSet.Contains(brigadeFormation.Group.Country))
                {
                    centerMap[brigadeFormation] = center;
                    attackFormations.Add(brigadeFormation);
                }
                else
                {
                    targetCenters.Add(center);
                    orders.Add(MakeHoldDefendOrder(brigadeFormation, center));
                }
            }
            foreach(var attackFormation in attackFormations)
            {
                var attackerCenter = centerMap[attackFormation];
                var minCenter = targetCenters.MinBy(other => attackerCenter.Distance2(other));
                orders.Add(new AIOrder()
                {
                    Unit=attackFormation.Group,
                    Time=Scenario.Time,
                    X=minCenter.X,
                    Y=minCenter.Y,
                    Type=AIOrderType.Attack
                });
            }
            return Transform(orders, "Attack Nearest AI Script");
        }

        public class ContourCache
        {
            public PathFinding<Hex>.DijkstraResult Result;
            public UnitState CenterState;
            public Hex Center;
            public float StrengthSum;
        }

        public IEnumerable<AIOrder> GetAttackContourOrders()
        {
            // Requires:
            // ComputeBrigadeMap();

            // A smaller threshold make units move to secure positions to regroup and ready to assualt.
            // Then a bigger threshold make AI lanuch a converging attack.

            var blockedSet = new HashSet<Hex>();
            
            for(var i=0; i<Map.Height; i++)
                for(var j=0; j<Map.Width; j++)
                {
                    if(EnemyMat[i, j] > TargetInfluenceThreshold)
                        blockedSet.Add(Network.HexMat[i, j]);
                }

            var availableSet = new HashSet<Hex>(); // edgeSet
            foreach(var hex in blockedSet)
            {
                foreach(var nei in Graph.Neighbors(hex))
                {
                    if(!blockedSet.Contains(nei))
                        availableSet.Add(nei);
                }
            }

            var blockedGraph = new BlockedGraph(){Graph=Graph, BlockedSet=blockedSet};
            
            var availableFormations = UnitStates.GetBrigadeFormations().Where(formation => FriendlyCountries.Contains(formation.Group.Country)).ToHashSet(); // TODO: de-duplicate?
            var cacheMap = new Dictionary<Formation, ContourCache>();

            foreach(var formation in availableFormations)
            {
                // var centerState = GetCenterUnitFrom(states, out var strengthSum);
                var centerState = formation.GetCenterUnitSum(out var strengthSum);
                var hex = Network.HexMat[centerState.I, centerState.J];
                var res = PathFinding<Hex>.GetReachable(blockedGraph, hex, StrengthBudget);
                cacheMap[formation] = new ContourCache()
                {
                    Result=res, CenterState=centerState, Center=hex, StrengthSum=strengthSum
                };
            }

            while(availableFormations.Count > 0 && availableSet.Count > 0)
            {
                float minCost = float.PositiveInfinity;
                Formation minFormation = null;
                Hex minHex = null;
                foreach(var formation in availableFormations)
                {
                    var cache = cacheMap[formation];
                    foreach(var hex in availableSet)
                    {
                        if(cache.Result.nodeToPath.TryGetValue(hex, out var path))
                        {
                            if(path.cost < minCost)
                            {
                                minCost = path.cost;
                                minFormation = formation;
                                minHex = hex;
                            }
                        }
                    }
                }
                if(minFormation == null)
                    break;

                availableFormations.Remove(minFormation);

                // breath-first deletion for hex
                availableSet.Remove(minHex);
                var designedWidth = (int)MathF.Ceiling(cacheMap[minFormation].StrengthSum / 500);
                var dispatched = 1;
                var openSet = new HashSet<Hex>(){minHex};
                while(openSet.Count > 0 && dispatched < designedWidth && availableSet.Count >= 0)
                {
                    var newOpenSet = new HashSet<Hex>();
                    foreach(var hex in openSet)
                    {
                        if(dispatched >= designedWidth)
                            break;
                        foreach(var nei in Graph.Neighbors(hex))
                        {
                            if(dispatched >= designedWidth)
                                break;
                            if(availableSet.Contains(nei))
                            {
                                availableSet.Remove(nei);
                                newOpenSet.Add(nei);
                                dispatched += 1;
                            }
                        }
                    }
                    openSet = newOpenSet;
                }
                yield return new AIOrder()
                {
                    Unit=minFormation.Group,
                    Time=Scenario.Time,
                    X=minHex.X,
                    Y=minHex.Y,
                    Type=AIOrderType.Attack
                };
            }
        }

        /*
        public List<AIOrder> GetAttackContourOrdersAsList() => GetAttackContourOrders().ToList(); // Python API Comp
        public static IEnumerable<int> TestPython()
        {
            yield return 0;
            yield return 1;
        }
        public static List<int> TestPythonAsList()
        {
            return TestPython().ToList();
        }
        public static int[] TestPythonAsArray()
        {
            return TestPython().ToArray();
        }
        */


        public string TransformCountourPlanning()
        {
            ComputeBrigadeMap();

            var attackOrders = GetAttackContourOrders();
            var defendOrders = UnitStates.GetBrigadeFormations().Where(f => !FriendlyCountries.Contains(f.Group.Country)).Select(f => MakeHoldDefendOrder(f));
            var orders = attackOrders.Concat(defendOrders).ToList();
            return Transform(orders, "Contour AI Script");
        }

        public class BlockedGraph : PathFinding.IGeneralGraph<Hex>
        {
            public IGeneralGraph<Hex> Graph;
            public HashSet<Hex> BlockedSet;
            public IEnumerable<Hex> Neighbors(Hex hex)
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

            public float MoveCost(Hex src, Hex dst) => Graph.MoveCost(src, dst);
        }
    }
}