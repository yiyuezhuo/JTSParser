namespace YYZ.JTS
{
    using System.Linq;
    using System;
    using System.Collections.Generic;
    
    public class HexPosition
    {
        public int X;
        public int Y;
        public int I{get => Y;}
        public int J{get => X;}
        public float YCorrected{get => X % 2 == 0 ? Y : Y - 0.5f;}
        
        public override int GetHashCode()
        {
            int hash = 23;
            hash = hash * 37 + X;
            hash = hash * 37 + Y;
            return hash;
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj as HexPosition);
        }
        public bool Equals(HexPosition hex)
        {
            return X == hex.X && Y == hex.Y;
        }
    }

    public static class Utils
    {
        public static T MinBy<T>(IEnumerable<T> l, Func<T, float> f)
        {
            
            var minValue = float.PositiveInfinity;
            var minEl = default(T);
            foreach(var el in l)
            {
                var fEl = f(el);
                if(fEl < minValue)
                {
                    minValue = fEl;
                    minEl = el;
                }
            }
            return minEl;   
        }

        public static int MinIndexBy<T>(IEnumerable<T> l, Func<T, float> f)
        {
            var minValue = float.PositiveInfinity;
            var minIdx = -1;
            var idx = 0;
            foreach(var el in l)
            {
                var fEl = f(el);
                if(fEl < minValue)
                {
                    minValue = fEl;
                    minIdx = idx;
                }
                idx++;
            }
            return minIdx;
        }

        public static float Distance2(float x1, float y1, float x2, float y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            return dx*dx + dy*dy;
        }

        public static float Distance(float x1, float y1, float x2, float y2) => MathF.Sqrt(Distance2(x1, y1, x2, y2));
    }
}