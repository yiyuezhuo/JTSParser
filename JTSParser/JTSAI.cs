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
        
        public void ComputeInfluenceMap()
        {
            VPMat = Zeros();
            FriendlyMat = Zeros();
            EnemyMat = Zeros();
            EngagementMat = Zeros();
            ControlMat = Zeros();
            AssignValueMat = Zeros();

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

            var hex2unitsXPath = new Dictionary<Hex, Tuple<PathFinding.PathFinding<Hex>.DijkstraResult, List<UnitState>>>();
            foreach(var state in UnitStates.UnitStates)
            {
                var hex = Network.HexMat[state.I, state.J];
                if(hex2unitsXPath.TryGetValue(hex, out var unitsXPath))
                {
                    unitsXPath.Item2.Add(state);
                }
                else
                {
                    var res = PathFinding.PathFinding<Hex>.GetReachable(Graph, hex, StrengthBudget);
                    var t2 = new List<UnitState>(){state};
                    hex2unitsXPath[hex] = new (res, t2);
                }
            }

            foreach((var hex, (var pathFindingRes, var states)) in hex2unitsXPath)
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
    }
}