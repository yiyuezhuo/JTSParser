namespace YYZ.JTS.NB
{
    using YYZ.AI;
    using YYZ.PathFinding;
    using System.Collections.Generic;


    public class InfluenceController
    {
        /*
        public List<UnitState> UnitStates;
        public InfantryColumnGraph Graph;
        public InfluenceMap<Hex> Map = new();

        public void Setup()
        {
            foreach(var unitState in UnitStates)
            {
                unitState.CurrentStrength\
            }
        }
        */

        public static InfluenceMap<Hex> Setup(List<UnitState> unitStates, InfantryColumnGraph graph, float momentum, float decay, int step=0)
        {
            var map = new InfluenceMap<Hex>(){Graph=graph, Momentum=momentum, Decay=decay};
            foreach(var node in graph.Nodes())
            {
                map.InfluenceDict[node] = 0;
            }
            foreach(var unitState in unitStates)
            {
                var node = graph.HexMat[unitState.Y, unitState.X];
                map.InfluenceDict[node] = unitState.CurrentStrength;
            }
            for(var i=0; i<step; i++)
            {
                map.Step();
            }
            return map;
        }
    }
}