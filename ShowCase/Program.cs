// See https://aka.ms/new-console-template for more information
using System;
using YYZ.JTS;
using YYZ.PathFinding;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

class JTSEnvironment
{
    public string MapFolder;
    public string OOBFolder;
    public string ScenarioFolder;
    public JTSParser Parser;
    public void Load(string scenarioName, out string scenarioStr, out string OOBStr, out string mapStr)
    {
        scenarioStr = File.ReadAllText(Path.Join(ScenarioFolder, scenarioName));
        var scenario = Parser.ParseScenario(scenarioStr);
        OOBStr = File.ReadAllText(Path.Join(OOBFolder, scenario.OobFile));
        mapStr = File.ReadAllText(Path.Join(MapFolder, scenario.MapFile));
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var nbParser = new JTSParser(JTSSeries.NapoleonicBattle);
        var cwbParser = new JTSParser(JTSSeries.CivilWarBattle);
        var pzcParser = new JTSParser(JTSSeries.PanzerCampaign);

        string scenarioStr, OOBStr, mapStr;
        InfluenceController2 controller;

        var nbPen = new JTSEnvironment(){ScenarioFolder=@"E:\JTSGames\Pen_spain\Scenarios", MapFolder=@"E:\JTSGames\Pen_spain\Maps", OOBFolder=@"E:\JTSGames\Pen_spain\OOBs", Parser=nbParser};
        nbPen.Load("167.Vitoria1_21June13.scn", out scenarioStr, out OOBStr, out mapStr);

        controller = new InfluenceController2();
        // controller.FriendlyCountries = new HashSet<string>(){"French"};
        controller.Extract("NB", scenarioStr, mapStr, OOBStr);
        controller.AssignGraphByStaticData(StaticData.NBParameterData, "Column Infantry Movement Costs");

        var divider = new HexGraphDivider(){Graph=controller.Graph, RoadSystem=controller.Map.CurrentTerrainSystem.Road};
        var segGraph = divider.GetGraph();
        Console.WriteLine(segGraph);

        {
            var src = segGraph.SegmentMap[controller.Network.HexMat[10, 10]];
            var dst = segGraph.SegmentMap[controller.Network.HexMat[50, 50]];
            var res = PathFinding<NetworkSegment>.AStar3(segGraph, src, dst);
            Console.WriteLine(res);
        }


        Console.WriteLine(nbParser.ParseScenario(File.ReadAllText(@"E:\JTSGames\Pen_spain\Saves\battle_loss.btl")));
        Console.WriteLine(cwbParser.ParseScenario(File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Saves\battle_loss.btl")));
        Console.WriteLine(pzcParser.ParseScenario(File.ReadAllText(@"E:\JTSGames\Mius43\battle_loss.btl")));

        var oobStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\OOBs\Coruna.oob");
        // var oobStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\OOBs\1st Bull Run.oob");
        // var scenarioStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Scenarios\002 1BR_Bull Run (Historical).scn");
        scenarioStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\Scenarios\011.Coruna4_BrAI_test.scn");



        // var units = JTSOobParser.ParseUnits(s);
        /*
        var scenario = new JTSScenario();
        scenario.Extract(scenarioStr);
        */
        var scenario = nbParser.ParseScenario(scenarioStr);
        Console.WriteLine(scenario);

        // var units = JTSOobParser.FromCode("CWB").ParseUnits(oobStr);
        // var units = JTSOobParser.FromCode("NB").ParseUnits(oobStr);
        var units = nbParser.ParseOOB(oobStr);
        Console.WriteLine(units);

        var unitStatus = new JTSUnitStates();
        unitStatus.ExtractByLines(units, scenario.DynamicCommandBlock);
        Console.WriteLine(unitStatus);
        Console.WriteLine(unitStatus.FormationRoot);
        
        // Console.WriteLine('9' - '0');
        // Console.WriteLine('a' - '0');

        mapStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\Maps\Coruna.map");
        var mapFile = nbParser.ParseMap(mapStr);

        // var mapFile = MapFile.Parse(mapStr);
        Console.WriteLine(mapFile.ToString());

        var distance = new DistanceSystem(){Name="Column Infantry Movement Costs"};
        var nbParam = ParameterData.Parse(StaticData.NBParameterData);
        distance.Extract(mapFile.CurrentTerrainSystem, nbParam.Data["Column Infantry Movement Costs"]);
        Console.WriteLine(distance);
        
        var network = HexNetwork.FromMapFile(mapFile);
        Console.WriteLine(network);

        var graph = new DistanceGraph(){Network=network, Distance=distance};
        Console.WriteLine(graph);

        var roads = network.SimplifyRoad(mapFile.CurrentTerrainSystem.Road.GetValue("Road"));
        Console.WriteLine(roads.Count);
        foreach(var road in roads)
        {
            // Console.WriteLine(string.Join(",", road.Select(x => $"({x.X}, {x.Y})")));
        }

        var mapFullStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Maps\the gaps to manassas.map");
        var mapSubStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Maps\Henry Hill.map");
        var fullMap = cwbParser.ParseMap(mapFullStr);
        var subMap = new SubMapFile();
        subMap.Extract(mapSubStr);
        Console.WriteLine($"Before: {fullMap}");
        subMap.ApplyTo(fullMap);
        Console.WriteLine($"After: {fullMap} | {subMap}");
        
        var objectiveHexes = scenario.Objectives.Select(o => network.HexMat[o.I, o.J]).ToList();
        var limitNetwork = graph.GetSparseProxyGraph(objectiveHexes);
        Console.WriteLine(limitNetwork);

        controller = new InfluenceController2(){FriendlyCountries = new HashSet<string>(){"French"}};
        controller.Extract("NB", scenarioStr, mapStr, oobStr);
        controller.AssignGraphByStaticData(StaticData.NBParameterData, "Column Infantry Movement Costs");

        controller.VPDecay = 0.05f;
        controller.FriendlyDecay = 0.1f;
        controller.EnemyDecay = 0.1f;

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        // controller.ComputeInfluenceMap();
        // controller.TransformCountourPlanning();

        controller.ComputeBrigadeMap();
        var orders = controller.GetAttackContourOrders().ToList();

        TimeSpan ts = stopWatch.Elapsed;

        Console.WriteLine(ts);

        Console.WriteLine($"orders.Count={orders.Count}");
        foreach(var order in orders)
            Console.WriteLine(order);
        
        controller.TargetInfluenceThreshold = 1750;
        orders = controller.GetHierarchyFrontalAttackOrders().ToList();

        Console.WriteLine($"[Hierarchy] orders.Count={orders.Count}");
        foreach(var order in orders)
            Console.WriteLine(order);


        /*
        stopWatch.Restart();

        controller.ComputeInfluenceMap2();
        ts = stopWatch.Elapsed;

        Console.WriteLine(ts);
        */

        Console.WriteLine(controller);

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

        var r3 = YYZ.PathFinding.PathFinding<Hex>.GetReachable(graph, network.HexMat[8, 0], 10);
        foreach(var pp in r3.nodeToPath)
        {
            var prev = pp.Value.prev == null ? "" : $"({pp.Value.prev.X} {pp.Value.prev.Y})";
            // Console.WriteLine($"({pp.Key.X}, {pp.Key.Y}), {pp.Value.cost}, {prev}");
        }

        var aiStatus = new AIStatus();
        aiStatus.Extract(units, scenario.AICommandScripts);
        // Console.WriteLine(aiStatus);

        var influenceMap = InfluenceController.Setup(unitStatus.UnitStates, graph, 1, 1, 10);
        // Console.WriteLine(influenceMap);
        
        /*
        var mapStr2 = File.ReadAllText(@"E:\JTSGames\Mius43\Mius.map");
        var mapFile2 = MapFile.Parse(mapStr2);
        Console.WriteLine(mapFile2.ToString());
        */

        // Console.WriteLine(ParameterData.Parse(StaticData.NBParameterData));

        
        // scenarioStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Scenarios\002 1BR_Bull Run (Historical).scn");
        // scenarioStr = File.ReadAllText(@"E:\JTSGames\cwb_demo\Scenarios\005-620101-Williamsburg.scn");
        scenarioStr = File.ReadAllText(@"E:\JTSGames\cwb_demo\Scenarios\004-620101-Williamsburg_W.scn");

        /*
        var match = Regex.Matches("1-40[100/15]", @"(\d+)-(\d+)");
        var match2 = Regex.Matches("1-40[100/15]", @"(\d+)/(\d+)");
        var match3 = Regex.Matches("100/15", @"(\d+)/(\d+)");
        var match4 = Regex.Matches("2", @"(\d+)/(\d+)");
        */

        // Console.WriteLine(cwbParser.ParseScenario(scenarioStr));

        scenarioStr = File.ReadAllText(@"E:\JTSGames\Mius43\#0717_01_Mius_Campaign.scn");
        //Console.WriteLine(pzcParser.ParseScenario(scenarioStr));

        // scenarioStr = File.ReadAllText(@"E:\JTSGames\Mius43\Mius.map");
        scenarioStr = File.ReadAllText(@"E:\JTSGames\Tobruk_41\Scenarios\Tobruk_Winter.map");
        // scenarioStr = File.ReadAllText(@"E:\JTSGames\Tobruk_41\Scenarios\Mersa_Brega_Sub.map");
        // Console.WriteLine(pzcParser.ParseMap(scenarioStr));
    }
}

