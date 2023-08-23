using YYZ.JTS;
using System;
using YYZ.PathFinding;
using YYZ.PathFinding2;
using System.Collections.Generic;

namespace YYZ.JTS.AI
{
    public static class JTSAI // This class is expected to be used with `using static`
    {
        public static float[,] Zeros(int height, int width) => new float[height, width];
        public static float[,] Zeros(float[,] mat) => Zeros(mat.GetLength(0), mat.GetLength(1));
        public static float[,] CreateArray2D(int height, int width, Func<int, int, float> f)
        {
            var mat = Zeros(height, width);
            for(var i=0; i<height; i++)
                for(var j=0; j<width; j++)
                    mat[i, j] = f(i, j);
            return mat;
        }
        public static float[,] CreateArray2D(float[,] mat, Func<int, int, float> f) =>
            CreateArray2D(mat.GetLength(0), mat.GetLength(1), f);

        public static float[,] ComputeEngagementMat(float[,] friendlyMat, float[,] enemyMat) =>
            CreateArray2D(friendlyMat, (i, j) => friendlyMat[i, j] * enemyMat[i, j]);

        public static float[,] ComputeControlMat(float[,] friendlyMat, float[,] enemyMat) =>
            CreateArray2D(friendlyMat, (i, j) => friendlyMat[i, j] - enemyMat[i, j]);

        public static float[,] ComputeControlMat(float[,] friendlyMat, float[,] enemyMat, float[,] vpMat) =>
            CreateArray2D(friendlyMat, (i, j) => vpMat[i, j] - 0.1f * friendlyMat[i, j] + 0.2f * enemyMat[i, j]);

        public static Dictionary<NetworkSegment, int> GetSegmentStrengthMap(GameState s, string countryName, Func<Hex, NetworkSegment> segmentGetter)
        {
            var segmentStrengthMap = new Dictionary<NetworkSegment, int>();

            foreach(var state in s.UnitStates.UnitStates)
            {
                if(countryName == state.OobItem.Country)
                {
                    var hex = s.Network.HexMat[state.I, state.J];
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