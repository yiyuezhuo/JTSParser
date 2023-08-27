
using YYZ.JTS;
using System;
using YYZ.PathFinding;
using YYZ.PathFinding2;
using System.Collections.Generic;
using System.Linq;
using YYZ.AI.PointToPointAllocator;

namespace YYZ.JTS.AI
{
    public class Position: IPosition
    {
        public Hex Origin;

        public override bool Equals(object obj)
        {
            return obj is Position position &&
                   EqualityComparer<Hex>.Default.Equals(Origin, position.Origin);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode();
        }

    }

    public class Unit: IUnit
    {
        public UnitState Origin;
        public float CombatValue{get => Origin.CurrentStrength;} // TODO: handle gun
        public IPosition Position{get; set;}
    }

    public class Board: IBoard
    {
        public GameState S;
        public GraphWrapper G;
        public HashSet<string> activeCountries;
        public List<float> MoveTime(IPosition src, IEnumerable<IPosition> positions)
        {
            // I dont't want use complicated generic to write experimenting code so I just downcast,
            // so a little performance hit and introduce some obscure implicity constraints which could be enforced by generic.
            // For example, positions should be the same concrete class, which can be enforced by generic, but I rather not to write many 
            // generic type shits.
            var _src = ((Position)src).Origin;
            var _positions = positions.Select(p => ((Position)p).Origin).ToList();
            
            var res = G.StaticPathFinder.GetShortpathForMultiple(_src, _positions);
            var ret = new List<float>();
            foreach(var pos in _positions)
                if(res.TryGetValue(pos, out var path))
                    ret.Add(path.cost);
                else
                    ret.Add(-1);
            return ret;
        }

        IEnumerable<IUnit> SelectUnits(bool selectActive)
        {
            foreach(var state in S.UnitStates.UnitStates)
            {
                var isActive = activeCountries.Contains(state.OobItem.Country);
                if((isActive == selectActive) && (!isActive == !selectActive))
                {
                    var hex = S.Network.HexMat[state.I, state.J];
                    var position = new Position(){Origin=hex};
                    var unit = new Unit()
                    {
                        Origin=state,
                        Position=position
                    };
                    yield return unit;
                }
            }

        }

        public IEnumerable<IUnit> ActivesUnits
        {
            get => SelectUnits(true);
        }
        public IEnumerable<IUnit> PassiveUnits
        {
            get => SelectUnits(false);
        }


    }

    public class StateGraphWrapper
    {
        public GameState S;
        public GraphWrapper G;

        public float[,] ComputeVPMap(float vpBudget, float vpDecay)
        {
            var vpMat = S.Zeros();

            foreach(var objective in S.Scenario.Objectives)
            {
                var pt = objective.VP + (objective.VPPerTurn1 + objective.VPPerTurn2) * 5; // TODO: Use a formula with more discretion
                var vpHex = S.Network.HexMat[objective.I, objective.J];
                var res = G.StaticPathFinder.GetReachable(vpHex, vpBudget);
                foreach((var hex, var path) in res)
                {
                    vpMat[hex.I, hex.J] += pt * MathF.Exp(-vpDecay * path.cost);
                }
            }

            return vpMat;
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

        public class UnitInfluenceMapParams
        {
            public HashSet<string> FriendlyCountries;
            public float StrengthBudget;
            public float FriendlyDecay;
            public float EnemyDecay;
        }

        public (float[,] friendlyMat, float[,] enemyMat) ComputeBrigadeMap(UnitInfluenceMapParams p)
        {
            var friendlyMat = S.Zeros();
            var enemyMat = S.Zeros();

            var relatedHexMap = new Dictionary<Hex, StampRecord>(); // hex => (friendly, enemy)
            foreach(var brigadeFormation in S.UnitStates.GetBrigadeFormations())
            {
                var hex = S.Network.HexMat[brigadeFormation.IAnchored, brigadeFormation.JAnchored];
                if(!relatedHexMap.TryGetValue(hex, out var record))
                {
                    // var res = PathFinding.PathFinding<Hex>.GetReachable(DynamicGraph, hex, StrengthBudget);
                    var res = G.StaticPathFinder.GetReachable(hex, p.StrengthBudget);
                    record = relatedHexMap[hex] = new StampRecord(){Result=res};
                }
                var friendly = p.FriendlyCountries.Contains(brigadeFormation.OobItem.Country);
                if(friendly)
                    record.Friendly += brigadeFormation.CurrentStrength;
                else
                    record.Enemy += brigadeFormation.CurrentStrength;
            }

            foreach((var hex, var record) in relatedHexMap)
            {
                if(record.Friendly > 0)
                {
                    Stamp(record.Result, friendlyMat, record.Friendly, p.FriendlyDecay);
                }
                if(record.Enemy > 0)
                {
                    Stamp(record.Result, enemyMat, record.Enemy, p.EnemyDecay);
                }
            }

            return (friendlyMat, enemyMat);
        }

        public (float[,] friendlyMat, float[,] enemyMat) ComputeUnitMap(UnitInfluenceMapParams p)
        {
            var friendlyMat = S.Zeros();
            var enemyMat = S.Zeros();

            // var hex2pathXUnits = new Dictionary<Hex, Tuple<PathFinding.DijkstraResult<Hex>, List<UnitState>>>();
            var hex2pathXUnits = new Dictionary<Hex, (IDijkstraOutput<Hex>, List<UnitState>)>();
            foreach(var state in S.UnitStates.UnitStates)
            {
                var hex = S.Network.HexMat[state.I, state.J];
                if(hex2pathXUnits.TryGetValue(hex, out var unitsXPath))
                {
                    unitsXPath.Item2.Add(state);
                }
                else
                {
                    // var res = PathFinding.PathFinding<Hex>.GetReachable(DynamicGraph, hex, StrengthBudget);
                    var res = G.StaticPathFinder.GetReachable(hex, p.StrengthBudget);
                    var t2 = new List<UnitState>(){state};
                    hex2pathXUnits[hex] = (res, t2);
                }
            }

            foreach((var hex, (var pathFindingRes, var states)) in hex2pathXUnits)
            {
                foreach(var state in states)
                {
                    var friendly = p.FriendlyCountries.Contains(state.OobItem.Country);
                    var mat = friendly ? friendlyMat : enemyMat;
                    var decay = friendly ? p.FriendlyDecay : p.EnemyDecay;
                    /*
                    foreach((var node, var path) in pathFindingRes.nodeToPath)
                    {
                        mat[node.I, node.J] += state.CurrentStrength * MathF.Exp(-FriendlyDecay * path.cost);    
                    }
                    */
                    foreach((var node, var path) in pathFindingRes)
                    {
                        mat[node.I, node.J] += state.CurrentStrength * MathF.Exp(-decay * path.cost);    
                    }
                }
            }
            return (friendlyMat, enemyMat);
        }

        public class ContourCache
        {
            public IDijkstraOutput<Hex> Result;
            public Hex Center;
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

        public class AllocationPureParams
        {
            public float StrengthBudget;
            public HashSet<string> FriendlyCountries;
            public float TargetInfluenceThreshold;
        }

        public class AllocationParams: AllocationPureParams
        {
            public float[,] EnemyMat;
            public static AllocationParams With(AllocationPureParams p, float[,] enemyMat) =>
                new AllocationParams()
                {StrengthBudget=p.StrengthBudget, FriendlyCountries=p.FriendlyCountries,
                TargetInfluenceThreshold=p.TargetInfluenceThreshold, EnemyMat=enemyMat};
        }

        public IEnumerable<DispatchPlan> AllocateSpace(HashSet<Hex> availableSet, HashSet<Formation> availableFormations, IGraphEnumerable<Hex> graphModified, AllocationParams p)
        {
            var cacheMap = new Dictionary<Formation, ContourCache>();

            var staticPathFinderModified = FrozenGraph2D<Hex>.GetPathFinder(graphModified, hex => (hex.X, hex.Y));

            foreach(var formation in availableFormations)
            {
                var hex = S.Network.HexMat[formation.IAnchored, formation.JAnchored];
                // var res = PathFinding<Hex>.GetReachable(graphModified, hex, StrengthBudget);
                var res = staticPathFinderModified.GetReachable(hex, p.StrengthBudget); // FIXME: here should use another PathFinder
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
                        foreach(var nei in G.DynamicGraph.Neighbors(hex)) // FIXME: Introduce StaticGraph?
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

        public void GetBlockedSetAvailableSet(AllocationParams p, out HashSet<Hex> blockedSet, out HashSet<Hex> availableSet)
        {
            blockedSet = new HashSet<Hex>();
            
            for(var i=0; i<S.Height; i++)
                for(var j=0; j<S.Width; j++)
                {
                    if(p.EnemyMat[i, j] > p.TargetInfluenceThreshold)
                        blockedSet.Add(S.Network.HexMat[i, j]);
                }

            availableSet = new HashSet<Hex>(); // edgeSet
            foreach(var hex in blockedSet)
            {
                foreach(var nei in G.DynamicGraph.Neighbors(hex)) // TODO: Use StaticGraph?
                {
                    if(!blockedSet.Contains(nei))
                        availableSet.Add(nei);
                }
            }
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


        public IEnumerable<DispatchPlan> GetAttackContourPlan(AllocationParams p)
        {
            // Requires:
            // ComputeBrigadeMap();

            // A smaller threshold make units move to secure positions to regroup and ready to assualt.
            // Then a bigger threshold make AI lanuch a converging attack.

            GetBlockedSetAvailableSet(p, out var blockedSet, out var availableSet);

            var blockedGraph = new BlockedGraph(){Graph=G.DynamicGraph, BlockedSet=blockedSet};
            
            var availableFormations = S.UnitStates.GetBrigadeFormations().Where(formation => p.FriendlyCountries.Contains(formation.Group.Country)).ToHashSet(); // TODO: de-duplicate?

            return AllocateSpace(availableSet, availableFormations, blockedGraph, p);
        }

        public IEnumerable<DispatchPlan> GetHierarchyFrontalAttackPlan(AllocationParams p) // ~= GetAttackContourPlan
        {
            GetBlockedSetAvailableSet(p, out var blockedSet, out var availableSet);

            var blockedGraph = new BlockedGraph(){Graph=G.DynamicGraph, BlockedSet=blockedSet};

            var activeFormations = new List<Formation>();
            /*
            foreach((var unitGroup, var formation) in UnitStates.Group2Formation)
            {
                if(formation != UnitStates.FormationRoot && FriendlyCountries.Contains(unitGroup.Country))
                    activeFormations.Add(formation);
            }
            */
            foreach(var formation in S.UnitStates.FormationRoot.SubFormations)
            {
                if(p.FriendlyCountries.Contains(formation.Group.Country))
                {
                    activeFormations.Add(formation);
                }
            }
            
            // var plans = AllocateSpace(availableSet.ToHashSet(), activeFormations.ToHashSet(), blockedGraph).ToList(); // Pass shallow-copied value
            // foreach(var plan in)
            var rootPlans = AllocateSpace(availableSet.ToHashSet(), activeFormations.ToHashSet(), blockedGraph, p).ToList(); // Pass shallow-copied value
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
                        var newPlans = AllocateSpace(plan.AllocatedSpace.ToHashSet(), plan.Formation.SubFormations.ToHashSet(), blockedGraph, p).ToList();
                        stack.Push(newPlans);
                    }
                }
            }
            // var availableFormations = UnitStates.GetBrigadeFormations().Where(formation => FriendlyCountries.Contains(formation.Group.Country)).ToHashSet(); // TODO: de-duplicate?
            // return AllocateSpace(availableSet, availableFormations, blockedGraph);
        }

        public IEnumerable<AIOrder> GetHierarchyFrontalAttackOrders(AllocationParams p)
        {
            foreach(var plan in GetHierarchyFrontalAttackPlan(p))
            {
                var ss = string.Join(",", plan.AllocatedSpace.Select(h => $"({h.X}, {h.Y})"));
                var superior = plan.Formation.Parent == S.UnitStates.FormationRoot ? "HQ" : plan.Formation.Parent.Group.DescribeCommand();
                // Log($"{superior} => {plan.Formation.Group.DescribeCommand()} is assigned at {plan.AllocatedSpace.Count} hexes (requested {plan.DesignedWidth}): {ss}");
                // dispatchPlan.Formation.Group.
                yield return new AIOrder()
                {
                    Unit=plan.Formation.Group,
                    Time=S.Scenario.Time,
                    X=plan.AnchorHex.X,
                    Y=plan.AnchorHex.Y,
                    Type=AIOrderType.Attack
                };
            }
        }

    }

    public class Transformers
    {
        public StateGraphWrapper sg;

        public IEnumerable<AIOrder> GetAttackContourOrders(StateGraphWrapper.AllocationParams p)
        {
            foreach(var plan in sg.GetAttackContourPlan(p))
            {
                var ss = string.Join(",", plan.AllocatedSpace.Select(h => $"({h.X}, {h.Y})"));
                // Log($"HQ => {plan.Formation.Group.DescribeCommand()} is assigned at {plan.AllocatedSpace.Count} hexes (requested {plan.DesignedWidth}): {ss}");
                // dispatchPlan.Formation.Group.
                yield return new AIOrder()
                {
                    Unit=plan.Formation.Group,
                    Time=sg.S.Scenario.Time,
                    X=plan.AnchorHex.X,
                    Y=plan.AnchorHex.Y,
                    Type=AIOrderType.Attack
                };
            }
        }

        public string TransformCountourPlanning(StateGraphWrapper.UnitInfluenceMapParams p, StateGraphWrapper.AllocationPureParams p2)
        {
            (var friendlyMat, var enemyMat) = sg.ComputeBrigadeMap(p);

            var attackOrders = GetAttackContourOrders(StateGraphWrapper.AllocationParams.With(p2, enemyMat));
            var defendOrders = sg.S.UnitStates.GetBrigadeFormations().Where(f => !p.FriendlyCountries.Contains(f.Group.Country)).Select(f => sg.S.MakeHoldDefendOrder(f));
            var orders = attackOrders.Concat(defendOrders).ToList();
            return sg.S.Transform(orders, "Contour AI Script");
        }
    }
}