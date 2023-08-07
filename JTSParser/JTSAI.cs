namespace YYZ.JTS
{
    using YYZ.AI;
    using YYZ.PathFinding;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using YYZ.PathFinding2;

    // using Microsoft.Extentions.Log;


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
            return $"InfluenceController2([{fl}], {Scenario}, {Map}, {Network}, {DynamicGraph}, {RootOOBUnit}, {UnitStates})";
        }

        public JTSScenario Scenario;
        public MapFile Map;
        public HexNetwork Network;

        public DistanceGraph DynamicGraph;
        // public FrozenGraph<Hex> StaticGraph;
        public IPathFinder<Hex> StaticPathFinder;

        // public IGraphEnumerable<Hex> FrozenGraph;
        public UnitGroup RootOOBUnit;
        public JTSUnitStates UnitStates;
        public JTSBridgeStates BridgeStates;

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

        public List<string> Logs = new();

        public void Extract(string code, string scenarioStr, string mapStr, string oobStr)
        {
            var parser = JTSParser.FromCode(code);
            Scenario = parser.ParseScenario(scenarioStr);
            Map = parser.ParseMap(mapStr);
            Network = HexNetwork.FromMapFile(Map);
            RootOOBUnit = parser.ParseOOB(oobStr);
            UnitStates = new JTSUnitStates();
            UnitStates.ExtractByLines(RootOOBUnit, Scenario.DynamicCommandBlock);

            BridgeStates = new JTSBridgeStates(Map.CurrentTerrainSystem.Road.GetFirstTerrain());
            BridgeStates.ExtractByLines(Scenario.DynamicCommandBlock);
            BridgeStates.ApplyTo(Network);
        }
        public void AssignGraphByStaticData(string parameterDataStr, string movementCostName)
        {
            var distance = new DistanceSystem(){Name=movementCostName};
            var param = ParameterData.Parse(parameterDataStr);
            distance.Extract(Map.CurrentTerrainSystem, param.Data[movementCostName]);
            DynamicGraph = new DistanceGraph(){Network=Network, Distance=distance};

            /*
            var staticGraph = FrozenGraph<Hex>.GetGraph(DynamicGraph, hex => (hex.X, hex.Y));
            StaticPathFinder = staticGraph.GetPathFinder();
            */
            StaticPathFinder = FrozenGraph2D<Hex>.GetPathFinder(DynamicGraph, hex => (hex.X, hex.Y));
        }

        public float[,] Zeros() => new float[Map.Height, Map.Width];

        public void ComputeVPMap()
        {
            VPMat = Zeros();

            foreach(var objective in Scenario.Objectives)
            {
                var pt = objective.VP + (objective.VPPerTurn1 + objective.VPPerTurn2) * 5; // TODO: Use a formula with more discretion
                var vpHex = Network.HexMat[objective.I, objective.J];
                /*
                var res = PathFinding.PathFinding<Hex>.GetReachable(DynamicGraph, vpHex, VPBudget);
                foreach((var hex, var path) in res.nodeToPath)
                {
                    VPMat[hex.I, hex.J] += pt * MathF.Exp(-VPDecay * path.cost);
                }
                */
                var res = StaticPathFinder.GetReachable(vpHex, VPBudget);
                foreach((var hex, var path) in res)
                {
                    VPMat[hex.I, hex.J] += pt * MathF.Exp(-VPDecay * path.cost);
                }

            }
        }

        public class StampRecord
        {
            public PathFinding.IDijkstraOutput<Hex> Result;
            public float Friendly;
            public float Enemy;
        }

        public static void Stamp(IDijkstraOutput<Hex> result, float[,] mat, float source, float decay)
        {
            /*
            foreach((var node, var path) in result.nodeToPath)
            {
                mat[node.I, node.J] += source * MathF.Exp(-decay * path.cost);    
            }
            */
            foreach((var node, var path) in result)
            {
                mat[node.I, node.J] += source * MathF.Exp(-decay * path.cost);    
            }
        }

        public void ComputeBrigadeMap()
        {
            FriendlyMat = Zeros();
            EnemyMat = Zeros();

            var relatedHexMap = new Dictionary<Hex, StampRecord>(); // hex => (friendly, enemy)
            foreach(var brigadeFormation in UnitStates.GetBrigadeFormations())
            {
                var hex = Network.HexMat[brigadeFormation.IAnchored, brigadeFormation.JAnchored];
                if(!relatedHexMap.TryGetValue(hex, out var record))
                {
                    // var res = PathFinding.PathFinding<Hex>.GetReachable(DynamicGraph, hex, StrengthBudget);
                    var res = StaticPathFinder.GetReachable(hex, StrengthBudget);
                    record = relatedHexMap[hex] = new StampRecord(){Result=res};
                }
                var friendly = FriendlyCountries.Contains(brigadeFormation.OobItem.Country);
                if(friendly)
                    record.Friendly += brigadeFormation.CurrentStrength;
                else
                    record.Enemy += brigadeFormation.CurrentStrength;
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

            // var hex2pathXUnits = new Dictionary<Hex, Tuple<PathFinding.DijkstraResult<Hex>, List<UnitState>>>();
            var hex2pathXUnits = new Dictionary<Hex, (IDijkstraOutput<Hex>, List<UnitState>)>();
            foreach(var state in UnitStates.UnitStates)
            {
                var hex = Network.HexMat[state.I, state.J];
                if(hex2pathXUnits.TryGetValue(hex, out var unitsXPath))
                {
                    unitsXPath.Item2.Add(state);
                }
                else
                {
                    // var res = PathFinding.PathFinding<Hex>.GetReachable(DynamicGraph, hex, StrengthBudget);
                    var res = StaticPathFinder.GetReachable(hex, StrengthBudget);
                    var t2 = new List<UnitState>(){state};
                    hex2pathXUnits[hex] = (res, t2);
                }
            }

            foreach((var hex, (var pathFindingRes, var states)) in hex2pathXUnits)
            {
                foreach(var state in states)
                {
                    var friendly = FriendlyCountries.Contains(state.OobItem.Country);
                    var mat = friendly ? FriendlyMat : EnemyMat;
                    /*
                    foreach((var node, var path) in pathFindingRes.nodeToPath)
                    {
                        mat[node.I, node.J] += state.CurrentStrength * MathF.Exp(-FriendlyDecay * path.cost);    
                    }
                    */
                    foreach((var node, var path) in pathFindingRes)
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

        public AIOrder MakeHoldDefendOrder(Formation formation)
        {
            var order = new AIOrder()
            {
                Unit=formation.Group,
                Time=Scenario.Time,
                X=formation.XAnchored,
                Y=formation.YAnchored,
                Type=AIOrderType.Defend
            };
            return order;
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
            var targetFormations = new List<Formation>();
            var attackFormations = new List<Formation>();
            foreach(var brigadeFormation in UnitStates.GetBrigadeFormations())
            {
                if(attackerSet.Contains(brigadeFormation.Group.Country))
                {
                    attackFormations.Add(brigadeFormation);
                }
                else
                {
                    targetFormations.Add(brigadeFormation);
                    orders.Add(MakeHoldDefendOrder(brigadeFormation));
                }
            }
            foreach(var attackFormation in attackFormations)
            {
                var minFormation = Utils.MinBy(targetFormations, f => Utils.Distance2(attackFormation.XMean, attackFormation.YMean, f.XMean, f.YMean));

                orders.Add(new AIOrder()
                {
                    Unit=attackFormation.Group,
                    Time=Scenario.Time,
                    X=minFormation.XAnchored,
                    Y=minFormation.YAnchored,
                    Type=AIOrderType.Attack
                });
            }
            return Transform(orders, "Attack Nearest AI Script");
        }

        public class ContourCache
        {
            public IDijkstraOutput<Hex> Result;
            public Hex Center;
        }

        public IEnumerable<DispatchPlan> AllocateSpace(HashSet<Hex> availableSet, HashSet<Formation> availableFormations, IGraphEnumerable<Hex> graphModified)
        {
            var cacheMap = new Dictionary<Formation, ContourCache>();

            var staticPathFinderModified = FrozenGraph2D<Hex>.GetPathFinder(graphModified, hex => (hex.X, hex.Y));

            foreach(var formation in availableFormations)
            {
                var hex = Network.HexMat[formation.IAnchored, formation.JAnchored];
                // var res = PathFinding<Hex>.GetReachable(graphModified, hex, StrengthBudget);
                var res = staticPathFinderModified.GetReachable(hex, StrengthBudget); // FIXME: here should use another PathFinder
                cacheMap[formation] = new ContourCache()
                {
                    Result=res, Center=hex
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
                        // if(cache.Result.nodeToPath.TryGetValue(hex, out var path))
                        if(cache.Result.TryGetValue(hex, out var path))
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
                var designedWidth = (int)MathF.Ceiling(minFormation.CurrentStrength / 500);
                var dispatchedList = new List<Hex>(){minHex};
                var openSet = new HashSet<Hex>(){minHex};
                while(openSet.Count > 0 && dispatchedList.Count < designedWidth && availableSet.Count >= 0)
                {
                    var newOpenSet = new HashSet<Hex>();
                    foreach(var hex in openSet)
                    {
                        if(dispatchedList.Count >= designedWidth)
                            break;
                        foreach(var nei in DynamicGraph.Neighbors(hex))
                        {
                            if(dispatchedList.Count >= designedWidth)
                                break;
                            if(availableSet.Contains(nei))
                            {
                                availableSet.Remove(nei);
                                newOpenSet.Add(nei);
                                dispatchedList.Add(nei);
                            }
                        }
                    }
                    openSet = newOpenSet;
                }
                yield return new DispatchPlan()
                {
                    Formation=minFormation,
                    AnchorHex=minHex,
                    AllocatedSpace=dispatchedList,
                    DesignedWidth=designedWidth
                };
            }
        }

        public class DispatchPlan
        {
            public Formation Formation;
            public Hex AnchorHex;
            public List<Hex> AllocatedSpace;
            public int DesignedWidth;

            public override string ToString()
            {
                return $"DispatchPlan({Formation}, {AnchorHex}, Allocated=[{AllocatedSpace.Count}], Designed={DesignedWidth})";
            }
        }

        public void GetBlockedSetAvailableSet(out HashSet<Hex> blockedSet, out HashSet<Hex> availableSet)
        {
            blockedSet = new HashSet<Hex>();
            
            for(var i=0; i<Map.Height; i++)
                for(var j=0; j<Map.Width; j++)
                {
                    if(EnemyMat[i, j] > TargetInfluenceThreshold)
                        blockedSet.Add(Network.HexMat[i, j]);
                }

            availableSet = new HashSet<Hex>(); // edgeSet
            foreach(var hex in blockedSet)
            {
                foreach(var nei in DynamicGraph.Neighbors(hex))
                {
                    if(!blockedSet.Contains(nei))
                        availableSet.Add(nei);
                }
            }
        }

        public IEnumerable<DispatchPlan> GetAttackContourPlan()
        {
            // Requires:
            // ComputeBrigadeMap();

            // A smaller threshold make units move to secure positions to regroup and ready to assualt.
            // Then a bigger threshold make AI lanuch a converging attack.

            GetBlockedSetAvailableSet(out var blockedSet, out var availableSet);

            var blockedGraph = new BlockedGraph(){Graph=DynamicGraph, BlockedSet=blockedSet};
            
            var availableFormations = UnitStates.GetBrigadeFormations().Where(formation => FriendlyCountries.Contains(formation.Group.Country)).ToHashSet(); // TODO: de-duplicate?

            return AllocateSpace(availableSet, availableFormations, blockedGraph);
        }

        public IEnumerable<AIOrder> GetAttackContourOrders()
        {
            foreach(var plan in GetAttackContourPlan())
            {
                var ss = string.Join(",", plan.AllocatedSpace.Select(h => $"({h.X}, {h.Y})"));
                Log($"HQ => {plan.Formation.Group.DescribeCommand()} is assigned at {plan.AllocatedSpace.Count} hexes (requested {plan.DesignedWidth}): {ss}");
                // dispatchPlan.Formation.Group.
                yield return new AIOrder()
                {
                    Unit=plan.Formation.Group,
                    Time=Scenario.Time,
                    X=plan.AnchorHex.X,
                    Y=plan.AnchorHex.Y,
                    Type=AIOrderType.Attack
                };
            }
        }

        public string TransformCountourPlanning()
        {
            ComputeBrigadeMap();

            var attackOrders = GetAttackContourOrders();
            var defendOrders = UnitStates.GetBrigadeFormations().Where(f => !FriendlyCountries.Contains(f.Group.Country)).Select(f => MakeHoldDefendOrder(f));
            var orders = attackOrders.Concat(defendOrders).ToList();
            return Transform(orders, "Contour AI Script");
        }

        public class BlockedGraph : PathFinding.IGraphEnumerable<Hex>
        {
            public IGraphEnumerable<Hex> Graph;
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
            public float EstimateCost(Hex src, Hex dst) => Graph.EstimateCost(src, dst);
            public IEnumerable<Hex> Nodes() => Graph.Nodes();
        }

        public IEnumerable<DispatchPlan> GetHierarchyFrontalAttackPlan() // ~= GetAttackContourPlan
        {
            GetBlockedSetAvailableSet(out var blockedSet, out var availableSet);

            var blockedGraph = new BlockedGraph(){Graph=DynamicGraph, BlockedSet=blockedSet};

            var activeFormations = new List<Formation>();
            /*
            foreach((var unitGroup, var formation) in UnitStates.Group2Formation)
            {
                if(formation != UnitStates.FormationRoot && FriendlyCountries.Contains(unitGroup.Country))
                    activeFormations.Add(formation);
            }
            */
            foreach(var formation in UnitStates.FormationRoot.SubFormations)
            {
                if(FriendlyCountries.Contains(formation.Group.Country))
                {
                    activeFormations.Add(formation);
                }
            }
            
            // var plans = AllocateSpace(availableSet.ToHashSet(), activeFormations.ToHashSet(), blockedGraph).ToList(); // Pass shallow-copied value
            // foreach(var plan in)
            var rootPlans = AllocateSpace(availableSet.ToHashSet(), activeFormations.ToHashSet(), blockedGraph).ToList(); // Pass shallow-copied value
            var stack = new Stack<List<DispatchPlan>>();
            stack.Push(rootPlans);
            while(stack.Count > 0)
            {
                var plans = stack.Pop();

                foreach(var plan in plans)
                {
                    yield return plan;
                    
                    if(plan.Formation.SubFormations.Count > 1)
                    {
                        var newPlans = AllocateSpace(plan.AllocatedSpace.ToHashSet(), plan.Formation.SubFormations.ToHashSet(), blockedGraph).ToList();
                        stack.Push(newPlans);
                    }
                }
            }
            // var availableFormations = UnitStates.GetBrigadeFormations().Where(formation => FriendlyCountries.Contains(formation.Group.Country)).ToHashSet(); // TODO: de-duplicate?
            // return AllocateSpace(availableSet, availableFormations, blockedGraph);
        }

        public IEnumerable<AIOrder> GetHierarchyFrontalAttackOrders()
        {
            foreach(var plan in GetHierarchyFrontalAttackPlan())
            {
                var ss = string.Join(",", plan.AllocatedSpace.Select(h => $"({h.X}, {h.Y})"));
                var superior = plan.Formation.Parent == UnitStates.FormationRoot ? "HQ" : plan.Formation.Parent.Group.DescribeCommand();
                Log($"{superior} => {plan.Formation.Group.DescribeCommand()} is assigned at {plan.AllocatedSpace.Count} hexes (requested {plan.DesignedWidth}): {ss}");
                // dispatchPlan.Formation.Group.
                yield return new AIOrder()
                {
                    Unit=plan.Formation.Group,
                    Time=Scenario.Time,
                    X=plan.AnchorHex.X,
                    Y=plan.AnchorHex.Y,
                    Type=AIOrderType.Attack
                };
            }
        }

        public void Log(string s) => Logs.Add(s);

        public Dictionary<NetworkSegment, int> GetSegmentStrengthMap(string countryName, Func<Hex, NetworkSegment> segmentGetter)
        {
            var segmentStrengthMap = new Dictionary<NetworkSegment, int>();

            foreach(var state in UnitStates.UnitStates)
            {
                if(countryName == state.OobItem.Country)
                {
                    var hex = Network.HexMat[state.I, state.J];
                    var segment = segmentGetter(hex);
                    if(!segmentStrengthMap.TryGetValue(segment, out var value))
                        value = 0;
                    segmentStrengthMap[segment] = value + state.CurrentStrength;
                }
            }
            return segmentStrengthMap;
        }
    }


}