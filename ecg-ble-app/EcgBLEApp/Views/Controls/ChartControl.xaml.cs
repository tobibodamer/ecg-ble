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
        private SKRect _lastWindow = SKRect.Empty;
        private SKMatrix _chartToDeviceMatrix, _deviceToChartMatrix;
        private int _lastValueCount;
        public ChartControl()
        {
            InitializeComponent();

            if (IsVisible)
            {
                _ = AnimationLoop();
            }

            canvasView.EnableTouchEvents = true;
            canvasView.Touch += CanvasView_Touch;
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

        /// <summary>
        /// The list of values Y values.
        /// </summary>
        public IList<float> Values
        {
            get { return (IList<float>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }

        public static readonly BindableProperty ValuesProperty =
            BindableProperty.Create("Values", typeof(IList<float>), typeof(ChartControl));

        /// <summary>
        /// The refresh rate of the main animation loop.
        /// </summary>
        public double FramesPerSecond
        {
            get { return (double)GetValue(FramesPerSecondProperty); }
            set { SetValue(FramesPerSecondProperty, value); }
        }

        public static readonly BindableProperty FramesPerSecondProperty =
            BindableProperty.Create("FramesPerSecond", typeof(double), typeof(ChartControl), 60.0);


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
                            MaxX = Values.Count + XRange;
                            MinX = Values.Count;
                            break;
                        case TimeModes.Pinned:
                        default:
                            break;
                    }

                }
                _lastValueCount = Values.Count;

                canvasView.InvalidateSurface();

                await Task.Delay(TimeSpan.FromSeconds(1.0 / FramesPerSecond));
            }

            Debug.WriteLine("Animation loop ended.");
        }

        public enum SizeModes
        {
            /// <summary>
            /// Extends the displayed range to fit the width and height. <br></br>
            /// Keeps the current aspect ratio and scale.
            /// </summary>
            Extend,

            /// <summary>
            /// Extends the displayed X range to fit the width, while keeping the current Y range. <br></br>
            /// Keeps the current X scale.
            /// </summary>
            ExtendWidth,

            /// <summary>
            /// Extends the displayed Y range to fit the height, while keeping the current X range. <br></br>
            /// Keeps the current Y scale.
            /// </summary>
            ExtendHeight,

            /// <summary>
            /// Fit the displayed range to the new height (fixed Y range on resize). <br></br>
            /// Keeps the current aspect ratio.
            /// </summary>
            FitToHeight,

            /// <summary>
            /// Fit the displayed range to the new width (fixed X range on resize). <br></br>
            /// Keeps the current aspect ratio.
            /// </summary>
            FitToWidth,

            /// <summary>
            /// Fit the displayed range to the new width and height (keep current X and Y range on resize).
            /// </summary>
            FitToWidthAndHeight,

        }

        public enum TimeModes
        {
            /// <summary>
            /// Resize to always show all values
            /// </summary>
            Fit,

            /// <summary>
            /// Show values between min and max x
            /// </summary>
            Pinned,

            /// <summary>
            /// Scroll for new values
            /// </summary>
            Scrolling,

            /// <summary>
            /// Reset to start when reaching max x
            /// </summary>
            Reset
        }

        /// <summary>
        /// Describes how the X range is transformed, when new values are added.
        /// </summary>
        public TimeModes TimeMode { get; set; } = TimeModes.Reset;

        /// <summary>
        /// Describes how the axis boundaries are transformed, when the control is resized.
        /// </summary>
        public SizeModes SizeMode { get; set; } = SizeModes.FitToHeight;

        /// <summary>
        /// Used to configure the X axis.
        /// </summary>
        public Axis XAxis { get; set; } = new Axis(minimum: 0, maximum: float.PositiveInfinity);

        /// <summary>
        /// Used to configure the Y axis.
        /// </summary>
        public Axis YAxis { get; set; } = new Axis();

        /// <summary>
        /// Gets or sets the current minimum X value.
        /// </summary>
        public float MinX { get; set; } = 0;

        /// <summary>
        /// Gets or sets the current maximum X value.
        /// </summary>
        public float MaxX { get; set; } = 200;

        /// <summary>
        /// Gets or sets the current minimum Y value.
        /// </summary>
        public float MinY { get; set; } = 0;

        /// <summary>
        /// Gets or sets the current maximum Y value.
        /// </summary>
        public float MaxY { get; set; } = 100;

        /// <summary>
        /// The scale of the X axis in pixel per value.
        /// </summary>
        public float XScale { get; set; } = 1;

        /// <summary>
        /// The scale of the Y axis in pixel per value.
        /// </summary>
        public float YScale { get; set; } = 1;

        /// <summary>
        /// The current range of the X axis.
        /// </summary>
        public float XRange => Math.Abs(MaxX - MinX);

        /// <summary>
        /// The current range of the Y axis.
        /// </summary>
        public float YRange => Math.Abs(MaxY - MinY);

        /// <summary>
        /// Calculated current aspect ratio.
        /// </summary>
        public float AspectRatio => XScale / YScale;

        /// <summary>
        /// Gets or sets the zero (reference) point. <br></br>
        /// (e.g. for resizing and grid alignment)
        /// </summary>
        public Point ZeroPoint { get; set; }

        /// <summary>
        /// If set to true, when <see cref="TimeMode"/> is set to <see cref="TimeModes.Reset"/>, 
        /// the previous window will be drawn after reset, until the new values override the old ones.
        /// </summary>
        public bool DrawLastWindow { get; set; } = true;

        public float FadeOutStart => XRange * 0.8f;
        public float FadeOutEnd => XRange * 0.9f;
        public float FadeOutLength => FadeOutEnd - FadeOutStart;


        #region Grid
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

        public Color FineGridColor
        {
            get { return (Color)GetValue(FineGridColorProperty); }
            set { SetValue(FineGridColorProperty, value); }
        }

        public static readonly BindableProperty FineGridColorProperty =
            BindableProperty.Create("FineGridColor", typeof(Color), typeof(ChartControl), Color.LightGray);

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

        #endregion        

        #region Scaling / Range

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

        [Flags]
        public enum AxisTypes
        {
            X = 1,
            Y = 2,
            Both = X | Y
        }

        /// <summary>
        /// Gets the best possible range for a given target range, while respecting the boundaries of the axis.
        /// 
        /// </summary>
        /// <param name="newRange">The new target range.</param>
        /// <param name="currentMin">The current min value.</param>
        /// <param name="currentMax">The current max value.</param>
        /// <param name="axis">The axis.</param>
        /// <param name="zero">The zero (reference) value.</param>
        /// <returns>Returns the new min and max values, and a true if the target range can be achieved.</returns>
        private (float min, float max, bool fits) GetClosestRange(ref float newRange, float currentMin, float currentMax, Axis axis, float zero)
        {
            float min, max;
            bool fits = false;
            float currentRange = Math.Abs(currentMax - currentMin);


            // Check range limits first
            if (axis.MaxRange.HasValue && newRange > axis.MaxRange && newRange >= currentRange)
            {
                // Too large and getting larger
                newRange = axis.MaxRange.Value;
            }
            else if (axis.MinRange.HasValue && newRange < axis.MinRange && newRange <= currentRange)
            {
                // Too small and getting smaller                    
                newRange = axis.MinRange.Value;
            }
            else
            {
                // Fits, when the total max range can be resepected too
                fits = newRange <= axis.MaximumRange;
            }

            // Calculate min and max
            (min, max) = GetNewRange(currentMin, currentMax, zero, currentRange, newRange);

            return (Math.Max(axis.Minimum, min), Math.Min(axis.Maximum, max), fits);
        }


        /// <summary>
        /// Adjust the <see cref="XScale"/> or <see cref="YScale"/>, to fit the range to the <paramref name="height"/> or <paramref name="width"/>.
        /// </summary>
        /// <param name="axisTypes"></param>
        /// <param name="height"></param>
        /// <param name="width"></param>
        private void RescaleToFitRange(AxisTypes axisTypes, float height, float width)
        {
            if (axisTypes.HasFlag(AxisTypes.X))
            {
                XScale = width / XRange;
            }

            if (axisTypes.HasFlag(AxisTypes.Y))
            {
                YScale = height / YRange;
            }
        }

        /// <summary>
        /// Applies the values from <see cref="GetClosestRange(ref float, float, float, Axis, float)"/> for the given <paramref name="axis"/>.
        /// </summary>
        /// <param name="newRange">The target range.</param>
        /// <param name="axis">The axis, must be either <see cref="AxisTypes.X"/> or <see cref="AxisTypes.Y"/>.</param>
        /// <returns>True if the target range was achieved.</returns>
        private bool SetBestPossibleRange(float newRange, AxisTypes axis)
        {
            bool fits = true;
            float min, max, minWindow, maxWindow;

            switch (axis)
            {
                case AxisTypes.X:
                    (min, max, fits) = GetClosestRange(ref newRange, MinX, MaxX, XAxis, (float)ZeroPoint.X);
                    (minWindow, maxWindow, _) = GetClosestRange(ref newRange, _lastWindow.Left, _lastWindow.Right, XAxis, (float)ZeroPoint.X);
                    MinX = min; MaxX = max; _lastWindow.Left = minWindow; _lastWindow.Right = maxWindow;
                    break;
                case AxisTypes.Y:
                    (min, max, fits) = GetClosestRange(ref newRange, MinY, MaxY, YAxis, (float)ZeroPoint.Y);
                    (minWindow, maxWindow, _) = GetClosestRange(ref newRange, _lastWindow.Top, _lastWindow.Bottom, YAxis, (float)ZeroPoint.Y);
                    MinY = min; MaxY = max; _lastWindow.Top = minWindow; _lastWindow.Bottom = maxWindow;
                    break;
            }

            return fits;
        }

        /// <summary>
        /// Adjust the range of the given <paramref name="axisType"/> to fit to the <paramref name="height"/> or <paramref name="width"/>,
        /// by maintaining the scale. <br></br>
        /// If <paramref name="ignoreLimit"/> is not set, the other axis will be rescaled according to the <see cref="Axis.SizeLimitMode"/>.
        /// </summary>
        /// <param name="axisType"></param>
        /// <param name="height"></param>
        /// <param name="width"></param>
        /// <param name="aspectRatio"></param>
        /// <param name="ignoreLimit"></param>
        private void AdjustRangeToFitScale(AxisTypes axisType, float height, float width, float aspectRatio, bool ignoreLimit)
        {
            float newRange;
            Axis axis;

            if (axisType == AxisTypes.X)
            {
                newRange = (width / XScale);
                axis = XAxis;
            }
            else if (axisType == AxisTypes.Y)
            {
                newRange = (height / YScale);
                axis = YAxis;
            }
            else
            {
                AdjustRangeToFitScale(AxisTypes.X, height, width, aspectRatio, ignoreLimit);
                AdjustRangeToFitScale(AxisTypes.Y, height, width, aspectRatio, ignoreLimit);
                return;
            }

            bool fits = SetBestPossibleRange(newRange, axisType);

            if (!ignoreLimit && !fits)
            {
                if (axis.SizeLimitMode == SizeLimitMode.Fit)
                {
                    // Fit limiting axis to size
                    FitToSize(axisType, height, width, aspectRatio, true);
                }
                else
                {
                    // FitToWidthAndHeight
                    RescaleToFitRange(AxisTypes.Both, height, width);
                }
            }
        }

        /// <summary>
        /// Fit the range of the given <paramref name="axis"/> to the <paramref name="height"/> or <paramref name="width"/>, by adjusting its scale. <br></br>
        /// If only one axis is given, the other axis will be adjusted while the aspect ratio ist maintained.
        /// </summary>
        /// <param name="axis">The axis to fit (can be both).</param>
        /// <param name="height">The new height.</param>
        /// <param name="width">The new width.</param>
        /// <param name="aspectRatio">The aspect ratio to maintain.</param>
        /// <param name="ignoreLimit">Ignore the limit when adjusting the other axis.</param>
        public void FitToSize(AxisTypes axis, float height, float width, float aspectRatio, bool ignoreLimit)
        {
            AxisTypes otherAxis;

            // Fit the range to the size
            RescaleToFitRange(axis, height, width);

            // Adjust other scale to keep aspect ratio
            if (axis == AxisTypes.X)
            {
                YScale = XScale / aspectRatio; // Adjust y scale to maintain aspect ratio
                otherAxis = AxisTypes.Y;
            }
            else
            {
                XScale = YScale * aspectRatio; // Adjust x scale to maintain aspect ratio                
                otherAxis = AxisTypes.X;
            }

            // Adjust the range of the other axis
            AdjustRangeToFitScale(otherAxis, height, width, aspectRatio, ignoreLimit);
        }

        private void ConfigureScale(float height, float width)
        {
            float aspectRatio = AspectRatio;

            switch (SizeMode)
            {
                case SizeModes.ExtendWidth:
                    RescaleToFitRange(AxisTypes.Y, height, width); // Adjust YScale (keep YRange, keep XScale)
                    AdjustRangeToFitScale(AxisTypes.X, height, width, aspectRatio, false);
                    break;
                case SizeModes.ExtendHeight:
                    RescaleToFitRange(AxisTypes.X, height, width);
                    AdjustRangeToFitScale(AxisTypes.Y, height, width, aspectRatio, false);
                    break;
                case SizeModes.Extend:
                    AdjustRangeToFitScale(AxisTypes.Both, height, width, aspectRatio, false);
                    break;
                case SizeModes.FitToHeight:
                    FitToSize(AxisTypes.Y, height, width, aspectRatio, false);
                    break;
                case SizeModes.FitToWidth:
                    FitToSize(AxisTypes.X, height, width, aspectRatio, false);
                    break;
                case SizeModes.FitToWidthAndHeight:
                    RescaleToFitRange(AxisTypes.Both, height, width);
                    break;
                default:
                    // Dont change x and y scales
                    break;
            }
        }

        #endregion

        #region Transform
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

        private SKPoint DeviceToChart(SKPoint point)
        {
            return _deviceToChartMatrix.MapPoint(point);
        }

        #endregion

        #region Draw
        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
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
                    paint.Color = FineGridColor.ToSKColor();

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


        private void DrawGrid(SKCanvas canvas, SKPaint paint, float height, float width)
        {
            if (width == 0 || height == 0)
            {
                return;
            }

            float xOffset = GridAlignment.HasFlag(AlignGrid.XAxis)
                ? width - Math.Abs(Mod((MinX + (float)ZeroPoint.X), width))
                : 0;

            float yOffset = GridAlignment.HasFlag(AlignGrid.YAxis)
                ? height - Math.Abs(Mod((MinY + (float)ZeroPoint.Y), height))
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

                int x0 = (int)Math.Max(Math.Floor(minX + xOffset), 0);
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

        #endregion

        private float CalculateFadeOutFactor(int age)
        {
            float fadeOutAge = age - FadeOutStart;

            return Math.Min(1, fadeOutAge / FadeOutLength);
        }


        #region Touch Manipulations

        public float MinZoom { get; set; } = float.PositiveInfinity; //2f;
        public float MaxZoom { get; set; } = 0.08f;

        public double ZoomThreshold { get; set; } = 0.005;

        /// <summary>
        /// Will set the <see cref="TimeMode"/> to <see cref="TimeModes.Pinned"/> if the user starts dragging.
        /// </summary>
        public bool StopTrackingOnDrag { get; set; } = false;

        /// <summary>
        /// Will set the <see cref="TimeMode"/> to <see cref="TimeModes.Pinned"/> if the user starts zooming.
        /// </summary>
        public bool StopTrackingOnZoom { get; set; } = false;

        public enum ZoomPivot
        {
            Zero,
            Center,
        }

        public enum ZoomModes
        {
            None,
            X,
            Y,
            Both,
            BothKeepAspectRatio
        }

        public ZoomModes ZoomMode { get; set; } = ZoomModes.Both;

        // Touch information
        private readonly Dictionary<long, SKPoint> _touchDictionary = new Dictionary<long, SKPoint>();
        private SKPoint _initialScaleVec, _lastCenter;

        private void CanvasView_Touch(object sender, SKTouchEventArgs e)
        {
            float xOffset = -MinX * XScale + 0;
            float yOffset = -MinY * -YScale + canvasView.CanvasSize.Height;

            var chartToDeviceMatrix = SKMatrix.CreateScaleTranslation(XScale, -YScale, xOffset, yOffset);
            chartToDeviceMatrix.TryInvert(out var deviceToChartMatrix);


            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    if (!_touchDictionary.ContainsKey(e.Id))
                    {
                        _touchDictionary.Add(e.Id, e.Location);

                        if (_touchDictionary.Count == 2)
                        {
                            long[] keys = new long[2];
                            _touchDictionary.Keys.CopyTo(keys, 0);

                            // Find index of non-moving (pivot) finger
                            int pivotIndex = (keys[0] == e.Id) ? 1 : 0;

                            _initialScaleVec = e.Location - _touchDictionary[keys[pivotIndex]];
                            _lastCenter = GetCenter(e.Location, _touchDictionary[keys[pivotIndex]]);
                        }
                    }
                    break;
                case SKTouchAction.Moved:
                    if (_touchDictionary.ContainsKey(e.Id))
                    {
                        // Single-finger drag
                        if (_touchDictionary.Count == 1)
                        {
                            if (StopTrackingOnDrag)
                            {
                                TimeMode = TimeModes.Pinned;
                            }

                            SKPoint point = deviceToChartMatrix.MapPoint(e.Location);
                            SKPoint prevPoint = deviceToChartMatrix.MapPoint(_touchDictionary[e.Id]);

                            var xDiff = point.X - prevPoint.X;
                            var yDiff = point.Y - prevPoint.Y;

                            if (MinX - xDiff >= XAxis.Minimum && MaxX - xDiff <= XAxis.Maximum)
                            {
                                MinX -= xDiff;
                                MaxX -= xDiff;
                            }

                            if (MinY - yDiff >= YAxis.Minimum && MaxY - yDiff <= YAxis.Maximum)
                            {
                                MinY -= yDiff;
                                MaxY -= yDiff;
                            }

                            canvasView.InvalidateSurface();
                        }
                        // Double-finger scale and drag
                        else if (_touchDictionary.Count >= 2)
                        {
                            if (StopTrackingOnZoom)
                            {
                                TimeMode = TimeModes.Pinned;
                            }

                            // Copy two dictionary keys into array
                            long[] keys = new long[_touchDictionary.Count];
                            _touchDictionary.Keys.CopyTo(keys, 0);

                            // Find index of non-moving (pivot) finger
                            int pivotIndex = (keys[0] == e.Id) ? 1 : 0;

                            // Get the three points involved in the transform
                            SKPoint pivotPoint = _touchDictionary[keys[pivotIndex]];
                            SKPoint prevPoint = _touchDictionary[e.Id];
                            SKPoint newPoint = e.Location;

                            SKPoint newCenter = GetCenter(newPoint, pivotPoint);


                            // Calculate two vectors
                            SKPoint oldVector = prevPoint - pivotPoint;
                            SKPoint newVector = newPoint - pivotPoint;



                            float scaleToInitialX = Math.Abs(newVector.X / _initialScaleVec.X);
                            float scaleToInitialY = Math.Abs(newVector.Y / _initialScaleVec.Y);


                            // Scaling factors are ratios of those
                            float scaleX = 1;
                            float scaleY = 1;

                            var translation = deviceToChartMatrix.MapVector(_lastCenter - newCenter);
                            SKMatrix translationMatrix = SKMatrix.Identity;

                            switch (ZoomMode)
                            {
                                case ZoomModes.X:
                                    scaleX = newVector.X / oldVector.X;
                                    translationMatrix = SKMatrix.CreateTranslation(0, translation.Y);
                                    break;
                                case ZoomModes.Y:
                                    scaleY = newVector.Y / oldVector.Y;
                                    translationMatrix = SKMatrix.CreateTranslation(translation.X, 0);
                                    break;
                                case ZoomModes.Both:

                                    scaleX = newVector.X / oldVector.X; //Map(Math.Max(0, 50 - Math.Abs(oldVector.X)), 50, 0, 1, newVector.X / oldVector.X);
                                                                        //translationMatrix = SKMatrix.CreateTranslation(translation.X, 0);

                                    scaleY = newVector.Y / oldVector.Y;
                                    //translationMatrix = SKMatrix.CreateTranslation(translation.X, translation.Y);
                                    break;
                                case ZoomModes.BothKeepAspectRatio:
                                    scaleX = scaleY = newVector.Length / oldVector.Length;
                                    scaleToInitialX = scaleToInitialY = Math.Abs(newVector.Length / _initialScaleVec.Length);
                                    break;
                                case ZoomModes.None:
                                default:
                                    translationMatrix = SKMatrix.CreateTranslation(translation.X, translation.Y);
                                    break;
                            }

                            // Constraint scale to Max / Min zoom levels

                            if (scaleToInitialY <= MaxZoom || scaleToInitialY >= MinZoom || Math.Abs(1 - scaleY) < ZoomThreshold)
                            {
                                scaleY = 1;
                            }
                            else if (scaleToInitialY < 1)
                            {
                                scaleY = Map(scaleToInitialY, 1, MaxZoom, scaleY, 1);
                                //scaleY += (1 - scaleToInitialY) * (1 - scaleY);
                            }
                            else if (scaleToInitialY > 1)
                            {
                                scaleY = Map(scaleToInitialY, 1, MinZoom, scaleY, 1);
                            }

                            if (scaleToInitialX <= MaxZoom || scaleToInitialX >= MinZoom || Math.Abs(1 - scaleX) < ZoomThreshold)
                            {
                                scaleX = 1;
                            }
                            else if (scaleToInitialX < 1)
                            {
                                scaleX = Map(scaleToInitialX, 1, MaxZoom, scaleX, 1);
                            }
                            else if (scaleToInitialX > 1)
                            {
                                scaleX = Map(scaleToInitialX, 1, MinZoom, scaleX, 1);
                            }

                            if (!float.IsNaN(scaleX) && !float.IsInfinity(scaleX) &&
                                !float.IsNaN(scaleY) && !float.IsInfinity(scaleY) &&
                                scaleX > 0 && scaleY > 0)
                            {
                                SKPoint mappedPivotPoint = deviceToChartMatrix.MapPoint(pivotPoint);

                                // If something bad hasn't happened, calculate a scale and translation matrix
                                SKMatrix scaleMatrix =
                                    SKMatrix.CreateScale(1 / scaleX, 1 / scaleY, mappedPivotPoint.X, mappedPivotPoint.Y)
                                            .PostConcat(translationMatrix);

                                // Map min and max values
                                TransformRangeAndScale(scaleMatrix, scaleX, scaleY);


                                canvasView.InvalidateSurface();

                                _lastCenter = newCenter;
                            }
                        }

                        // Store the new point in the dictionary
                        _touchDictionary[e.Id] = e.Location;
                    }
                    break;
                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    if (_touchDictionary.ContainsKey(e.Id))
                    {
                        _touchDictionary.Remove(e.Id);
                    }
                    break;
            }

            // Let the OS know that we want to receive more touch events
            e.Handled = true;
        }

        private void TransformRangeAndScale(SKMatrix transformMatrix, float scaleX, float scaleY)
        {
            var newMin = transformMatrix.MapPoint(MinX, MinY);
            var newMax = transformMatrix.MapPoint(MaxX, MaxY);
            var newRange = newMax - newMin;

            bool xCanFit = (newMin.X >= XAxis.Minimum || newMax.X <= XAxis.Maximum) &&
                (XAxis.ForceRangeLimit
                    ? ((XAxis.MaxRange >= newRange.X || scaleX < 1) && (XAxis.MinRange <= newRange.X || scaleX > 1)) // check if in resize range limit, or if getting out of it
                    : XAxis.MaximumRange >= newRange.X || scaleX < 1); // check absolute range limit, or if getting out of it

            bool yCanFit = (newMin.Y >= YAxis.Minimum || newMax.Y <= YAxis.Maximum) && YAxis.MaximumRange >= newRange.Y &&
                (YAxis.ForceRangeLimit
                    ? ((YAxis.MaxRange >= newRange.Y || scaleY < 1) && (YAxis.MinRange <= newRange.Y || scaleY > 1)) // check resize range limit
                    : YAxis.MaximumRange >= newRange.Y || scaleY < 1); // check absolute range limit


            if (xCanFit && (yCanFit || ZoomMode != ZoomModes.BothKeepAspectRatio))
            {
                if (newMin.X >= XAxis.Minimum)
                {
                    if (newMax.X <= XAxis.Maximum)
                    {
                        // Completely in range -> adjust min and max
                        MinX = newMin.X;
                        MaxX = newMax.X;
                    }
                    else
                    {
                        // Max boundary hit -> adjust min
                        MinX = MaxX - newRange.X;
                    }
                }
                else
                {
                    // Min boundary hit -> adjust max
                    MaxX = MinX + newRange.X;
                }

                XScale *= scaleX;
            }

            if (yCanFit && (xCanFit || ZoomMode != ZoomModes.BothKeepAspectRatio))
            {
                if (newMin.Y >= YAxis.Minimum)
                {
                    if (newMax.Y <= YAxis.Maximum)
                    {
                        // Completely in range -> adjust min and max
                        MinY = newMin.Y;
                        MaxY = newMax.Y;
                    }
                    else
                    {
                        // Max boundary hit -> adjust min
                        MinY = MaxY - newRange.Y;
                    }
                }
                else
                {
                    // Min boundary hit -> adjust max
                    MaxY = MinY + newRange.Y;
                }

                YScale *= scaleY;
            }
        }

        #endregion

        private static SKPoint GetCenter(SKPoint p1, SKPoint p2)
        {
            return new SKPoint
                (
                    x: (p1.X + p2.X) / 2,
                    y: (p1.Y + p2.Y) / 2
                );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value">Value between 0 and 1.</param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static double LogMap(float value, float max)
        {
            return Math.Log(1 + value) / Math.Log(1 + max);
        }

        private static float Map(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }

        private static float Mod(float k, float n) { return ((k %= n) < 0) ? k + n : k; }
    }
}