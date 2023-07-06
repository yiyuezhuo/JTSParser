// See https://aka.ms/new-console-template for more information
using System;
using YYZ.JTS.NB;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var oobStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\OOBs\Coruna.oob");
        // var oobStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\OOBs\1st Bull Run.oob");
        // var scenarioStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Scenarios\002 1BR_Bull Run (Historical).scn");
        var scenarioStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\Scenarios\011.Coruna4_BrAI_test.scn");

        // var units = JTSOobParser.ParseUnits(s);
        var scenario = new JTSScenario();
        scenario.Extract(scenarioStr);
        Console.WriteLine(scenario);

        // var units = JTSOobParser.FromCode("CWB").ParseUnits(oobStr);
        var units = JTSOobParser.FromCode("NB").ParseUnits(oobStr);

        var unitStatus = new JTSUnitStates();
        unitStatus.ExtractByLines(units, scenario.DynamicCommandBlock);
        
        // Console.WriteLine('9' - '0');
        // Console.WriteLine('a' - '0');

        var mapStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\Maps\Coruna.map");
        var mapFile = MapFile.Parse(mapStr);
        Console.WriteLine(mapFile.ToString());

        var graph = InfantryColumnGraph.FromMapFile(mapFile);
        Console.WriteLine(graph);

        var roads = graph.SimplifyRoad(RoadType.Road);
        Console.WriteLine(roads.Count);
        foreach(var road in roads)
        {
            Console.WriteLine(string.Join(",", road.Select(x => $"({x.X}, {x.Y})")));
        }

        /*
        Console.WriteLine(graph.ToString());

        Console.WriteLine(graph.HexMat[0, 0]);
        Console.WriteLine(graph.HexMat[1, 0]);

        Console.WriteLine(graph.Neighbors(graph.HexMat[0, 0]));
        Console.WriteLine(graph.MoveCost(graph.HexMat[0, 0], graph.HexMat[0, 1]));
        Console.WriteLine(graph.EstimateCost(graph.HexMat[0, 0], graph.HexMat[0, 2]));
        */

        /*
        var l1 = new int[]{1,2};
        var l2 = new int[]{};
        var s1 = l1.Select(x => x);
        var s2 = l2.Select(x => x);

        Console.WriteLine(s1.Count());
        Console.WriteLine(s1.Min());

        Console.WriteLine(l1.Count());
        Console.WriteLine(l1.Min());
        Console.WriteLine(l2.Min());
        */

        /*

        var rs = YYZ.PathFinding.PathFinding<Hex>.GetReachable(graph, graph.HexMat[0, 0], 20);
        Console.WriteLine(rs.nodeToPath.Count);
        Console.WriteLine(string.Join(",", rs.nodeToPath.Keys.Select(hex => $"({hex.I}, {hex.J})")));

        var p = rs.Reconstruct(graph.HexMat[2, 2]);
        Console.WriteLine(rs.nodeToPath[graph.HexMat[2, 2]].cost);
        Console.WriteLine(string.Join(",", p.Select(hex => $"({hex.X}, {hex.Y})")));

        Console.WriteLine(graph.HexMat[0, 0]);
        Console.WriteLine(graph.HexMat[1, 0]);
        Console.WriteLine(graph.HexMat[2, 1]);
        Console.WriteLine(graph.HexMat[2, 2]);
        Console.WriteLine(graph.HexMat[1, 1]);

        var p2 = YYZ.PathFinding.PathFinding<Hex>.AStar(graph, graph.HexMat[0, 0], graph.HexMat[2, 2]);
        Console.WriteLine(string.Join(",", p2.Select(hex => $"({hex.X}, {hex.Y})")));

        Console.WriteLine(string.Join(",", graph.HexMat[1, 1].EdgeMap.Keys.Select(hex => $"({hex.X}, {hex.Y})")));
        Console.WriteLine(string.Join(",", graph.HexMat[1, 0].EdgeMap.Keys.Select(hex => $"({hex.X}, {hex.Y})")));

        var r2 = YYZ.PathFinding.PathFinding<Hex>.GetReachable(graph, graph.HexMat[21, 37], 20);
        Console.WriteLine(graph.MoveCost(graph.HexMat[21, 37], graph.HexMat[20, 37]));
        Console.WriteLine(graph.MoveCost(graph.HexMat[22, 37], graph.HexMat[21, 37]));
        foreach(var pp in r2.nodeToPath)
        {
            var prev = pp.Value.prev == null ? "" : $"({pp.Value.prev.X} {pp.Value.prev.Y})";
            Console.WriteLine($"({pp.Key.X}, {pp.Key.Y}), {pp.Value.cost}, {prev}");
        }
        */

        var r3 = YYZ.PathFinding.PathFinding<Hex>.GetReachable(graph, graph.HexMat[8, 0], 10);
        foreach(var pp in r3.nodeToPath)
        {
            var prev = pp.Value.prev == null ? "" : $"({pp.Value.prev.X} {pp.Value.prev.Y})";
            // Console.WriteLine($"({pp.Key.X}, {pp.Key.Y}), {pp.Value.cost}, {prev}");
        }

        var aiStatus = new AIStatus();
        aiStatus.Extract(units, scenario.AICommandScripts);
        Console.WriteLine(aiStatus);

        var influenceMap = InfluenceController.Setup(unitStatus.UnitStates, graph, 1, 1, 10);
        Console.WriteLine(influenceMap);
        
        /*
        var mapStr2 = File.ReadAllText(@"E:\JTSGames\Mius43\Mius.map");
        var mapFile2 = MapFile.Parse(mapStr2);
        Console.WriteLine(mapFile2.ToString());
        */
    }
}

