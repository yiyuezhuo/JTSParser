namespace YYZ.JTS.AI
{
    using System;

    /// <summary>
    /// Lanchester's Quadrotic Law
    /// \frac{R_0^2-R^2(t)}{B_0^2-B^2(t)}=\frac{\beta}{\alpha}
    /// </summary>
    public class LanchesterQuadroticSolution
    {
        public float Red0;
        public float Blue0;
        public float RedCoef = 1;
        public float BlueCoef = 1;

        public float RedCurrent;
        public float BlueCurrent;

        public float RedLoss{get => Red0 - RedCurrent;}
        public float BlueLoss{get => Blue0 - BlueCurrent;}
        
        public static float Solve(float blue0, float blueCoef, float blueCurrent, float red0, float redCoef)
        {
            var redCurrent = MathF.Sqrt(red0*red0 - (blueCoef / redCoef) * (blue0*blue0 - blueCurrent*blueCurrent));
            return redCurrent;
        }

        public float SolveRedCurrent(float blueCurrent)
        {
            return Solve(Blue0, BlueCoef, blueCurrent, Red0, RedCoef);
        }

        public float SolveBlueCurrent(float redCurrent)
        {
            return Solve(Red0, RedCoef, redCurrent, Blue0, BlueCoef);
        }
        

        public void FightToMinPercent(float p)
        {
            var blueCurrent = SolveBlueCurrent(Red0 * p);
            if(blueCurrent / Blue0 > p)
            {
                BlueCurrent = blueCurrent;
                RedCurrent = Red0 * p;
            }
            else
            {
                BlueCurrent = Blue0 * p;
                RedCurrent = SolveRedCurrent(BlueCurrent);
            }
        }

        public override string ToString() => $"LQS(Red0={Red0}, RedCoef={RedCoef}, RedCurrent={RedCurrent}, RedLoss={RedLoss}, Blue0={Blue0}, BlueCoef={BlueCoef}, BlueCurrent={BlueCurrent}, BlueLoss={BlueLoss})";
    }
}