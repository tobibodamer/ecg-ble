using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ecg_ble_app.ViewModels;
using Plugin.Permissions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ecg_ble_app.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class EcgView : ContentPage
    {
        public EcgView()
        {
            InitializeComponent();
            BindingContext = new EcgViewModel();

            //var y = chart.YAxes.First();
            //y.MaxLimit = 256;
            //y.MinLimit = 0;

            //y.AnimationsSpeed = new TimeSpan(0);

            //var x = chart.XAxes.First();

            //chart.AnimationsSpeed = new TimeSpan(0);            
            //chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X;
        }

        public EcgViewModel ViewModel => BindingContext as EcgViewModel;

        Stopwatch stopwatch = new Stopwatch();
        bool pageIsActive;

        protected override void OnAppearing()
        {
            base.OnAppearing();
            pageIsActive = true;
            _ = AnimationLoop();

            ViewModel.Values.CollectionChanged += Values_CollectionChanged;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(ViewModel.PollingRate)))
            {
                UpdateSizing();
            }
        }

        private void UpdateSizing()
        {
            GridSize = new SKSize(170.6666f, ViewModel.PollingRate * 0.2f);
            FineGridSize = new SKSize(170.6666f / 5.0f, ViewModel.PollingRate * 0.04f);
            XScale = 350.0f / ViewModel.PollingRate; // 500px/s
            YScale = 50 / 170.6666f;
        }

        List<double> filtered = new List<double>();

        LowpassFilterButterworthImplementation _filter = new LowpassFilterButterworthImplementation(40, 1, 1000);
        private void Values_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            int length = ViewModel.Values.Count;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                for (int i = ViewModel.Values.Count - e.NewItems.Count; i < ViewModel.Values.Count; i++)
                {
                    filtered.Add(_filter.compute(ViewModel.Values[i]));
                }

                if (length <= MaxX)
                {
                    return;
                }

                switch (TimeMode)
                {
                    case TimeModes.Fit:

                        MaxX += e.NewItems.Count;
                        break;
                    case TimeModes.Scrolling:
                        MinX += e.NewItems.Count;
                        MaxX += e.NewItems.Count;
                        break;
                    case TimeModes.Reset:
                        _lastWindow = new SKRect(MinX, MaxY, MaxX, MinY);
                        MaxX = length + RangeX;
                        MinX = length;
                        return;
                    case TimeModes.Pinned:
                    default:
                        break;
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            pageIsActive = false;

            ViewModel.Values.CollectionChanged -= Values_CollectionChanged;
        }



        async Task AnimationLoop()
        {
            stopwatch.Start();

            //UpdateSizing();

            while (pageIsActive)
            {
                canvasView.InvalidateSurface();
                await Task.Delay(TimeSpan.FromSeconds(1.0 / 90));
            }

            stopwatch.Stop();
        }

        //public int PollingRate { get; set; } = 1000;

        public bool DrawLastWindow { get; set; } = true;

        private SKRect _lastWindow = SKRect.Empty;

        public enum SizeModes
        {
            FitToHeight,
            FitToWidth,
            FitToWidthAndHeight,
            Scale
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
        public float MaxX { get; set; } = 2400;

        public float MinY { get; set; } = -1000;
        public float MaxY { get; set; } = 2000;

        public float XScale { get; set; } = 1;
        public float YScale { get; set; } = 1;

        public float RangeX => Math.Abs(MaxX - MinX);
        public float RangeY => Math.Abs(MaxY - MinY);

        public float AspectRatio => XScale / YScale;


        public SKPoint ZeroPoint = new SKPoint(0, 512);


        public float FadeOutStart => RangeX * 0.8f;
        public float FadeOutEnd => RangeX * 0.9f;
        public float FadeOutLength => FadeOutEnd - FadeOutStart;


        public bool ShowGrid { get; set; } = true;
        public SKColor GridColor { get; set; } = SKColor.Parse("de453a");
        public SKSize GridSize { get; set; } = new SKSize(100, 100);
        public SKSize FineGridSize { get; set; } = new SKSize(10, 10);

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

        private SKMatrix _chartToDeviceMatrix, _deviceToChartMatrix;

        private void ConfigureScale(float height, float width)
        {
            // NOTE: This function needs to be only called on resize

            float aspectRatio = AspectRatio;

            switch (SizeMode)
            {
                case SizeModes.FitToHeight:
                    YScale = height / RangeY;
                    XScale = YScale * aspectRatio; // Adjust x scale
                    MaxX = MinX + (width / XScale); // Fit x range
                    break;
                case SizeModes.FitToWidth:
                    XScale = width / RangeX;
                    YScale = XScale / AspectRatio;
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
                ? width - Math.Abs(((MinX + ZeroPoint.X) % width))
                : 0;

            float yOffset = GridAlignment.HasFlag(AlignGrid.YAxis)
                ? height - Math.Abs(((MinY + ZeroPoint.Y) % height))
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
                    paint.Color = GridColor;
                    paint.StrokeWidth = 2;

                    var DeviceMin = new SKPoint(0, 0);
                    var DeviceMax = new SKPoint(info.Width - 0, info.Height - 0);
                    var ChartMin = new SKPoint(MinX, MinY);
                    var ChartMax = new SKPoint(MaxX, MaxY);

                    // Prepare the transformation matrices.
                    this.ConfigureTransforms(ChartMin, ChartMax, DeviceMin, DeviceMax);

                    // Draw big grid
                    DrawGrid(canvas, paint, GridSize.Height, GridSize.Width);

                    paint.StrokeWidth = 1;

                    // Draw fine grid
                    DrawGrid(canvas, paint, FineGridSize.Height, FineGridSize.Width);
                }
            }


            DrawValues(MinX, MinY, MaxX, MaxY, 0, args);

            if (TimeMode == TimeModes.Reset && DrawLastWindow && !_lastWindow.IsEmpty)
            {
                int x0 = (int)Math.Floor(MinX);
                int xOffset = Math.Max(0, (ViewModel.Values.Count - 1) - x0);

                DrawValues(_lastWindow.Left, _lastWindow.Bottom, _lastWindow.Right, _lastWindow.Top, xOffset, args);
            }
        }

        public static double[] Butterworth(double[] indata, double Samplingrate, double CutOff)
        {
            if (indata == null) return null;
            if (CutOff == 0) return indata;

            long dF2 = indata.Length - 1;        // The data range is set with dF2
            double[] Dat2 = new double[dF2 + 4]; // Array with 4 extra points front and back
            double[] data = indata; // Ptr., changes passed data

            // Copy indata to Dat2
            for (long r = 0; r < dF2; r++)
            {
                Dat2[2 + r] = indata[r];
            }
            Dat2[1] = Dat2[0] = indata[0];
            Dat2[dF2 + 3] = Dat2[dF2 + 2] = indata[dF2];

            const double pi = 3.14159265358979;
            double wc = Math.Tan(CutOff * pi / Samplingrate);
            double k1 = 1.414213562 * wc; // Sqrt(2) * wc
            double k2 = wc * wc;
            double a = k2 / (1 + k1 + k2);
            double b = 2 * a;
            double c = a;
            double k3 = b / k2;
            double d = -2 * a + k3;
            double e = 1 - (2 * a) - k3;

            // RECURSIVE TRIGGERS - ENABLE filter is performed (first, last points constant)
            double[] DatYt = new double[dF2 + 4];
            DatYt[1] = DatYt[0] = indata[0];
            for (long s = 2; s < dF2 + 2; s++)
            {
                DatYt[s] = a * Dat2[s] + b * Dat2[s - 1] + c * Dat2[s - 2]
                           + d * DatYt[s - 1] + e * DatYt[s - 2];
            }
            DatYt[dF2 + 3] = DatYt[dF2 + 2] = DatYt[dF2 + 1];

            // FORWARD filter
            double[] DatZt = new double[dF2 + 2];
            DatZt[dF2] = DatYt[dF2 + 2];
            DatZt[dF2 + 1] = DatYt[dF2 + 3];
            for (long t = -dF2 + 1; t <= 0; t++)
            {
                DatZt[-t] = a * DatYt[-t + 2] + b * DatYt[-t + 3] + c * DatYt[-t + 4]
                            + d * DatZt[-t + 1] + e * DatZt[-t + 2];
            }

            // Calculated points copied for return
            for (long p = 0; p < dF2; p++)
            {
                data[p] = DatZt[p];
            }

            return data;
        }

        private void DrawValues(float minX, float minY, float maxX, float maxY, int xOffset, SKPaintSurfaceEventArgs args)
        {
            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            using (SKPaint paint = new SKPaint())
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = SKColors.Black;
                paint.StrokeWidth = 3;

                int x0 = (int)Math.Floor(minX + xOffset);
                int xEnd = (int)Math.Min(Math.Ceiling(maxX), ViewModel.Values.Count - 1);

                if (x0 >= ViewModel.Values.Count - 1)
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


                for (int x = x0; x < xEnd - 1; x++)
                {
                    int x1 = x;
                    int x2 = x + 1;


                    int y1 = (int)ViewModel.Values[x1]; //ViewModel.Values[x1]; 
                    int y2 = (int)ViewModel.Values[x2]; //ViewModel.Values[x2];

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
                        int age = (ViewModel.Values.Count - 1) - x;

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