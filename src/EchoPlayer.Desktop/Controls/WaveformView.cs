using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EchoPlayer.Desktop.Controls;

/// <summary>
/// A mirrored waveform on a shared timeline, with the played part highlighted and a playhead.
/// Clicking it runs <see cref="SeekCommand"/> with the clicked point (0–1).
/// </summary>
public sealed class WaveformView : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = Register<IReadOnlyList<float>?>(nameof(Peaks), null);
    public static readonly DependencyProperty LengthFractionProperty = Register(nameof(LengthFraction), 1.0);
    public static readonly DependencyProperty PlayheadFractionProperty = Register(nameof(PlayheadFraction), 0.0);
    public static readonly DependencyProperty MarkerFractionProperty = Register(nameof(MarkerFraction), double.NaN);
    public static readonly DependencyProperty WaveBrushProperty = Register<Brush?>(nameof(WaveBrush), Brushes.Gray);
    public static readonly DependencyProperty PlayedBrushProperty = Register<Brush?>(nameof(PlayedBrush), Brushes.SteelBlue);
    public static readonly DependencyProperty PlayheadBrushProperty = Register<Brush?>(nameof(PlayheadBrush), Brushes.White);
    public static readonly DependencyProperty AxisBrushProperty = Register<Brush?>(nameof(AxisBrush), Brushes.DimGray);
    public static readonly DependencyProperty UnplayedOpacityProperty = Register(nameof(UnplayedOpacity), 1.0);
    public static readonly DependencyProperty SeekCommandProperty = DependencyProperty.Register(
        nameof(SeekCommand), typeof(ICommand), typeof(WaveformView));

    public WaveformView()
    {
        Cursor = Cursors.Hand;
    }

    /// <summary>Peak levels (0–1) spread evenly over the waveform's own length.</summary>
    public IReadOnlyList<float>? Peaks
    {
        get => (IReadOnlyList<float>?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    /// <summary>How much of the timeline this waveform covers (the original is shorter than the echo).</summary>
    public double LengthFraction
    {
        get => (double)GetValue(LengthFractionProperty);
        set => SetValue(LengthFractionProperty, value);
    }

    public double PlayheadFraction
    {
        get => (double)GetValue(PlayheadFractionProperty);
        set => SetValue(PlayheadFractionProperty, value);
    }

    /// <summary>Optional dashed line, for example where the original ends and the tail begins.</summary>
    public double MarkerFraction
    {
        get => (double)GetValue(MarkerFractionProperty);
        set => SetValue(MarkerFractionProperty, value);
    }

    public Brush? WaveBrush
    {
        get => (Brush?)GetValue(WaveBrushProperty);
        set => SetValue(WaveBrushProperty, value);
    }

    public Brush? PlayedBrush
    {
        get => (Brush?)GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public Brush? PlayheadBrush
    {
        get => (Brush?)GetValue(PlayheadBrushProperty);
        set => SetValue(PlayheadBrushProperty, value);
    }

    public Brush? AxisBrush
    {
        get => (Brush?)GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    /// <summary>Opacity of the part that has not been played yet, so the played part stands out.</summary>
    public double UnplayedOpacity
    {
        get => (double)GetValue(UnplayedOpacityProperty);
        set => SetValue(UnplayedOpacityProperty, value);
    }

    public ICommand? SeekCommand
    {
        get => (ICommand?)GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(WaveformView),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        double mid = height / 2;

        // Transparent background so the whole area takes clicks.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        dc.DrawLine(new Pen(AxisBrush, 1), new Point(0, mid), new Point(width, mid));

        if (Peaks is { Count: > 0 } peaks)
        {
            var wave = BuildGeometry(peaks, width * Math.Clamp(LengthFraction, 0, 1), mid);
            double played = width * Math.Clamp(PlayheadFraction, 0, 1);

            dc.PushClip(new RectangleGeometry(new Rect(played, 0, Math.Max(0, width - played), height)));
            dc.PushOpacity(UnplayedOpacity);
            dc.DrawGeometry(WaveBrush, null, wave);
            dc.Pop();
            dc.Pop();

            if (played > 0)
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, played, height)));
                dc.DrawGeometry(PlayedBrush, null, wave);
                dc.Pop();
            }
        }

        if (double.IsFinite(MarkerFraction) && MarkerFraction < 1)
        {
            var dash = new Pen(AxisBrush, 1.5) { DashStyle = DashStyles.Dash };
            double x = width * MarkerFraction;
            dc.DrawLine(dash, new Point(x, 2), new Point(x, height - 2));
        }

        double head = Math.Round(width * Math.Clamp(PlayheadFraction, 0, 1)) + 0.5;
        dc.DrawLine(new Pen(PlayheadBrush, 2), new Point(head, 0), new Point(head, height));
    }

    /// <summary>A closed shape: the peaks above the axis left to right, then mirrored below right to left.</summary>
    private static StreamGeometry BuildGeometry(IReadOnlyList<float> peaks, double width, double mid)
    {
        int columns = Math.Max(1, (int)width);
        var top = new Point[columns + 1];
        for (int x = 0; x <= columns; x++)
        {
            int from = (int)((double)x / (columns + 1) * peaks.Count);
            int to = Math.Max(from + 1, (int)((double)(x + 1) / (columns + 1) * peaks.Count));
            float peak = 0;
            for (int i = from; i < to && i < peaks.Count; i++)
                peak = Math.Max(peak, peaks[i]);

            top[x] = new Point(x * width / columns, mid - Math.Max(0.5, peak * (mid - 2)));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(top[0], isFilled: true, isClosed: true);
            context.PolyLineTo(top, isStroked: false, isSmoothJoin: false);
            context.PolyLineTo(top.Reverse().Select(p => new Point(p.X, 2 * mid - p.Y)).ToArray(), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        double fraction = ActualWidth > 0 ? e.GetPosition(this).X / ActualWidth : 0;
        if (SeekCommand?.CanExecute(fraction) == true)
            SeekCommand.Execute(fraction);
        e.Handled = true;
    }
}
