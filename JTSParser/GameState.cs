using System.IO;
// using YYZ.PathFinding;
using System.Collections.Generic;
using System.Linq;

namespace YYZ.JTS
{
    public class GameState
    {
        public JTSScenario Scenario;
        public MapFile Map;
        public HexNetwork Network;
        public UnitGroup RootOOBUnit;
        public JTSUnitStates UnitStates;
        public JTSBridgeStates BridgeStates;
        public int Height{get=>Map.Height;}
        public int Width{get=>Map.Width;}

        public static GameState Load(string code, string scenarioStr, string mapStr, string oobStr)
        {
            var parser = JTSParser.FromCode(code);
            var scenario = parser.ParseScenario(scenarioStr);
            var map = parser.ParseMap(mapStr);
            var network = HexNetwork.FromMapFile(map);
            var rootOOBUnit = parser.ParseOOB(oobStr);
            var unitStates = new JTSUnitStates();
            unitStates.ExtractByLines(rootOOBUnit, scenario.DynamicCommandBlock);

            var bridgeStates = new JTSBridgeStates(map.CurrentTerrainSystem.Road.GetFirstTerrain());
            bridgeStates.ExtractByLines(scenario.DynamicCommandBlock);
            bridgeStates.ApplyTo(network);

            return new()
            {
                Scenario=scenario,
                Map=map,
                Network=network,
                RootOOBUnit=rootOOBUnit,
                UnitStates=unitStates,
                BridgeStates=bridgeStates
            };
        }

        public static GameState LoadFromPath(string code, string scenarioPath, string mapPath, string oobPath)
            => Load(code, File.ReadAllText(scenarioPath), File.ReadAllText(mapPath), File.ReadAllText(oobPath));

        // Present following as an extension method in JTSAI?
        public float[,] Zeros() => new float[Height, Width]; 

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
    }
}