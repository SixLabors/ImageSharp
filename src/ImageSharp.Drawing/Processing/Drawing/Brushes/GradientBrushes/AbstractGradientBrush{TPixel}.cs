using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes
{
    /// <summary>
    /// Base class for Gradient brushes
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public abstract class AbstractGradientBrush<TPixel> : IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc cref="IBrush{TPixel}"/>
        /// <param name="colorStops">The gradient colors.</param>
        protected AbstractGradientBrush(params ColorStop<TPixel>[] colorStops)
        {
            this.ColorStops = colorStops;
        }

        /// <summary>
        /// Gets the list of color stops for this gradient.
        /// </summary>
        protected ColorStop<TPixel>[] ColorStops { get; }

        /// <inheritdoc cref="IBrush{TPixel}" />
        public abstract BrushApplicator<TPixel> CreateApplicator(
            ImageFrame<TPixel> source,
            RectangleF region,
            GraphicsOptions options);

        /// <summary>
        /// Base class for gradient brush applicators
        /// </summary>
        protected abstract class AbstractGradientBrushApplicator : BrushApplicator<TPixel>
        {
            private readonly ColorStop<TPixel>[] colorStops;

            /// <summary>
            /// Initializes a new instance of the <see cref="AbstractGradientBrushApplicator"/> class.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="options">The options.</param>
            /// <param name="colorStops">an array of color stops sorted by their position.</param>
            /// <param name="region">TODO: use region, compare with other Brushes for reference</param>
            protected AbstractGradientBrushApplicator(
                ImageFrame<TPixel> target,
                GraphicsOptions options,
                ColorStop<TPixel>[] colorStops,
                RectangleF region)
                : base(target, options)
            {
                this.colorStops = colorStops; // TODO: requires colorStops to be sorted by position - should that be checked?
            }

            /// <summary>
            /// Base implementation of the indexer for gradients
            /// (follows the facade pattern, using abstract methods)
            /// </summary>
            /// <param name="x">X coordinate of the Pixel.</param>
            /// <param name="y">Y coordinate of the Pixel.</param>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    float positionOnCompleteGradient = this.PositionOnGradient(x, y);
                    var (from, to) = this.GetGradientSegment(positionOnCompleteGradient);

                    if (from.Color.Equals(to.Color))
                    {
                        return from.Color;
                    }
                    else
                    {
                        var fromAsVector = from.Color.ToVector4();
                        var toAsVector = to.Color.ToVector4();
                        float onLocalGradient = (positionOnCompleteGradient - from.Ratio) / to.Ratio;

                        // TODO: this should be changeble for different gradienting functions
                        Vector4 result = PorterDuffFunctions.Normal(
                            fromAsVector,
                            toAsVector,
                            onLocalGradient);

                        TPixel resultColor = default;
                        resultColor.PackFromVector4(result);
                        return resultColor;
                    }
                }
            }

            /// <summary>
            /// calculates the position on the gradient for a given pixel.
            /// This method is abstract as it's content depends on the shape of the gradient.
            /// </summary>
            /// <param name="x">The x coordinate of the pixel</param>
            /// <param name="y">The y coordinate of the pixel</param>
            /// <returns>
            /// The position the given pixel has on the gradient.
            /// The position is not bound to the [0..1] interval.
            /// Values outside of that interval may be treated differently,
            /// e.g. for the <see cref="GradientRepetitionMode" /> enum.
            /// </returns>
            protected abstract float PositionOnGradient(int x, int y);

            private (ColorStop<TPixel> from, ColorStop<TPixel> to) GetGradientSegment(
                float positionOnCompleteGradient)
            {
                var localGradientFrom = this.colorStops[0];
                ColorStop<TPixel> localGradientTo = default;

                // TODO: ensure colorStops has at least 2 items (technically 1 would be okay, but that's no gradient)
                foreach (var colorStop in this.colorStops)
                {
                    localGradientTo = colorStop;

                    if (colorStop.Ratio > positionOnCompleteGradient)
                    {
                        // we're done here, so break it!
                        break;
                    }

                    localGradientFrom = localGradientTo;
                }

                return (localGradientFrom, localGradientTo);
            }
        }
    }
}