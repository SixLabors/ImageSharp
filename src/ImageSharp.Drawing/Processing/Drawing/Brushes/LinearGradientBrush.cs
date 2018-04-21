using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Drawing.Brushes
{
    /// <summary>
    /// Provides an implementation of a brush for painting gradients within areas.
    /// Supported right now:
    /// - a set of colors in relative distances to each other.
    /// - two points to gradient along.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public class LinearGradientBrush<TPixel> : IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Point p1;

        private readonly Point p2;

        private readonly ColorStop[] colorStops;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearGradientBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="p1">Start point</param>
        /// <param name="p2">End point</param>
        /// <param name="colorStops">
        ///     A set of color keys and where they are.
        ///     The double should be in range [0..1] and is relative between p1 and p2.
        ///     TODO: what about the [0..1] restriction? is it necessary? If so, it should be checked, if not, it should be explained what happens for greater/smaller values.
        /// </param>
        public LinearGradientBrush(Point p1, Point p2, params ColorStop[] colorStops)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.colorStops = colorStops;
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options)
            => new LinearGradientBrushApplicator(source, this.p1, this.p2, this.colorStops, region, options);

        /// <summary>
        /// A struct that defines a single color stop.
        /// </summary>
        public struct ColorStop
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ColorStop" /> struct.
            /// </summary>
            /// <param name="ratio">Where should it be? 0 is at the start, 1 at the end of the <see cref="LinearGradientBrush{TPixel}"/>.</param>
            /// <param name="color">What color should be used at that point?</param>
            public ColorStop(float ratio, TPixel color)
            {
                this.Ratio = ratio;
                this.Color = color;
            }

            /// <summary>
            /// Gets the point along the defined <see cref="LinearGradientBrush{TPixel}" /> gradient axis.
            /// </summary>
            public float Ratio { get; }

            /// <summary>
            /// Gets the color to be used.
            /// </summary>
            public TPixel Color { get; }
        }

        /// <summary>
        /// The linear gradient brush applicator.
        /// </summary>
        private class LinearGradientBrushApplicator : BrushApplicator<TPixel>
        {
            private readonly Point start;

            private readonly Point end;

            private readonly ColorStop[] colorStops;

            /// <summary>
            /// the vector along the gradient, x component
            /// </summary>
            private readonly float alongX;

            /// <summary>
            /// the vector along the gradient, y component
            /// </summary>
            private readonly float alongY;

            /// <summary>
            /// the vector perpendicular to the gradient, y component
            /// </summary>
            private readonly float acrossY;

            /// <summary>
            /// the vector perpendicular to the gradient, x component
            /// </summary>
            private readonly float acrossX;

            /// <summary>
            /// the result of <see cref="alongX"/>^2 + <see cref="alongY"/>^2
            /// </summary>
            private readonly float alongsSquared;

            /// <summary>
            /// the length of the defined gradient (between source and end)
            /// </summary>
            private readonly float length;

            /// <summary>
            /// Initializes a new instance of the <see cref="LinearGradientBrushApplicator" /> class.
            /// </summary>
            /// <param name="source">The source</param>
            /// <param name="start">start point of the gradient</param>
            /// <param name="end">end point of the gradient</param>
            /// <param name="colorStops">tuple list of colors and their respective position between 0 and 1 on the line</param>
            /// <param name="region">the region, copied from SolidColorBrush, not sure if necessary! TODO</param>
            /// <param name="options">the graphics options</param>
            public LinearGradientBrushApplicator(
                ImageFrame<TPixel> source,
                Point start,
                Point end,
                ColorStop[] colorStops,
                RectangleF region, // TODO: use region, compare with other Brushes for reference.
                GraphicsOptions options)
                : base(source, options)
            {
                this.start = start;
                this.end = end;
                this.colorStops = colorStops; // TODO: requires colorStops to be sorted by Item1!

                // the along vector:
                this.alongX = this.end.X - this.start.X;
                this.alongY = this.end.Y - this.start.Y;

                // the cross vector:
                this.acrossX = this.alongY;
                this.acrossY = -this.alongX;

                // some helpers:
                this.alongsSquared = (this.alongX * this.alongX) + (this.alongY * this.alongY);
                this.length = (float)Math.Sqrt(this.alongsSquared);
            }

            /// <summary>
            /// Gets the color for a single pixel
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    // the following formula is the result of the linear equation system that forms the vector.
                    // TODO: this formula should be abstracted as it's the only difference between linear and radial gradient!
                    float onCompleteGradient = this.RatioOnGradient(x, y);

                    var localGradientFrom = this.colorStops[0];
                    ColorStop localGradientTo = default;

                    // TODO: ensure colorStops has at least 2 items (technically 1 would be okay, but that's no gradient)
                    foreach (var colorStop in this.colorStops)
                    {
                        localGradientTo = colorStop;
                        if (colorStop.Ratio >= onCompleteGradient)
                        {
                            // we're done here, so break it!
                            break;
                        }

                        localGradientFrom = localGradientTo;
                    }

                    TPixel resultColor = default;
                    if (localGradientFrom.Color.Equals(localGradientTo.Color))
                    {
                        resultColor = localGradientFrom.Color;
                    }
                    else
                    {
                        var fromAsVector = localGradientFrom.Color.ToVector4();
                        var toAsVector = localGradientTo.Color.ToVector4();
                        float onLocalGradient = (onCompleteGradient - localGradientFrom.Ratio) / localGradientTo.Ratio; // TODO:

                        Vector4 result = PorterDuffFunctions.Normal(
                            fromAsVector,
                            toAsVector,
                            onLocalGradient);

                        // TODO: when resultColor is a struct, what does PackFromVector4 do here?
                        resultColor.PackFromVector4(result);
                    }

                    return resultColor;
                }
            }

            private float RatioOnGradient(int x, int y)
            {
                if (this.acrossX == 0)
                {
                    return (x - this.start.X) / (float)(this.end.X - this.start.X);
                }
                else if (this.acrossY == 0)
                {
                    return (y - this.start.Y) / (float)(this.end.Y - this.start.Y);
                }
                else
                {
                    float deltaX = x - this.start.X;
                    float deltaY = y - this.start.Y;
                    float k = ((this.alongY * deltaX) - (this.alongX * deltaY)) / this.alongsSquared;

                    // point on the line:
                    float x4 = x - (k * this.alongY);
                    float y4 = y + (k * this.alongX);

                    // get distance from (x4,y4) to start
                    float distance = (float)Math.Sqrt(
                        Math.Pow(x4 - this.start.X, 2)
                        + Math.Pow(y4 - this.start.Y, 2));

                    // get and return ratio
                    float ratio = distance / this.length;
                    return ratio;
                }
            }

            internal override void Apply(Span<float> scanline, int x, int y)
            {
                base.Apply(scanline, x, y);

                // Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);
                // MemoryManager memoryManager = this.Target.MemoryManager;
                // using (IBuffer<float> amountBuffer = memoryManager.Allocate<float>(scanline.Length))
                // {
                //     Span<float> amountSpan = amountBuffer.Span;
                //
                //     for (int i = 0; i < scanline.Length; i++)
                //     {
                //         amountSpan[i] = scanline[i] * this.Options.BlendPercentage;
                //     }
                //
                //     this.Blender.Blend(memoryManager, destinationRow, destinationRow, this.Colors.Span, amountSpan);
                // }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }
        }
    }
}