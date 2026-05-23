using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MCSTVisualizer.Models;
using MCSTVisualizer.Services;

namespace MCSTVisualizer;

public partial class MainWindow : Window
{
    private enum DragTarget
    {
        None,
        SigmaMin,
        SigmaMax,
        SigmaX,
        SigmaY,
        TauXY,
        TauMax,
        AngleLine,
        StressTensorDiagram
    }

    private const double StressLimit = 1000.0;
    private const double DragMinimumRadius = 0.000001;
    private const double AngleSnapToleranceDegrees = 1.0;
    private const double PsiPerGPa = 145037.73773;

    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private StressState _state = new();
    private StressDisplayUnit _displayUnit = StressDisplayUnit.MPa;
    private bool _isUpdatingUi = true;
    private DragTarget _dragTarget = DragTarget.None;
    private string? _currentPath;

    private Point _mohrCenter;
    private double _mohrScale = 1.0;
    private double _mohrDomainMin;
    private double _mohrDomainMax;
    private double _mohrStressPerPixel = 1.0;
    private double _stressTensorBaseAngle;
    private Point _dragStartPoint;
    private StressState _dragStartState = new();

    private enum StressDisplayUnit
    {
        Psi,
        Ksi,
        GPa,
        MPa,
        KPa
    }

    public MainWindow()
    {
        InitializeComponent();
        _isUpdatingUi = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SyncUiFromState();
    }

    private void SyncUiFromState()
    {
        ClampStressState();
        _isUpdatingUi = true;
        Axis1Box.Text = _state.Axis1;
        Axis2Box.Text = _state.Axis2;
        StressUnitBox.SelectedIndex = _displayUnit switch
        {
            StressDisplayUnit.Ksi => 1,
            StressDisplayUnit.KPa => 2,
            StressDisplayUnit.MPa => 3,
            StressDisplayUnit.GPa => 4,
            _ => 0
        };
        StressValuesHeader.Text = $"Stress Values ({UnitLabel})";
        SigmaXLabel.Text = $"{Sigma(_state.Axis1)} ({UnitLabel})";
        SigmaYLabel.Text = $"{Sigma(_state.Axis2)} ({UnitLabel})";
        TauXYLabel.Text = $"{Tau(_state.Axis1, _state.Axis2)} ({UnitLabel})";
        SigmaAveLabel.Text = $"{Sigma("ave")} ({UnitLabel})";
        TauMaxLabel.Text = $"{Tau("max")} ({UnitLabel})";
        ThetaLabel.Text = "θ, degrees";
        SigmaXBox.Text = FormatStress(_state.SigmaX);
        SigmaYBox.Text = FormatStress(_state.SigmaY);
        TauXYBox.Text = FormatStress(_state.TauXY);
        SigmaAveBox.Text = FormatStress(_state.SigmaAverage);
        RadiusBox.Text = FormatStress(_state.Radius);
        TauMaxBox.Text = FormatStress(_state.TauMax);
        ThetaBox.Text = Format(_state.PhysicalAngleDegrees);
        var transformed = _state.Transform(_state.PhysicalAngleDegrees);
        DerivedText.Text =
            $"{Sigma("max")} = {FormatStress(_state.SigmaMax)} {UnitLabel}\n" +
            $"{Sigma("min")} = {FormatStress(_state.SigmaMin)} {UnitLabel}\n" +
            $"principal angle = {Format(_state.PrincipalAngleDegrees)} deg\n" +
            $"{SigmaPrime(_state.Axis1)} = {FormatStress(transformed.SigmaXP)} {UnitLabel}\n" +
            $"{SigmaPrime(_state.Axis2)} = {FormatStress(transformed.SigmaYP)} {UnitLabel}\n" +
            $"{TauPrime(_state.Axis1, _state.Axis2)} = {FormatStress(transformed.TauXYP)} {UnitLabel}";
        _isUpdatingUi = false;

        DrawMohrCircle();
        DrawStressTensorDiagram();
    }

    private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi)
        {
            return;
        }

        if (!TryRead(SigmaXBox, out double sigmaX) ||
            !TryRead(SigmaYBox, out double sigmaY) ||
            !TryRead(TauXYBox, out double tauXY))
        {
            return;
        }

        _state.SigmaX = ClampStress(FromDisplayUnit(sigmaX));
        _state.SigmaY = ClampStress(FromDisplayUnit(sigmaY));
        _state.TauXY = ClampStress(FromDisplayUnit(tauXY));

        if (sender == SigmaAveBox && TryRead(SigmaAveBox, out double average))
        {
            double half = _state.HalfDifference;
            double averageGPa = FromDisplayUnit(average);
            _state.SigmaX = ClampStress(averageGPa + half);
            _state.SigmaY = ClampStress(averageGPa - half);
        }
        else if ((sender == RadiusBox || sender == TauMaxBox) && TryRead((TextBox)sender, out double radius))
        {
            _state.SetRadius(ClampRadiusForAverage(FromDisplayUnit(radius), _state.SigmaAverage));
            ClampStressState();
        }

        if (TryRead(ThetaBox, out double theta))
        {
            _state.PhysicalAngleDegrees = NormalizeDegrees(theta);
        }

        SyncUiFromState();
    }

    private void AxisChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAxesFromBoxes();
    }

    private void AxisLostFocus(object sender, RoutedEventArgs e)
    {
        UpdateAxesFromBoxes();
    }

    private void StressUnitBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || StressUnitBox is null)
        {
            return;
        }

        string? selectedUnit = (StressUnitBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? StressUnitBox.Text;
        _displayUnit = ParseDisplayUnit(selectedUnit);
        SyncUiFromState();
    }

    private void UpdateAxesFromBoxes()
    {
        if (_isUpdatingUi || Axis1Box is null || Axis2Box is null)
        {
            return;
        }

        _state.Axis1 = CleanAxis(Axis1Box.Text, "x");
        _state.Axis2 = CleanAxis(Axis2Box.Text, "y");
        SyncUiFromState();
    }

    private void DrawMohrCircle()
    {
        if (MohrCanvas.ActualWidth <= 20 || MohrCanvas.ActualHeight <= 20)
        {
            return;
        }

        MohrCanvas.Children.Clear();
        double width = MohrCanvas.ActualWidth;
        double height = MohrCanvas.ActualHeight;
        double pad = 42;
        double range = StressLimit;
        _mohrDomainMin = -StressLimit;
        _mohrDomainMax = StressLimit;
        _mohrScale = Math.Min((width - 2 * pad) / (_mohrDomainMax - _mohrDomainMin), (height - 2 * pad) / (2.0 * range));
        _mohrStressPerPixel = Math.Max(1e-12, (_mohrDomainMax - _mohrDomainMin) / Math.Max(1, width - 2 * pad));
        _mohrCenter = new Point(ToCanvasX(_state.SigmaAverage), height / 2.0);

        AddLine(MohrCanvas, pad, height / 2.0, width - pad / 2.0, height / 2.0, "#7D8998", 1.4);
        AddLine(MohrCanvas, ToCanvasX(0), pad / 2.0, ToCanvasX(0), height - pad / 2.0, "#C2CAD6", 1.0);
        AddText(MohrCanvas, $"normal stress ({UnitLabel})", width - 146, height / 2.0 + 10, 12, "#55606D");
        AddText(MohrCanvas, $"shear stress ({UnitLabel})", ToCanvasX(0) + 8, pad / 2.0, 12, "#55606D");

        double rPixels = VisualRadiusPixels();
        Ellipse circle = new()
        {
            Width = rPixels * 2,
            Height = rPixels * 2,
            Stroke = Brush("#286090"),
            StrokeThickness = 2.2,
            Fill = Brush("#EAF4FF", 0.35)
        };
        Canvas.SetLeft(circle, _mohrCenter.X - rPixels);
        Canvas.SetTop(circle, _mohrCenter.Y - rPixels);
        MohrCanvas.Children.Add(circle);

        Point originalPoint = CirclePoint(_state.HalfDifference, _state.TauXY);
        AddLine(MohrCanvas, _mohrCenter.X, _mohrCenter.Y, originalPoint.X, originalPoint.Y, "#7D8998", 1.2);
        AddPoint(MohrCanvas, originalPoint, "#596579", "current state");
        AddText(MohrCanvas, "current state", originalPoint.X + 8, originalPoint.Y + 8, 11, "#596579");

        Point sigmaXPoint = CirclePoint(_state.HalfDifference, 0);
        Point sigmaYPoint = CirclePoint(-_state.HalfDifference, 0);
        Point tauXYPoint = CirclePoint(0, _state.TauXY);
        AddLine(MohrCanvas, sigmaXPoint.X, height / 2.0 - 8, sigmaXPoint.X, height / 2.0 + 8, "#1B7F5A", 1.2);
        AddLine(MohrCanvas, sigmaYPoint.X, height / 2.0 - 8, sigmaYPoint.X, height / 2.0 + 8, "#286090", 1.2);
        AddLine(MohrCanvas, _mohrCenter.X - 8, tauXYPoint.Y, _mohrCenter.X + 8, tauXYPoint.Y, "#C2410C", 1.2);
        AddPoint(MohrCanvas, sigmaXPoint, "#1B7F5A", Sigma(_state.Axis1));
        AddPoint(MohrCanvas, sigmaYPoint, "#286090", Sigma(_state.Axis2));
        AddPoint(MohrCanvas, tauXYPoint, "#C2410C", Tau(_state.Axis1, _state.Axis2));
        AddText(MohrCanvas, Sigma(_state.Axis1), sigmaXPoint.X + 8, sigmaXPoint.Y - 24, 11, "#1B7F5A");
        AddText(MohrCanvas, Sigma(_state.Axis2), sigmaYPoint.X + 8, sigmaYPoint.Y + 12, 11, "#286090");
        AddText(MohrCanvas, Tau(_state.Axis1, _state.Axis2), tauXYPoint.X + 12, tauXYPoint.Y - 6, 11, "#C2410C");

        Point minPoint = CirclePoint(-_state.Radius, 0);
        Point maxPoint = CirclePoint(_state.Radius, 0);
        Point tauPoint = CirclePoint(0, _state.TauMax);
        AddPoint(MohrCanvas, minPoint, "#7B2CBF", Sigma("min"));
        AddPoint(MohrCanvas, maxPoint, "#1B7F5A", Sigma("max"));
        AddPoint(MohrCanvas, tauPoint, "#C2410C", Tau("max"));
        AddText(MohrCanvas, Sigma("min"), minPoint.X - 44, minPoint.Y + 10, 12, "#7B2CBF");
        AddText(MohrCanvas, Sigma("max"), maxPoint.X + 8, maxPoint.Y + 10, 12, "#1B7F5A");
        AddText(MohrCanvas, Tau("max"), tauPoint.X + 8, tauPoint.Y - 18, 12, "#C2410C");

        double mohrAngle = StressState.Radians(MohrVisualAngleDegrees(_state.PhysicalAngleDegrees));
        Point anglePoint = new(
            _mohrCenter.X + Math.Cos(mohrAngle) * Math.Max(18, rPixels),
            _mohrCenter.Y - Math.Sin(mohrAngle) * Math.Max(18, rPixels));
        AddLine(MohrCanvas, _mohrCenter.X, _mohrCenter.Y, anglePoint.X, anglePoint.Y, "#E0A106", 2.0);
        AddPoint(MohrCanvas, anglePoint, "#E0A106", "angle");
        AddText(MohrCanvas, $"2θ = {Format(MohrVisualAngleDegrees(_state.PhysicalAngleDegrees))} deg", anglePoint.X + 8, anglePoint.Y - 6, 12, "#8A6500");
    }

    private void DrawStressTensorDiagram()
    {
        if (StressTensorCanvas.ActualWidth <= 20 || StressTensorCanvas.ActualHeight <= 20)
        {
            return;
        }

        StressTensorCanvas.Children.Clear();
        double width = StressTensorCanvas.ActualWidth;
        double height = StressTensorCanvas.ActualHeight;
        Point center = new(width / 2.0, height / 2.0 + 10);
        double side = Math.Min(width, height) * 0.34;
        double angle = StressState.Radians(MohrVisualAngleDegrees(_state.PhysicalAngleDegrees));
        var transformed = _state.Transform(_state.PhysicalAngleDegrees);

        Point[] corners =
        [
            Rotate(new Point(-side / 2, -side / 2), angle, center),
            Rotate(new Point(side / 2, -side / 2), angle, center),
            Rotate(new Point(side / 2, side / 2), angle, center),
            Rotate(new Point(-side / 2, side / 2), angle, center)
        ];

        Polygon element = new()
        {
            Points = new PointCollection(corners),
            Fill = Brush("#F7FBFF"),
            Stroke = Brush("#243143"),
            StrokeThickness = 2.0
        };
        StressTensorCanvas.Children.Add(element);

        Vector ex = new(Math.Cos(angle), Math.Sin(angle));
        Vector ey = new(-Math.Sin(angle), Math.Cos(angle));
        DrawStressArrow(center + ex * (side / 2 + 8), ex, transformed.SigmaXP, SigmaPrime(_state.Axis1));
        DrawStressArrow(center - ex * (side / 2 + 8), -ex, transformed.SigmaXP, SigmaPrime(_state.Axis1));
        DrawStressArrow(center + ey * (side / 2 + 8), ey, transformed.SigmaYP, SigmaPrime(_state.Axis2));
        DrawStressArrow(center - ey * (side / 2 + 8), -ey, transformed.SigmaYP, SigmaPrime(_state.Axis2));

        DrawShearArrow(center + ey * (side / 2 + 26), ex, transformed.TauXYP);
        DrawShearArrow(center - ey * (side / 2 + 26), -ex, transformed.TauXYP);
        DrawShearArrow(center + ex * (side / 2 + 26), -ey, transformed.TauXYP);
        DrawShearArrow(center - ex * (side / 2 + 26), ey, transformed.TauXYP);

        AddText(StressTensorCanvas, $"θ = {Format(_state.PhysicalAngleDegrees)} deg", 18, 14, 13, "#55606D");
        AddText(StressTensorCanvas, $"{SigmaPrime(_state.Axis1)} = {FormatStress(transformed.SigmaXP)} {UnitLabel}", 18, height - 74, 13, "#1B7F5A");
        AddText(StressTensorCanvas, $"{SigmaPrime(_state.Axis2)} = {FormatStress(transformed.SigmaYP)} {UnitLabel}", 18, height - 52, 13, "#286090");
        AddText(StressTensorCanvas, $"{TauPrime(_state.Axis1, _state.Axis2)} = {FormatStress(transformed.TauXYP)} {UnitLabel}", 18, height - 30, 13, "#C2410C");
    }

    private void MohrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(MohrCanvas);
        _dragTarget = HitTestMohr(p);
        if (_dragTarget != DragTarget.None)
        {
            _dragStartPoint = p;
            _dragStartState = _state.Clone();
            MohrCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MohrCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point p = e.GetPosition(MohrCanvas);
        double screenWidth = Math.Max(1.0, SystemParameters.PrimaryScreenWidth);
        double screenHeight = Math.Max(1.0, SystemParameters.PrimaryScreenHeight);
        double stressDeltaX = (p.X - _dragStartPoint.X) * (2.0 * StressLimit / screenWidth);
        double stressDeltaY = (_dragStartPoint.Y - p.Y) * (2.0 * StressLimit / screenHeight);
        double logRadiusDelta = (_dragStartPoint.Y - p.Y) / screenHeight * (Math.Log10(StressLimit) - Math.Log10(DragMinimumRadius));
        if (_dragTarget == DragTarget.SigmaMax)
        {
            double sigmaMax = ClampStress(_dragStartState.SigmaMax + stressDeltaX);
            _state = _dragStartState.Clone();
            if (Math.Abs(sigmaMax - _dragStartState.SigmaMin) / 2.0 < DragMinimumRadius)
            {
                sigmaMax = _dragStartState.SigmaMin + 2.0 * DragMinimumRadius;
            }
            _state.SetPrincipalExtremes(_dragStartState.SigmaMin, sigmaMax);
            ClampStressState();
        }
        else if (_dragTarget == DragTarget.SigmaMin)
        {
            double sigmaMin = ClampStress(_dragStartState.SigmaMin + stressDeltaX);
            _state = _dragStartState.Clone();
            if (Math.Abs(_dragStartState.SigmaMax - sigmaMin) / 2.0 < DragMinimumRadius)
            {
                sigmaMin = _dragStartState.SigmaMax - 2.0 * DragMinimumRadius;
            }
            _state.SetPrincipalExtremes(sigmaMin, _dragStartState.SigmaMax);
            ClampStressState();
        }
        else if (_dragTarget == DragTarget.SigmaX)
        {
            _state.SigmaX = ClampStress(_dragStartState.SigmaX + stressDeltaX);
            _state.SigmaY = _dragStartState.SigmaY;
            _state.TauXY = _dragStartState.TauXY;
            ClampStressState();
        }
        else if (_dragTarget == DragTarget.SigmaY)
        {
            _state.SigmaX = _dragStartState.SigmaX;
            _state.SigmaY = ClampStress(_dragStartState.SigmaY + stressDeltaX);
            _state.TauXY = _dragStartState.TauXY;
            ClampStressState();
        }
        else if (_dragTarget == DragTarget.TauXY)
        {
            _state.SigmaX = _dragStartState.SigmaX;
            _state.SigmaY = _dragStartState.SigmaY;
            double tau = ClampTauForCurrentPrincipalBounds(_dragStartState.TauXY + stressDeltaY);
            if (Math.Abs(tau) < DragMinimumRadius && Math.Abs(stressDeltaY) > 0)
            {
                tau = Math.Sign(stressDeltaY) * DragMinimumRadius;
            }

            _state.TauXY = tau;
        }
        else if (_dragTarget == DragTarget.TauMax)
        {
            double startRadius = Math.Max(DragMinimumRadius, _dragStartState.Radius);
            double radius = Math.Pow(10.0, Math.Log10(startRadius) + logRadiusDelta);
            _state = _dragStartState.Clone();
            _state.SetRadius(ClampRadiusForAverage(radius, _dragStartState.SigmaAverage));
            ClampStressState();
        }
        else if (_dragTarget == DragTarget.AngleLine)
        {
            double angle = Math.Atan2(_mohrCenter.Y - p.Y, p.X - _mohrCenter.X);
            double mohrDegrees = SnapToCardinalDegrees(NormalizeCircleDegrees(StressState.Degrees(angle)));
            _state.PhysicalAngleDegrees = NormalizeDegrees(mohrDegrees / 2.0);
        }

        SyncUiFromState();
    }

    private void StressTensorCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragTarget = DragTarget.StressTensorDiagram;
        Point p = e.GetPosition(StressTensorCanvas);
        _dragStartPoint = p;
        _dragStartState = _state.Clone();
        Point center = new(StressTensorCanvas.ActualWidth / 2.0, StressTensorCanvas.ActualHeight / 2.0 + 10);
        _stressTensorBaseAngle = StressState.Degrees(Math.Atan2(p.Y - center.Y, p.X - center.X)) - MohrVisualAngleDegrees(_state.PhysicalAngleDegrees);
        StressTensorCanvas.CaptureMouse();
    }

    private void StressTensorCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTarget != DragTarget.StressTensorDiagram || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point p = e.GetPosition(StressTensorCanvas);
        Point center = new(StressTensorCanvas.ActualWidth / 2.0, StressTensorCanvas.ActualHeight / 2.0 + 10);
        double visualAngle = SnapToCardinalDegrees(NormalizeCircleDegrees(StressState.Degrees(Math.Atan2(p.Y - center.Y, p.X - center.X)) - _stressTensorBaseAngle));
        _state.PhysicalAngleDegrees = NormalizeDegrees(visualAngle / 2.0);
        SyncUiFromState();
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragTarget = DragTarget.None;
        MohrCanvas.ReleaseMouseCapture();
        StressTensorCanvas.ReleaseMouseCapture();
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawMohrCircle();
        DrawStressTensorDiagram();
    }

    private DragTarget HitTestMohr(Point p)
    {
        double mohrAngle = StressState.Radians(MohrVisualAngleDegrees(_state.PhysicalAngleDegrees));
        Point anglePoint = new(
            _mohrCenter.X + Math.Cos(mohrAngle) * Math.Max(18, VisualRadiusPixels()),
            _mohrCenter.Y - Math.Sin(mohrAngle) * Math.Max(18, VisualRadiusPixels()));
        if (Distance(p, anglePoint) < 30 || DistanceToSegment(p, _mohrCenter, anglePoint) < 10)
        {
            return DragTarget.AngleLine;
        }

        if (Distance(p, CirclePoint(_state.HalfDifference, 0)) < 18) return DragTarget.SigmaX;
        if (Distance(p, CirclePoint(-_state.HalfDifference, 0)) < 18) return DragTarget.SigmaY;
        if (Distance(p, CirclePoint(0, _state.TauXY)) < 18) return DragTarget.TauXY;
        if (Distance(p, CirclePoint(-_state.Radius, 0)) < 18) return DragTarget.SigmaMin;
        if (Distance(p, CirclePoint(_state.Radius, 0)) < 18) return DragTarget.SigmaMax;
        if (Distance(p, CirclePoint(0, _state.TauMax)) < 18) return DragTarget.TauMax;

        return DragTarget.None;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "CSV parameter files (*.csv)|*.csv|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _state = CsvStressFile.Open(dialog.FileName);
            ClampStressState();
            _currentPath = dialog.FileName;
            StatusText.Content = $"Opened {System.IO.Path.GetFileName(dialog.FileName)}";
            SyncUiFromState();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentPath))
        {
            SaveCsv(_currentPath);
        }
        else
        {
            SaveAs_Click(sender, e);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        SaveAs();
    }

    private void SaveCsv_Click(object sender, RoutedEventArgs e)
    {
        SaveAs(preferCsv: true);
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        SaveAs(preferScreenshot: true);
    }

    private void SaveAs(bool preferCsv = false, bool preferScreenshot = false)
    {
        SaveFileDialog dialog = new()
        {
            FileName = "MCSTVisualizer",
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|TIFF image (*.tif;*.tiff)|*.tif;*.tiff|CSV parameters (*.csv)|*.csv",
            FilterIndex = preferCsv ? 4 : 1
        };

        if (preferScreenshot)
        {
            dialog.FilterIndex = 1;
        }

        if (dialog.ShowDialog(this) == true)
        {
            string extension = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (extension == ".csv" || preferCsv)
            {
                SaveCsv(dialog.FileName);
                _currentPath = dialog.FileName;
            }
            else
            {
                ScreenshotExporter.SaveFrameworkElement(RootVisual, dialog.FileName);
                StatusText.Content = $"Saved screenshot {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
    }

    private void SaveCsv(string path)
    {
        CsvStressFile.Save(path, _state);
        StatusText.Content = $"Saved parameters {System.IO.Path.GetFileName(path)}";
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
    }

    private void Zoom_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Content = "Zoom controls are reserved for a later build.";
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        string helpPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Help", "MCSTVisualizer.chm");
        if (File.Exists(helpPath))
        {
            Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show(this, "The CHM help file has not been added yet.", "Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private Point StressPoint(double sigma, double tau)
    {
        return new Point(ToCanvasX(sigma), _mohrCenter.Y - tau * _mohrScale);
    }

    private Point CirclePoint(double normalOffsetFromCenter, double shear)
    {
        double radius = _state.Radius;
        double visualRadius = VisualRadiusPixels();
        if (radius <= double.Epsilon)
        {
            return _mohrCenter;
        }

        double x = _mohrCenter.X + (normalOffsetFromCenter / radius) * visualRadius;
        double y = _mohrCenter.Y - (shear / radius) * visualRadius;
        return new Point(x, y);
    }

    private double VisualRadiusPixels()
    {
        return VisualRadiusPixels(_state.Radius);
    }

    private double VisualRadiusPixels(double radiusGPa)
    {
        double maxRadius = Math.Min(Math.Max(1.0, MohrCanvas.ActualWidth), Math.Max(1.0, MohrCanvas.ActualHeight)) * 0.42;
        maxRadius = Math.Max(50.0, maxRadius);
        double radius = Math.Max(DragMinimumRadius, Math.Min(StressLimit, radiusGPa));
        double t = (Math.Log10(radius) - Math.Log10(DragMinimumRadius)) / (Math.Log10(StressLimit) - Math.Log10(DragMinimumRadius));
        t = Math.Clamp(t, 0.0, 1.0);
        return 50.0 + t * (maxRadius - 50.0);
    }

    private double ToCanvasX(double sigma)
    {
        double width = Math.Max(1, MohrCanvas.ActualWidth);
        return 42 + (sigma - _mohrDomainMin) * (width - 84) / Math.Max(1, _mohrDomainMax - _mohrDomainMin);
    }

    private double ToStressX(double x)
    {
        double width = Math.Max(1, MohrCanvas.ActualWidth);
        return _mohrDomainMin + (x - 42) * Math.Max(1, _mohrDomainMax - _mohrDomainMin) / Math.Max(1, width - 84);
    }

    private void DrawStressArrow(Point start, Vector direction, double value, string label)
    {
        Vector normal = direction;
        normal.Normalize();
        double length = 42;
        Vector signDirection = value >= 0 ? normal : -normal;
        Point end = start + signDirection * length;
        AddLine(StressTensorCanvas, start.X, start.Y, end.X, end.Y, value >= 0 ? "#1B7F5A" : "#7B2CBF", 2.0);
        AddArrowHead(StressTensorCanvas, end, signDirection, value >= 0 ? "#1B7F5A" : "#7B2CBF");
        AddText(StressTensorCanvas, label, end.X + 4, end.Y + 4, 12, value >= 0 ? "#1B7F5A" : "#7B2CBF");
    }

    private void DrawShearArrow(Point start, Vector direction, double value)
    {
        Vector dir = direction;
        dir.Normalize();
        if (value < 0)
        {
            dir = -dir;
        }

        Point end = start + dir * 38;
        AddLine(StressTensorCanvas, start.X, start.Y, end.X, end.Y, "#C2410C", 6.0, 0.18);
        AddArrowHead(StressTensorCanvas, end, dir, "#C2410C", 0.18, 14, 6);
        AddLine(StressTensorCanvas, start.X, start.Y, end.X, end.Y, "#C2410C", 2.0);
        AddArrowHead(StressTensorCanvas, end, dir, "#C2410C");
    }

    private static Point Rotate(Point p, double radians, Point center)
    {
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);
        return new Point(center.X + p.X * c - p.Y * s, center.Y + p.X * s + p.Y * c);
    }

    private static void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, string color, double thickness, double opacity = 1.0)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = Brush(color, opacity),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
    }

    private static void AddPoint(Canvas canvas, Point p, string color, string tooltip)
    {
        Ellipse ellipse = new()
        {
            Width = 12,
            Height = 12,
            Fill = Brush(color),
            Stroke = Brushes.White,
            StrokeThickness = 1.4,
            ToolTip = tooltip
        };
        Canvas.SetLeft(ellipse, p.X - 6);
        Canvas.SetTop(ellipse, p.Y - 6);
        canvas.Children.Add(ellipse);
    }

    private static void AddText(Canvas canvas, string text, double x, double y, double size, string color)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = size,
            Foreground = Brush(color)
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        canvas.Children.Add(block);
    }

    private static void AddArrowHead(Canvas canvas, Point tip, Vector direction, string color, double opacity = 1.0, double length = 10.0, double halfWidth = 4.5)
    {
        direction.Normalize();
        Vector side = new(-direction.Y, direction.X);
        Point p1 = tip;
        Point p2 = tip - direction * length + side * halfWidth;
        Point p3 = tip - direction * length - side * halfWidth;
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection([p1, p2, p3]),
            Fill = Brush(color, opacity)
        });
    }

    private static SolidColorBrush Brush(string color, double opacity = 1.0)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(color));
        brush.Opacity = opacity;
        return brush;
    }

    private static double Distance(Point a, Point b)
    {
        return (a - b).Length;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        Vector ab = b - a;
        double lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
        if (lengthSquared <= double.Epsilon)
        {
            return Distance(p, a);
        }

        double t = Math.Max(0, Math.Min(1, Vector.Multiply(p - a, ab) / lengthSquared));
        Point projection = a + ab * t;
        return Distance(p, projection);
    }

    private bool TryRead(TextBox box, out double value)
    {
        return double.TryParse(box.Text, NumberStyles.Float, _culture, out value) ||
               double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private string Format(double value)
    {
        return value.ToString("G10", _culture);
    }

    private string FormatStress(double valueGPa)
    {
        return ToDisplayUnit(valueGPa).ToString("G10", _culture);
    }

    private static string Sigma(string subscript)
    {
        return $"σ{ToSubscript(subscript)}";
    }

    private static string SigmaPrime(string axis)
    {
        return $"{Sigma(axis)}′";
    }

    private static string Tau(string subscript)
    {
        return $"τ{ToSubscript(subscript)}";
    }

    private static string Tau(string axis1, string axis2)
    {
        return Tau(axis1 + axis2);
    }

    private static string TauPrime(string axis1, string axis2)
    {
        return $"τ{ToSubscript(axis1)}′{ToSubscript(axis2)}′";
    }

    private static string ToSubscript(string text)
    {
        Dictionary<char, string> map = new()
        {
            ['0'] = "₀",
            ['1'] = "₁",
            ['2'] = "₂",
            ['3'] = "₃",
            ['4'] = "₄",
            ['5'] = "₅",
            ['6'] = "₆",
            ['7'] = "₇",
            ['8'] = "₈",
            ['9'] = "₉",
            ['+'] = "₊",
            ['-'] = "₋",
            ['='] = "₌",
            ['('] = "₍",
            [')'] = "₎",
            ['a'] = "ₐ",
            ['e'] = "ₑ",
            ['h'] = "ₕ",
            ['i'] = "ᵢ",
            ['j'] = "ⱼ",
            ['k'] = "ₖ",
            ['l'] = "ₗ",
            ['m'] = "ₘ",
            ['n'] = "ₙ",
            ['o'] = "ₒ",
            ['p'] = "ₚ",
            ['r'] = "ᵣ",
            ['s'] = "ₛ",
            ['t'] = "ₜ",
            ['u'] = "ᵤ",
            ['v'] = "ᵥ",
            ['x'] = "ₓ",
            ['y'] = "ᵧ",
            ['z'] = "ᶻ"
        };

        return string.Concat(text.ToLowerInvariant().Select(character => map.TryGetValue(character, out string? subscript) ? subscript : character.ToString()));
    }

    private double ToDisplayUnit(double valueGPa)
    {
        return valueGPa * DisplayScale;
    }

    private double FromDisplayUnit(double displayValue)
    {
        return displayValue / DisplayScale;
    }

    private double DisplayScale => _displayUnit switch
    {
        StressDisplayUnit.Psi => PsiPerGPa,
        StressDisplayUnit.Ksi => PsiPerGPa / 1000.0,
        StressDisplayUnit.MPa => 1000.0,
        StressDisplayUnit.KPa => 1_000_000.0,
        _ => 1.0
    };

    private string UnitLabel => _displayUnit switch
    {
        StressDisplayUnit.Psi => "psi",
        StressDisplayUnit.Ksi => "ksi",
        StressDisplayUnit.MPa => "MPa",
        StressDisplayUnit.KPa => "kPa",
        _ => "GPa"
    };

    private static StressDisplayUnit ParseDisplayUnit(string? label)
    {
        return label?.Trim().ToLowerInvariant() switch
        {
            "ksi" => StressDisplayUnit.Ksi,
            "kpa" => StressDisplayUnit.KPa,
            "mpa" => StressDisplayUnit.MPa,
            "gpa" => StressDisplayUnit.GPa,
            _ => StressDisplayUnit.Psi
        };
    }

    private static string CleanAxis(string? axis, string fallback)
    {
        axis = axis?.Trim();
        return string.IsNullOrWhiteSpace(axis) ? fallback : axis;
    }

    private void ClampStressState()
    {
        _state.SigmaX = ClampStress(_state.SigmaX);
        _state.SigmaY = ClampStress(_state.SigmaY);
        _state.TauXY = ClampTauForCurrentPrincipalBounds(_state.TauXY);
        _state.PhysicalAngleDegrees = NormalizeDegrees(_state.PhysicalAngleDegrees);
    }

    private static double ClampStress(double value)
    {
        return Math.Clamp(value, -StressLimit, StressLimit);
    }

    private static double ClampTauMagnitude(double value)
    {
        return Math.Clamp(Math.Abs(value), 0.0, StressLimit);
    }

    private double ClampTauForCurrentPrincipalBounds(double tau)
    {
        double half = _state.HalfDifference;
        double maxRadius = MaxRadiusForAverage(_state.SigmaAverage);
        double allowableTau = Math.Sqrt(Math.Max(0.0, maxRadius * maxRadius - half * half));
        return Math.Clamp(tau, -allowableTau, allowableTau);
    }

    private static double ClampRadiusForAverage(double radius, double sigmaAverage)
    {
        return Math.Clamp(Math.Abs(radius), 0.0, MaxRadiusForAverage(sigmaAverage));
    }

    private static double MaxRadiusForAverage(double sigmaAverage)
    {
        double clampedAverage = ClampStress(sigmaAverage);
        return Math.Max(0.0, Math.Min(StressLimit - clampedAverage, clampedAverage + StressLimit));
    }

    private static double NormalizeDegrees(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0)
        {
            degrees += 360.0;
        }

        if (degrees <= AngleSnapToleranceDegrees || degrees >= 360.0 - AngleSnapToleranceDegrees)
        {
            return 0.0;
        }

        return degrees;
    }

    private static double MohrVisualAngleDegrees(double physicalAngleDegrees)
    {
        return NormalizeCircleDegrees(2.0 * physicalAngleDegrees);
    }

    private static double SnapToCardinalDegrees(double degrees)
    {
        double normalized = NormalizeCircleDegrees(degrees);
        double[] targets = [0.0, 90.0, 180.0, 270.0, 360.0];
        foreach (double target in targets)
        {
            if (Math.Abs(normalized - target) <= AngleSnapToleranceDegrees)
            {
                return NormalizeCircleDegrees(target);
            }
        }

        return normalized;
    }

    private static double NormalizeCircleDegrees(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0)
        {
            degrees += 360.0;
        }

        return degrees;
    }
}

