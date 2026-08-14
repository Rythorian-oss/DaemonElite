// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
using DaemonElite.Audio;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace DaemonElite.Controls;

public sealed class AudioVisualizer : FrameworkElement
{
    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
    // All Draw Resources are Static: their color values never depend on instance
    // state, so every AudioVisualizer shares one set instead of each instance allocating its own.
    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

    // They're built once and Frozen, which:
    // - lets the render thread skip dirty/change-notification checks per draw call
    // - makes them implicitly thread-safe and immutable
    // - removes ~4 heap allocations per frame, and up to 96 more (2 per bar x 48 bars) that were previously happening
    // inside the OnRender loop.
    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

    private static readonly Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(8, 13, 26));
    private static readonly Pen GridPen = CreateFrozenPen(Color.FromArgb(38, 69, 112, 155), 1);
    private static readonly Brush BarBrush = CreateFrozenBarBrush();
    private static readonly Pen PeakPen = CreateFrozenPen(Color.FromArgb(180, 155, 238, 255), 1);
    private static readonly Pen BaselinePen = CreateFrozenPen(Color.FromArgb(100, 69, 215, 255), 1);

    private readonly DispatcherTimer _timer;
    private readonly float[] _peaks = new float[48];
    private readonly float[] _heights = new float[48];
    private float[]? _magnitudes;
    private AudioManager? _audio;
    private double _phase;

    public AudioManager? AudioSource
    {
        get => _audio;
        set { _audio = value; InvalidateVisual(); }
    }

    public AudioVisualizer()
    {
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(32) };
        _timer.Tick += (_, _) => { _phase += .035; InvalidateVisual(); };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double width = Math.Max(ActualWidth, 1);
        double height = Math.Max(ActualHeight, 1);

        drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        for (int i = 1; i < 5; i++)
        {
            drawingContext.DrawLine(GridPen, new Point(0, height * i / 5), new Point(width, height * i / 5));
        }
        for (int i = 1; i < 12; i++)
        {
            drawingContext.DrawLine(GridPen, new Point(width * i / 12, 0), new Point(width * i / 12, height));
        }

        if (_audio is not null)
            _magnitudes = _audio.GetSmoothedMagnitudesCopy(ref _magnitudes);

        double gap = 3;
        double barWidth = Math.Max(2, (width - gap * (_heights.Length - 1)) / _heights.Length);

        for (int i = 0; i < _heights.Length; i++)
        {
            int magnitudeIndex = _magnitudes is null
                ? 0
                : Math.Min((int)(Math.Pow(i / (double)_heights.Length, 1.8) * _magnitudes.Length), _magnitudes.Length - 1);
            double normalized = _magnitudes is null ? 0 : Math.Min(1, _magnitudes[magnitudeIndex] * 4.2);
            if (_audio?.IsRecording != true && _audio?.IsPlaying != true)
                normalized = Math.Max(0, normalized - .02);

            float target = (float)(normalized * height * .88);
            _heights[i] += (target - _heights[i]) * .35f;
            if (_heights[i] > _peaks[i]) _peaks[i] = _heights[i];
            else _peaks[i] = Math.Max(0, _peaks[i] - 1.1f);

            double x = i * (barWidth + gap);
            drawingContext.DrawRoundedRectangle(BarBrush, null, new Rect(x, height - _heights[i], barWidth, Math.Max(2, _heights[i])), 2, 2);

            if (_peaks[i] > 3)
                drawingContext.DrawLine(PeakPen, new Point(x, height - _peaks[i]), new Point(x + barWidth, height - _peaks[i]));
        }

        drawingContext.DrawLine(BaselinePen, new Point(0, height - 1), new Point(width, height - 1));
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color, double thickness)
    {
        var pen = new Pen(CreateFrozenBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static LinearGradientBrush CreateFrozenBarBrush()
    {
        var brush = new LinearGradientBrush(Color.FromRgb(138, 108, 255), Color.FromRgb(69, 215, 255), 90);
        brush.Freeze();
        return brush;
    }
}