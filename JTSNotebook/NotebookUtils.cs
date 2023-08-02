namespace YYZ.JTS.Notebook
{
    using Plotly.NET.CSharp;
    using System;
    using System.Collections.Generic;
    using YYZ.JTS;
    using System.Linq;

    public static class NB // Notebook Utils
    {
        public static string[] RoadColors = new[]{"gray", "yellow", "red", "black"};
        public static List<Plotly.NET.GenericChart.GenericChart> GetCharts() => new();

        public static Dictionary<string, int[]> TerrainColorMap = new()
        {
            {"Water", new[]{50, 50, 255}},
            {"Forest", new[]{10, 255, 10}},
            {"Orchard", new[]{50, 250, 50}},
            {"Rough", new[]{100, 100, 50}}
        };

        public static int[] DefaultTerrainColor = new int[]{100, 200, 100};

        public static int[][][] CreateColorMatrix(int height, int width)
        {
            var colorMat = new int[height][][];
            for(var i=0; i<height; i++)
                colorMat[i] = new int[width][];
            return colorMat;
        }

        public static Plotly.NET.Color GetColor(string s) => Plotly.NET.Color.fromString(s);

        public static Plotly.NET.GenericChart.GenericChart CreateTerrainChart(MapFile map)
        {
            var colorMat = CreateColorMatrix(height: map.Height, width: map.Width);
            for(var i=0; i<map.Height; i++)
            {
                for(var j=0; j<map.Width; j++)
                {
                    if(!TerrainColorMap.TryGetValue(map.TerrainMap[i, j].Name, out var color))
                        color = DefaultTerrainColor;
                    colorMat[i][j] = color;
                }
            }
            return Chart.Image<int>(colorMat);
        }

        public static IEnumerable<Plotly.NET.GenericChart.GenericChart> CreateRoadCharts(MapFile map, HexNetwork network)
        {
            foreach((var roadType, var color) in map.CurrentTerrainSystem.Road.Terrains.Zip(RoadColors))
            {
                foreach(var road in network.SimplifyRoad(roadType))
                {
                    var xl = road.Select(node => node.X).ToArray();
                    var yl = road.Select(node => node.Y).ToArray();
                    var chart = Chart.Line<int, int, string>(x: xl, y:yl, LineColor: GetColor(color));
                    yield return chart;
                }
            }
        }

        // public static 

    }
}

