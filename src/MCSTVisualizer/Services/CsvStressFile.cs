using System.Globalization;
using System.IO;
using MCSTVisualizer.Models;

namespace MCSTVisualizer.Services;

public enum CsvAngleUnit
{
    Degrees,
    Radians
}

public static class CsvStressFile
{
    public static void Save(string path, StressState state)
    {
        Save(path, state, "GPa", 1.0);
    }

    public static void Save(string path, StressState state, string stressUnit, double stressScale, CsvAngleUnit angleUnit = CsvAngleUnit.Degrees)
    {
        using StreamWriter writer = new(path, false);
        writer.WriteLine("parameter,value,unit");
        Write(writer, "axis1", state.Axis1, string.Empty);
        Write(writer, "axis2", state.Axis2, string.Empty);
        Write(writer, "sigma_x", state.SigmaX * stressScale, stressUnit);
        Write(writer, "sigma_y", state.SigmaY * stressScale, stressUnit);
        Write(writer, "tau_xy", state.TauXY * stressScale, stressUnit);
        Write(writer, "sigma_ave", state.SigmaAverage * stressScale, stressUnit);
        Write(writer, "R", state.Radius * stressScale, stressUnit);
        Write(writer, "tau_max", state.TauMax * stressScale, stressUnit);
        Write(writer, "sigma_max", state.SigmaMax * stressScale, stressUnit);
        Write(writer, "sigma_min", state.SigmaMin * stressScale, stressUnit);
        WriteAngle(writer, state.PhysicalAngleDegrees, angleUnit);
        var transformed = state.Transform(state.PhysicalAngleDegrees);
        Write(writer, "sigma_x'", transformed.SigmaXP * stressScale, stressUnit);
        Write(writer, "sigma_y'", transformed.SigmaYP * stressScale, stressUnit);
        Write(writer, "tau_x'y'", transformed.TauXYP * stressScale, stressUnit);
    }

    public static StressState Open(string path)
    {
        StressState state = new();
        foreach (string rawLine in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            string[] parts = rawLine.Split(',', 3);
            if (parts.Length < 2)
            {
                continue;
            }

            string key = parts[0].Trim();
            string value = parts[1].Trim();
            string unit = parts.Length == 3 ? parts[2].Trim() : string.Empty;
            if (key == "axis1" || key == "axis_1") state.Axis1 = value;
            else if (key == "axis2" || key == "axis_2") state.Axis2 = value;
            else if (TryParse(value, out double number))
            {
                if (key == "sigma_x") state.SigmaX = FromStressUnit(number, unit);
                else if (key == "sigma_y") state.SigmaY = FromStressUnit(number, unit);
                else if (key == "tau_xy") state.TauXY = FromStressUnit(number, unit);
                else if (key == "physical_angle" || key == "physical_angle_degrees") state.PhysicalAngleDegrees = FromAngleUnit(number, unit);
            }
        }

        return state;
    }

    private static void Write(StreamWriter writer, string parameter, double value, string unit)
    {
        writer.WriteLine($"{parameter},{value.ToString("G17", CultureInfo.InvariantCulture)},{unit}");
    }

    private static void Write(StreamWriter writer, string parameter, string value, string unit)
    {
        writer.WriteLine($"{parameter},{value},{unit}");
    }

    private static void WriteAngle(StreamWriter writer, double degrees, CsvAngleUnit angleUnit)
    {
        if (angleUnit == CsvAngleUnit.Radians)
        {
            Write(writer, "physical_angle", degrees * Math.PI / 180.0, "rad");
        }
        else
        {
            Write(writer, "physical_angle", degrees, "deg");
        }
    }

    private static bool TryParse(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
    }

    private static double FromStressUnit(double value, string unit)
    {
        return unit.Trim().ToLowerInvariant() switch
        {
            "psi" => value / 145037.73773,
            "ksi" => value / 145.03773773,
            "mpa" => value / 1000.0,
            "kpa" => value / 1_000_000.0,
            _ => value
        };
    }

    private static double FromAngleUnit(double value, string unit)
    {
        return unit.Trim().ToLowerInvariant() == "rad"
            ? value * 180.0 / Math.PI
            : value;
    }
}
