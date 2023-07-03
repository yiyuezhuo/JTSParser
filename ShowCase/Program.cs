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

        // var s = File.ReadAllText(@"E:\JTSGames\Pen_spain\OOBs\Coruna.oob");
        var oobStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\OOBs\1st Bull Run.oob");
        var scenarioStr = File.ReadAllText(@"E:\JTSGames\CampaignAntietam\Scenarios\002 1BR_Bull Run (Historical).scn");

        // var units = JTSOobParser.ParseUnits(s);
        var scenario = new JTSScenario();
        scenario.Extract(scenarioStr);

        var units = JTSOobParser.FromCode("CWB").ParseUnits(oobStr);

        var unitStatus = new JTSUnitStates();
        unitStatus.ExtractByLines(units, scenario.DynamicCommandBlock);
        
        // Console.WriteLine('9' - '0');
        // Console.WriteLine('a' - '0');

        var mapStr = File.ReadAllText(@"E:\JTSGames\Pen_spain\Maps\Coruna.map");
        var mapFile = MapFile.Parse(mapStr);
        Console.WriteLine(mapFile.ToString());

        var graph = InfantryColumnGraph.FromMapFile(mapFile);
        Console.WriteLine(graph.ToString());

        Console.WriteLine(graph.HexMat[0, 0]);
        Console.WriteLine(graph.HexMat[1, 0]);

        Console.WriteLine(graph.Neighbors(graph.HexMat[0, 0]));
        Console.WriteLine(graph.MoveCost(graph.HexMat[0, 0], graph.HexMat[0, 1]));
        Console.WriteLine(graph.EstimateCost(graph.HexMat[0, 0], graph.HexMat[0, 2]));

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
    }
}

