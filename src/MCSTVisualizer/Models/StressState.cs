namespace MCSTVisualizer.Models;

public sealed class StressState
{
    public string Axis1 { get; set; } = "x";
    public string Axis2 { get; set; } = "y";
    public double SigmaX { get; set; } = 0.1;
    public double SigmaY { get; set; } = -0.1;
    public double TauXY { get; set; } = 0.0;
    public double PhysicalAngleDegrees { get; set; } = 0.0;

    public double SigmaAverage => (SigmaX + SigmaY) / 2.0;
    public double HalfDifference => (SigmaX - SigmaY) / 2.0;
    public double Radius => Math.Sqrt((HalfDifference * HalfDifference) + (TauXY * TauXY));
    public double TauMax => Radius;
    public double SigmaMax => SigmaAverage + Radius;
    public double SigmaMin => SigmaAverage - Radius;
    public double PrincipalAngleDegrees => 0.5 * Degrees(Math.Atan2(2.0 * TauXY, SigmaX - SigmaY));

    public StressState Clone() => new()
    {
        Axis1 = Axis1,
        Axis2 = Axis2,
        SigmaX = SigmaX,
        SigmaY = SigmaY,
        TauXY = TauXY,
        PhysicalAngleDegrees = PhysicalAngleDegrees
    };

    public (double SigmaXP, double SigmaYP, double TauXYP) Transform(double physicalAngleDegrees)
    {
        double theta = Radians(physicalAngleDegrees);
        double cos2 = Math.Cos(2.0 * theta);
        double sin2 = Math.Sin(2.0 * theta);
        double sigmaXp = SigmaAverage + HalfDifference * cos2 + TauXY * sin2;
        double sigmaYp = SigmaAverage - HalfDifference * cos2 - TauXY * sin2;
        double tauXyp = -HalfDifference * sin2 + TauXY * cos2;
        return (sigmaXp, sigmaYp, tauXyp);
    }

    public void SetAverageAndRadius(double sigmaAverage, double radius)
    {
        double angle = Math.Atan2(TauXY, HalfDifference);
        double r = Math.Max(0.0, Math.Abs(radius));
        double half = r * Math.Cos(angle);
        double tau = r * Math.Sin(angle);
        SigmaX = sigmaAverage + half;
        SigmaY = sigmaAverage - half;
        TauXY = tau;
    }

    public void SetRadius(double radius)
    {
        SetAverageAndRadius(SigmaAverage, radius);
    }

    public void SetPrincipalExtremes(double sigmaMin, double sigmaMax)
    {
        double average = (sigmaMin + sigmaMax) / 2.0;
        double radius = Math.Abs(sigmaMax - sigmaMin) / 2.0;
        SetAverageAndRadius(average, radius);
    }

    public static double Radians(double degrees) => Math.PI * degrees / 180.0;
    public static double Degrees(double radians) => 180.0 * radians / Math.PI;
}
