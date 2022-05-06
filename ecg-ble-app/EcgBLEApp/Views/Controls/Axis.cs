using System;

namespace EcgBLEApp.Views
{
    public class Axis
    {
        /// <summary>
        /// The lower boundary for this axis.
        /// </summary>
        public float Minimum { get; set; }

        /// <summary>
        /// The upper boundary for this axis.
        /// </summary>
        public float Maximum { get; set; }

        public float MaximumRange => Math.Abs(Maximum - Minimum);

        public float? MinRange { get; set; }
        public float? MaxRange { get; set; }

        /// <summary>
        /// Decides what to do to keep the <see cref="MinRange"/> and <see cref="MaxRange"/>.
        /// </summary>
        public SizeLimitMode SizeLimitMode { get; set; }

        /// <summary>
        /// If true, the range limit will be forced when the user changes the range.
        /// </summary>
        public bool ForceRangeLimit { get; set; }

        public Axis() : this(float.MinValue, float.MaxValue) { }
        public Axis(float minimum = float.MinValue, float maximum = float.MaxValue)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    public enum SizeLimitMode
    {
        /// <summary>
        /// Skew the aspect ratio to keep the min / max range constraint when resizing.
        /// </summary>
        Skew,

        /// <summary>
        /// Try to keep the aspect ratio by changing the range of the other axis.
        /// </summary>
        Fit,

        /// <summary>
        /// Ignore the size limit.
        /// </summary>
        Ignore
    }
}