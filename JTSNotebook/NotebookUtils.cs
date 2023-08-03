namespace YYZ.JTS.Notebook
{
    // using Plotly.NET.CSharp;
    // using static Plotly.NET.GenericChart;
    using Plotly.NET; // Plotly.NET.CSharp don't include `WithLayout` for some reason

    using System;
    using System.Collections.Generic;
    using YYZ.JTS;
    using System.Linq;
    using DynamicObj;

    public static class NB // Notebook Utils
    {
        static Random rand = new(43);

        public static string[] RoadColors = new[]{"gray", "yellow", "red", "black"};
        public static List<GenericChart.GenericChart> GetCharts() => new();

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
        public static float[][] CreateCountourMatrix(int height, int width)
        {
            var mat = new float[height][];
            for(var i=0; i<height; i++)
                mat[i] = new float[width];
            return mat;
        }
        public static float[][] CreateCountourMatrix(float[,] mat)
        {
            var height = mat.GetLength(0);
            var width = mat.GetLength(1);
            var z = CreateCountourMatrix(height, width);
            for(var i=0; i<height; i++)
                for(var j=0; j<width; j++)
                    z[i][j] = mat[i,j];
            return z;
        }

        public static IEnumerable<GenericChart.GenericChart> PlotsUnitsGroupByCountry(this JTSUnitStates unitStates)
        {
            foreach((var name, var states) in unitStates.GroupByCountry())
            {
                var x = states.Select(s => s.X);
                var y = states.Select(s => s.Y);
                // var chart = Plotly.NET.CSharp.Chart.Point<int, int, string>(x:x, y:y);
                var trace = new Trace("scatter");
                trace.SetValue("x", x);
                trace.SetValue("y", y);
                trace.SetValue("mode", "markers");
                var chart = GenericChart.ofTraceObject(false, trace);

                yield return chart;
            }
        }

        public static Plotly.NET.Color GetColor(string s) => Plotly.NET.Color.fromString(s);

        public static GenericChart.GenericChart Plot(this MapFile map)
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
            return Plotly.NET.CSharp.Chart.Image<int>(colorMat);
        }

        public static IEnumerable<GenericChart.GenericChart> Plots(this HexNetwork network)
        {
            foreach((var roadType, var color) in network.TerrainSystem.Road.Terrains.Zip(RoadColors))
            {
                foreach(var road in network.SimplifyRoad(roadType))
                {
                    var xl = road.Select(node => node.X).ToArray();
                    var yl = road.Select(node => node.Y).ToArray();
                    var chart = Plotly.NET.CSharp.Chart.Line<int, int, string>(x: xl, y:yl, LineColor: GetColor(color));
                    yield return chart;
                }
            }
        }

        public static GenericChart.GenericChart PlotMap(this SegmentGraph segGraph)
        {
            (var maxI, var maxJ) = ComputeBoundBox(segGraph);
            var colorMat = NB.CreateColorMatrix(height: maxI, width: maxJ);

            foreach(var seg in segGraph.Segments)
            {
                var color = new int[]{rand.Next(256), rand.Next(256), rand.Next(256)};
                foreach(var node in seg.Nodes)
                    colorMat[node.I][node.J] = color;
            }
            return Plotly.NET.CSharp.Chart.Image<int>(Z:colorMat);
        }

        public static (int, int) ComputeBoundBox(this SegmentGraph segGraph)
        {
            var maxI = int.MinValue;
            var maxJ = int.MinValue;

            foreach(var seg in segGraph.Segments)
            {
                foreach(var node in seg.Nodes)
                {
                    maxI = Math.Max(maxI, node.I);
                    maxJ = Math.Max(maxJ, node.J);
                }
            }
            return (maxI + 1, maxJ + 1);
        }

        public static IEnumerable<GenericChart.GenericChart> PlotEdges(this SegmentGraph segGraph)
        {
            foreach(var seg in segGraph.Segments)
            {
                foreach(var nei in seg.Neighbors)
                {
                    var line = Plotly.NET.CSharp.Chart.Line<float, float, string>(x: new []{seg.XMean, nei.XMean}, y: new[]{seg.YMean, nei.YMean});
                    yield return line;
                }
            }
        }

        public static IEnumerable<GenericChart.GenericChart> Plots(this SegmentGraph segGraph)
        {
            yield return segGraph.PlotMap();
            foreach(var chart in segGraph.PlotEdges())
                yield return chart;
        }

        public static DynamicObj CreateDyObj(Dictionary<string, object> dic)
        {
            var obj = new DynamicObj();
            Apply(obj, dic);
            return obj;
        }

        public static void Apply(DynamicObj obj, Dictionary<string, object> dic)
        {
            foreach((var key, var value) in dic)
                obj.SetValue(key, value);
        }

        public static GenericChart.GenericChart CreateContour(float[][] z)
        {
            var trace = new Plotly.NET.Trace("contour");
            trace.SetValue("z", z);

            var labelfont = CreateDyObj(new(){
                {"family", "Raleway"},
                {"size", 12},
                {"color", "black"}
            });

            var q = CreateDyObj(new(){
                {"coloring", "lines"},
                {"showlabels", true},
                {"labelfont", labelfont}
            });

            trace.SetValue("contours", q);

            return GenericChart
                .ofTraceObject(false, trace);
        }

        public static Plotly.NET.LayoutObjects.LinearAxis GetReversedAxis()
        {
            var axis = new Plotly.NET.LayoutObjects.LinearAxis();
            axis.SetValue("autorange", "reversed");
            return axis;
        }

        public static Plotly.NET.LayoutObjects.LinearAxis GetEqualAspectAxis()
        {
            var axis = new Plotly.NET.LayoutObjects.LinearAxis();
            axis.SetValue("scaleanchor", "x");
            axis.SetValue("scaleratio", 1);
            return axis;
        }

        public static Plotly.NET.LayoutObjects.LinearAxis GetEqualAspectReversedAxis()
        {
            var axis = new Plotly.NET.LayoutObjects.LinearAxis();
            axis.SetValue("scaleanchor", "x");
            axis.SetValue("scaleratio", 1);
            axis.SetValue("autorange", "reversed");
            return axis;
        }

        public static DynamicObj Annotate(float x, float y, float ax, float ay, string text)
        {
            /*
            var layout = new Layout();
            layout.SetValue("annotations", new[]{annotation});
            controller.Map.Plot().WithLayout(layout)
            */
            return CreateDyObj(new(){
                {"x", 50},
                {"y", 50},
                {"xref", "x"},
                {"yref", "y"},
                {"text", "Annotation Text"},
                {"showarrow", true},
                {"arrowhead", 7},
                {"ax", 25},
                {"ay", 25}
            });
        }

        public static GenericChart.GenericChart PlotArrow(float x, float y, float x2, float y2)
        {
            var marker = NB.CreateDyObj(new(){
                {"size", 10}, {"symbol", "arrow-bar-up"}, {"angleref", "previous"}
            });

            var trace = new Trace("scatter");
            trace.SetValue("x", new[]{x, x2});
            trace.SetValue("y", new[]{y, y2});
            trace.SetValue("marker", marker);
            trace.SetValue("line", NB.CreateDyObj(new(){
                {"color", "rgb(55, 128, 191)"},
                {"width", 1}
            }));

            return GenericChart.ofTraceObject(false, trace);
        }
    }
}

