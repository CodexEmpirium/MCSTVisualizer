namespace MCSTVisualizer.Models;

public sealed class StressState3D
{
    public double SigmaX { get; set; } = 0.16;
    public double SigmaY { get; set; } = -0.08;
    public double SigmaZ { get; set; } = 0.04;
    public double TauXY { get; set; } = 0.035;
    public double TauYZ { get; set; } = -0.025;
    public double TauZX { get; set; } = 0.045;

    public double[,] Matrix => new[,]
    {
        { SigmaX, TauXY, TauZX },
        { TauXY, SigmaY, TauYZ },
        { TauZX, TauYZ, SigmaZ }
    };

    public double MeanStress => (SigmaX + SigmaY + SigmaZ) / 3.0;

    public double[] PrincipalStresses()
    {
        double[,] a = Matrix;
        const int maxIterations = 40;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            int p = 0;
            int q = 1;
            double largest = Math.Abs(a[0, 1]);

            if (Math.Abs(a[0, 2]) > largest)
            {
                p = 0;
                q = 2;
                largest = Math.Abs(a[0, 2]);
            }

            if (Math.Abs(a[1, 2]) > largest)
            {
                p = 1;
                q = 2;
                largest = Math.Abs(a[1, 2]);
            }

            if (largest < 1e-12)
            {
                break;
            }

            double app = a[p, p];
            double aqq = a[q, q];
            double apq = a[p, q];
            double phi = 0.5 * Math.Atan2(2.0 * apq, aqq - app);
            double c = Math.Cos(phi);
            double s = Math.Sin(phi);

            for (int k = 0; k < 3; k++)
            {
                if (k == p || k == q)
                {
                    continue;
                }

                double akp = a[k, p];
                double akq = a[k, q];
                a[k, p] = c * akp - s * akq;
                a[p, k] = a[k, p];
                a[k, q] = s * akp + c * akq;
                a[q, k] = a[k, q];
            }

            a[p, p] = c * c * app - 2.0 * s * c * apq + s * s * aqq;
            a[q, q] = s * s * app + 2.0 * s * c * apq + c * c * aqq;
            a[p, q] = 0.0;
            a[q, p] = 0.0;
        }

        double[] principal = [a[0, 0], a[1, 1], a[2, 2]];
        Array.Sort(principal);
        Array.Reverse(principal);
        return principal;
    }
}
