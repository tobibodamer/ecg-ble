using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace EcgBLEApp.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChartControl : ContentView
    {
        public ChartControl()
        {
            InitializeComponent();

            if (IsVisible)
            {
                _ = AnimationLoop();
            }
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(IsVisible))
            {
                if (IsVisible)
                {
                    _ = AnimationLoop();
                }
            }
        }



        public IList<float> Values
        {
            get { return (IList<float>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }

        public static readonly BindableProperty ValuesProperty =
            BindableProperty.Create("Values", typeof(IList<float>), typeof(ChartControl));


        public double FramesPerSecond
        {
            get { return (double)GetValue(FramesPerSecondProperty); }
            set { SetValue(FramesPerSecondProperty, value); }
        }

        public static readonly BindableProperty FramesPerSecondProperty =
            BindableProperty.Create("FramesPerSecond", typeof(double), typeof(ChartControl), 90.0);


        private int _lastValueCount;

        async Task AnimationLoop()
        {
            while (IsVisible)
            {
                if (Values == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1.0 / FramesPerSecond));
                    continue;
                }

                if (Values.Count > MaxX)
                {
                    int newValuesCount = Math.Max(0, Values.Count - _lastValueCount);

                    switch (TimeMode)
                    {
                        case TimeModes.Fit:
                            MaxX += newValuesCount;
                            break;
                        case TimeModes.Scrolling:
                            MinX += newValuesCount;
                            MaxX += newValuesCount;
                            break;
                        case TimeModes.Reset:
                            _lastWindow = new SKRect(MinX, MinY, MaxX, MaxY);
                            MaxX = Values.Count + RangeX;
                            MinX = Values.Count;
                            break;
                        case TimeModes.Pinned:
                        default:
                            break;
                    }

                    _lastValueCount = Values.Count;
                }

                canvasView.InvalidateSurface();

                await Task.Delay(TimeSpan.FromSeconds(1.0 / FramesPerSecond));
            }

            Debug.WriteLine("Animation loop ended.");
        }

        [Flags]
        public enum SizeModes
        {
            Scale = 0,
            FitToHeight = 1,
            FitToWidth = 2,
            FitToWidthAndHeight = FitToHeight | FitToWidth,
        }

        public enum TimeModes
        {
            // Resize to always show all values
            Fit,
            // Show values between min and max x
            Pinned,
            // Scroll for new values
            Scrolling,
            // Reset to start when reaching max x
            Reset
        }

        public TimeModes TimeMode { get; set; } = TimeModes.Reset;
        public SizeModes SizeMode { get; set; } = SizeModes.FitToHeight;

        public float MinX { get; set; } = 0;
        public float MaxX { get; set; } = 200;

        public float MinY { get; set; } = 0;
        public float MaxY { get; set; } = 100;

        public float XScale { get; set; } = 1;
        public float YScale { get; set; } = 1;

        public float RangeX => Math.Abs(MaxX - MinX);
        public float RangeY => Math.Abs(MaxY - MinY);

        public float AspectRatio => XScale / YScale;

        public Point ZeroPoint { get; set; } = Point.Zero;


        public bool DrawLastWindow { get; set; } = true;

        public float FadeOutStart => RangeX * 0.8f;
        public float FadeOutEnd => RangeX * 0.9f;
        public float FadeOutLength => FadeOutEnd - FadeOutStart;

        public bool ShowGrid
        {
            get { return (bool)GetValue(ShowGridProperty); }
            set { SetValue(ShowGridProperty, value); }
        }

        public static readonly BindableProperty ShowGridProperty =
            BindableProperty.Create("ShowGrid", typeof(bool), typeof(ChartControl), false);


        public Color GridColor
        {
            get { return (Color)GetValue(GridColorProperty); }
            set { SetValue(GridColorProperty, value); }
        }

        public static readonly BindableProperty GridColorProperty =
            BindableProperty.Create("GridColor", typeof(Color), typeof(ChartControl), Color.LightGray);

        public Size GridSize
        {
            get { return (Size)GetValue(GridSizeProperty); }
            set { SetValue(GridSizeProperty, value); }
        }

        public static readonly BindableProperty GridSizeProperty =
            BindableProperty.Create("GridSize", typeof(Size), typeof(ChartControl), new Size(100, 100));

        public Size FineGridSize
        {
            get { return (Size)GetValue(FineGridSizeProperty); }
            set { SetValue(FineGridSizeProperty, value); }
        }

        public static readonly BindableProperty FineGridSizeProperty =
            BindableProperty.Create("FineGridSize", typeof(Size), typeof(ChartControl), new Size(20, 20));

        public AlignGrid GridAlignment { get; set; } = AlignGrid.YAxis;

        [Flags]
        public enum AlignGrid
        {
            /// <summary>
            /// Aligns the grid to the canvas zero point.
            /// </summary>
            None = 0,

            /// <summary>
            /// Aligns the grid to the X axis.
            /// </summary>
            XAxis = 1,

            /// <summary>
            /// Aligns the grid to the Y axis.
            /// </summary>
            YAxis = 2,

            /// <summary>
            /// Aligns the grid to both axis.
            /// </summary>
            BothAxis = XAxis | YAxis,
        }

        private SKRect _lastWindow = SKRect.Empty;
        private SKMatrix _chartToDeviceMatrix, _deviceToChartMatrix;

        /// <summary>
        /// Updates the min and max values to achieve a new range, by maintaining the center position of the axis.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="zero"></param>
        /// <param name="oldRange"></param>
        /// <param name="newRange"></param>
        private static (float min, float max) GetNewRange(float min, float max, float zero, float oldRange, float newRange)
        {
            if (min > zero)
            {
                return (min, min + newRange);
            }
            else if (max < zero)
            {
                return (max - newRange, max);
            }

            // Fit range
            float lowerWeight = (zero - min) / oldRange;
            float upperWeight = (max - zero) / oldRange;

            min = zero - lowerWeight * newRange;
            max = zero + upperWeight * newRange;

            return (min, max);
        }

        private void ConfigureScale(float height, float width)
        {
            // NOTE: This function needs to be only called on resize

            float aspectRatio = AspectRatio;



            switch (SizeMode)
            {
                case SizeModes.FitToHeight:
                    YScale = height / RangeY;
                    XScale = YScale * aspectRatio; // Adjust x scale

                    // Fit x range
                    float newXRange = (width / XScale);
                    (MinX, MaxX) = GetNewRange(MinX, MaxX, (float)ZeroPoint.X, RangeX, newXRange);

                    // Fit last window
                    (_lastWindow.Left, _lastWindow.Right) = GetNewRange(
                        _lastWindow.Left, _lastWindow.Right, (float)ZeroPoint.X, _lastWindow.Width, newXRange);

                    break;
                case SizeModes.FitToWidth:
                    XScale = width / RangeX;
                    YScale = XScale / aspectRatio;

                    // Fit y range
                    float newYRange = (height / YScale);
                    (MinY, MaxY) = GetNewRange(MinY, MaxY, (float)ZeroPoint.Y, RangeY, newYRange);

                    // Fit last window
                    (_lastWindow.Top, _lastWindow.Bottom) = GetNewRange(
                        _lastWindow.Top, _lastWindow.Bottom, (float)ZeroPoint.Y, _lastWindow.Height, newYRange);
                    break;
                case SizeModes.FitToWidthAndHeight:
                    XScale = width / RangeX;
                    YScale = height / RangeY;
                    break;
                case SizeModes.Scale:
                default:
                    // Dont change x and y scales
                    break;
            }
        }
        private void ConfigureTransforms(SKPoint ChartMin, SKPoint ChartMax, SKPoint DeviceMin, SKPoint DeviceMax)
        {
            float xOffset = -ChartMin.X * XScale + DeviceMin.X;
            float yOffset = -ChartMin.Y * -YScale + DeviceMax.Y;

            _chartToDeviceMatrix = SKMatrix.CreateScaleTranslation(XScale, -YScale, xOffset, yOffset);
            _chartToDeviceMatrix.TryInvert(out _deviceToChartMatrix);
        }


        /// <summary>
        /// Transform a point from chart to device coordinates.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private SKPoint ChartToDevice(SKPoint point)
        {
            return _chartToDeviceMatrix.MapPoint(point);
        }

        void DrawGrid(SKCanvas canvas, SKPaint paint, float height, float width)
        {
            if (width == 0 || height == 0)
            {
                return;
            }

            float xOffset = GridAlignment.HasFlag(AlignGrid.XAxis)
                ? width - Math.Abs(((MinX + (float)ZeroPoint.X) % width))
                : 0;

            float yOffset = GridAlignment.HasFlag(AlignGrid.YAxis)
                ? Math.Abs(((MinY + (float)ZeroPoint.Y) % height))
                : 0;

            for (float x = MinX + xOffset; x < MaxX; x += width)
            {
                canvas.DrawLine(ChartToDevice(new SKPoint(x, MinY)), ChartToDevice(new SKPoint(x, MaxY)), paint);
            }

            for (float y = MinY + yOffset; y < MaxY; y += height)
            {
                canvas.DrawLine(ChartToDevice(new SKPoint(MinX, y)), ChartToDevice(new SKPoint(MaxX, y)), paint);
            }
        }

        void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            ConfigureScale(info.Height, info.Width);

            if (ShowGrid)
            {
                using (var paint = new SKPaint())
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.Color = GridColor.ToSKColor();
                    paint.StrokeWidth = 2;

                    var DeviceMin = new SKPoint(0, 0);
                    var DeviceMax = new SKPoint(info.Width - 0, info.Height - 0);
                    var ChartMin = new SKPoint(MinX, MinY);
                    var ChartMax = new SKPoint(MaxX, MaxY);

                    // Prepare the transformation matrices.
                    this.ConfigureTransforms(ChartMin, ChartMax, DeviceMin, DeviceMax);

                    // Draw big grid
                    DrawGrid(canvas, paint, (float)GridSize.Height, (float)GridSize.Width);

                    paint.StrokeWidth = 1;

                    // Draw fine grid
                    DrawGrid(canvas, paint, (float)FineGridSize.Height, (float)FineGridSize.Width);
                }
            }


            DrawValues(MinX, MinY, MaxX, MaxY, 0, args);

            if (TimeMode == TimeModes.Reset && DrawLastWindow && !_lastWindow.IsEmpty)
            {
                int x0 = (int)Math.Floor(MinX);
                int xOffset = Math.Max(0, (Values.Count - 1) - x0);

                DrawValues(_lastWindow.Left, _lastWindow.Top, _lastWindow.Right, _lastWindow.Bottom, xOffset, args);
            }
        }

        private void DrawValues(float minX, float minY, float maxX, float maxY, int xOffset, SKPaintSurfaceEventArgs args)
        {
            if (Values.Count == 0)
            {
                return;
            }

            int numberOfValues = Values.Count;

            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            using (SKPaint paint = new SKPaint())
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = SKColors.Black;
                paint.StrokeWidth = 3;

                int x0 = (int)Math.Floor(minX + xOffset);
                int xEnd = (int)Math.Min(Math.Ceiling(maxX), numberOfValues - 1);

                if (x0 >= numberOfValues - 1)
                {
                    // Nothing to draw
                    return;
                }

                var deviceMin = new SKPoint(0, 0);
                var deviceMax = new SKPoint(info.Width - 0, info.Height - 0);
                var chartMin = new SKPoint(minX, minY);
                var chartMax = new SKPoint(maxX, maxY);

                // Prepare the transformation matrices.
                this.ConfigureTransforms(chartMin, chartMax, deviceMin, deviceMax);

                //SKPath curve = new SKPath();

                //int y0 = ViewModel.Values[x0];
                //SKPoint startPoint = ChartToDevice(new SKPoint(x0, y0));

                //curve.MoveTo(startPoint);

                //for (int x = x0 + 1; x < xEnd; x++)
                //{
                //    int y = ViewModel.Values[x];

                //    if (y > maxY || y < minY)
                //    {
                //        continue;
                //    }

                //    var c1 = ChartToDevice(new SKPoint(x, y));

                //    curve.LineTo(c1);
                //}

                //canvas.DrawPath(curve, paint);

                try
                {
                    for (int x = x0; x < xEnd - 1; x++)
                    {
                        int x1 = x;
                        int x2 = x + 1;

                        float y1 = Values[x1];
                        float y2 = Values[x2];

                        if ((y1 > MaxY && y2 > MaxY) ||
                            (y1 < MinY && y2 < MinY))
                        {
                            continue;
                        }

                        var c1 = ChartToDevice(new SKPoint(x1, y1));
                        var c2 = ChartToDevice(new SKPoint(x2, y2));

                        // Fade out
                        if (TimeMode == TimeModes.Reset)
                        {
                            int age = (numberOfValues - 1) - x;

                            if (age > FadeOutStart)
                            {
                                float fadeOutFactor = CalculateFadeOutFactor(age);
                                float fadeOutFactor2 = CalculateFadeOutFactor(age - 1);

                                paint.Shader = SKShader.CreateLinearGradient(
                                        c1,
                                        c2,
                                        new SKColor[] {
                                        paint.Color.WithAlpha((byte)Map(fadeOutFactor, 0, 1, 255, 0)),
                                        paint.Color.WithAlpha((byte)Map(fadeOutFactor2, 0, 1, 255, 0)) },
                                        SKShaderTileMode.Clamp);

                                //paint.Color = paint.Color.WithAlpha((byte)Map(fadeOutFactor, 0, 1, 255, 0));
                            }
                        }

                        canvas.DrawLine(c1, c2, paint);
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Collection was probably modified
                }
            }
        }

        private float CalculateFadeOutFactor(int age)
        {
            float fadeOutAge = age - FadeOutStart;

            return Math.Min(1, fadeOutAge / FadeOutLength);
        }

        private static float Map(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }
    }
}