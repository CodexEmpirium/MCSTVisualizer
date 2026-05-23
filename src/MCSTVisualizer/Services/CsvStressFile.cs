using System.Globalization;
using System.IO;
using MCSTVisualizer.Models;

namespace MCSTVisualizer.Services;

public static class CsvStressFile
{
    public static void Save(string path, StressState state)
    {
        using StreamWriter writer = new(path, false);
        writer.WriteLine("parameter,value");
        Write(writer, "axis_1", state.Axis1);
        Write(writer, "axis_2", state.Axis2);
        Write(writer, "sigma_x", state.SigmaX);
        Write(writer, "sigma_y", state.SigmaY);
        Write(writer, "tau_xy", state.TauXY);
        Write(writer, "sigma_ave", state.SigmaAverage);
        Write(writer, "R", state.Radius);
        Write(writer, "tau_max", state.TauMax);
        Write(writer, "sigma_max", state.SigmaMax);
        Write(writer, "sigma_min", state.SigmaMin);
        Write(writer, "physical_angle_degrees", state.PhysicalAngleDegrees);
        var transformed = state.Transform(state.PhysicalAngleDegrees);
        Write(writer, "sigma_x_prime", transformed.SigmaXP);
        Write(writer, "sigma_y_prime", transformed.SigmaYP);
        Write(writer, "tau_x_prime_y_prime", transformed.TauXYP);
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

            string[] parts = rawLine.Split(',', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            string key = parts[0].Trim();
            string value = parts[1].Trim();
            if (key == "axis_1") state.Axis1 = value;
            else if (key == "axis_2") state.Axis2 = value;
            else if (TryParse(value, out double number))
            {
                if (key == "sigma_x") state.SigmaX = number;
                else if (key == "sigma_y") state.SigmaY = number;
                else if (key == "tau_xy") state.TauXY = number;
                else if (key == "physical_angle_degrees") state.PhysicalAngleDegrees = number;
            }
        }

        return state;
    }

    private static void Write(StreamWriter writer, string parameter, double value)
    {
        writer.WriteLine($"{parameter},{value.ToString("G17", CultureInfo.InvariantCulture)}");
    }

    private static void Write(StreamWriter writer, string parameter, string value)
    {
        writer.WriteLine($"{parameter},{value}");
    }

    private static bool TryParse(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
    }
}
