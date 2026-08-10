using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using MCSTVisualizer.Models;

namespace MCSTVisualizer.Services;

public static class EngineeringReportExporter
{
    public static void Save(
        string path,
        StressState state,
        StressState3D state3D,
        string stressUnit,
        double stressScale,
        double tauAllow,
        double safetyFactor)
    {
        using StreamWriter writer = new(path, false, Encoding.UTF8);
        writer.Write(BuildReport(state, state3D, stressUnit, stressScale, tauAllow, safetyFactor));
    }

    private static string BuildReport(StressState state, StressState3D state3D, string unit, double scale, double tauAllow, double safetyFactor)
    {
        var transformed = state.Transform(state.PhysicalAngleDegrees);
        double[] principal3D = state3D.PrincipalStresses();
        double maxShear3D = (principal3D[0] - principal3D[2]) / 2.0;
        double allowableLimit = tauAllow / Math.Max(1e-12, safetyFactor);

        StringBuilder html = new();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<title>MCST Engineering Stress Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("""
            :root { color-scheme: light; --ink: #172033; --muted: #5d6675; --line: #ccd4df; --soft: #eef4fb; --accent: #155e75; --result: #fff3c4; --danger: #c2410c; }
            * { box-sizing: border-box; }
            body { margin: 0; color: var(--ink); background: #ffffff; font-family: "Segoe UI", Arial, sans-serif; font-size: 12px; }
            main { max-width: 900px; margin: 0 auto; padding: 32px; }
            header { border-bottom: 3px solid var(--accent); padding-bottom: 14px; margin-bottom: 24px; }
            h1 { margin: 0 0 6px; font-size: 26px; letter-spacing: 0; }
            h2 { margin: 24px 0 8px; font-size: 17px; color: var(--accent); }
            .meta { color: var(--muted); display: grid; grid-template-columns: 120px 1fr; gap: 4px 12px; }
            table { width: 100%; border-collapse: collapse; margin-top: 8px; page-break-inside: avoid; }
            th, td { border: 1px solid var(--line); padding: 7px 9px; text-align: left; vertical-align: top; }
            th { background: var(--soft); font-weight: 600; }
            td.value { text-align: right; font-variant-numeric: tabular-nums; }
            tr.result td { background: var(--result); font-weight: 700; }
            tr.exceeded td.value { color: var(--danger); font-weight: 800; }
            .tensor { font-family: Consolas, "Courier New", monospace; white-space: pre; line-height: 1.45; padding: 12px; border: 1px solid var(--line); background: #f8fafc; }
            footer { margin-top: 28px; padding-top: 10px; border-top: 1px solid var(--line); color: var(--muted); font-size: 11px; }
            @page { margin: 0.55in; }
            @media print {
                main { max-width: none; padding: 0; }
                header { margin-bottom: 18px; }
                h2 { break-after: avoid; }
                table, .tensor { break-inside: avoid; }
            }
            """);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main>");
        html.AppendLine("<header>");
        html.AppendLine("<h1>MCST Engineering Stress Report</h1>");
        html.AppendLine("<div class=\"meta\">");
        html.AppendLine($"<span>Generated</span><span>{Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}</span>");
        html.AppendLine($"<span>Stress units</span><span>{Escape(unit)}</span>");
        html.AppendLine($"<span>Plane axes</span><span>{Escape(state.Axis1)} / {Escape(state.Axis2)}</span>");
        html.AppendLine("</div>");
        html.AppendLine("</header>");

        AppendSection(html, "Allowable Stress Check", [
            Row("tau_allow", tauAllow, unit, scale),
            Row("n", safetyFactor, string.Empty, 1.0),
            Row("tau_allow / n", allowableLimit, unit, scale)
        ]);

        AppendSection(html, "2D Plane Stress Inputs", [
            Row($"sigma_{state.Axis1}", state.SigmaX, unit, scale),
            Row($"sigma_{state.Axis2}", state.SigmaY, unit, scale),
            Row($"tau_{state.Axis1}{state.Axis2}", state.TauXY, unit, scale),
            Row("theta", state.PhysicalAngleDegrees, "deg", 1.0)
        ]);

        AppendSection(html, "2D Resultant Stresses", [
            Row("sigma_ave", state.SigmaAverage, unit, scale, true, allowableLimit),
            Row("R", state.Radius, unit, scale, true, allowableLimit),
            Row("tau_max", state.TauMax, unit, scale, true, allowableLimit),
            Row("sigma_max", state.SigmaMax, unit, scale, true, allowableLimit),
            Row("sigma_min", state.SigmaMin, unit, scale, true, allowableLimit),
            Row("principal angle", state.PrincipalAngleDegrees, "deg", 1.0)
        ]);

        AppendSection(html, "2D Transformed Resultants", [
            Row($"sigma_{state.Axis1}'", transformed.SigmaXP, unit, scale, true, allowableLimit),
            Row($"sigma_{state.Axis2}'", transformed.SigmaYP, unit, scale, true, allowableLimit),
            Row($"tau_{state.Axis1}'{state.Axis2}'", transformed.TauXYP, unit, scale, true, allowableLimit)
        ]);

        AppendSection(html, "3D Stress Tensor Inputs", [
            Row("sigma_x", state3D.SigmaX, unit, scale),
            Row("sigma_y", state3D.SigmaY, unit, scale),
            Row("sigma_z", state3D.SigmaZ, unit, scale),
            Row("tau_xy", state3D.TauXY, unit, scale),
            Row("tau_yz", state3D.TauYZ, unit, scale),
            Row("tau_zx", state3D.TauZX, unit, scale)
        ]);

        html.AppendLine("<h2>3D Stress Tensor Matrix</h2>");
        html.AppendLine("<div class=\"tensor\">");
        html.AppendLine(Escape($"[{Fmt(state3D.SigmaX, scale)}, {Fmt(state3D.TauXY, scale)}, {Fmt(state3D.TauZX, scale)}]"));
        html.AppendLine(Escape($"[{Fmt(state3D.TauXY, scale)}, {Fmt(state3D.SigmaY, scale)}, {Fmt(state3D.TauYZ, scale)}]"));
        html.AppendLine(Escape($"[{Fmt(state3D.TauZX, scale)}, {Fmt(state3D.TauYZ, scale)}, {Fmt(state3D.SigmaZ, scale)}] {unit}"));
        html.AppendLine("</div>");

        AppendSection(html, "3D Principal and Resultant Stresses", [
            Row("sigma_1", principal3D[0], unit, scale, true, allowableLimit),
            Row("sigma_2", principal3D[1], unit, scale, true, allowableLimit),
            Row("sigma_3", principal3D[2], unit, scale, true, allowableLimit),
            Row("tau_max", maxShear3D, unit, scale, true, allowableLimit),
            Row("sigma_mean", state3D.MeanStress, unit, scale, true, allowableLimit)
        ]);

        html.AppendLine("<footer>Resultant stress rows are highlighted for review and values exceeding tau_allow / n are shown in red.</footer>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static ReportRow Row(string label, double value, string unit, double scale, bool result = false, double? allowableLimit = null)
    {
        bool exceeded = result && allowableLimit.HasValue && unit != "deg" && Math.Abs(value) > allowableLimit.Value;
        return new ReportRow(label, Fmt(value, scale), unit, result, exceeded);
    }

    private static void AppendSection(StringBuilder html, string title, IEnumerable<ReportRow> rows)
    {
        html.AppendLine($"<h2>{Escape(title)}</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<thead><tr><th>Parameter</th><th>Value</th><th>Unit</th></tr></thead>");
        html.AppendLine("<tbody>");
        foreach (ReportRow row in rows)
        {
            string rowClass = string.Join(" ", new[] { row.IsResult ? "result" : string.Empty, row.IsExceeded ? "exceeded" : string.Empty }.Where(value => value.Length > 0));
            string classAttribute = rowClass.Length > 0 ? $" class=\"{rowClass}\"" : string.Empty;
            html.AppendLine($"<tr{classAttribute}><td>{Escape(row.Label)}</td><td class=\"value\">{Escape(row.Value)}</td><td>{Escape(row.Unit)}</td></tr>");
        }

        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
    }

    private static string Fmt(double value, double scale)
    {
        return (value * scale).ToString("G10", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private sealed record ReportRow(string Label, string Value, string Unit, bool IsResult, bool IsExceeded);
}
