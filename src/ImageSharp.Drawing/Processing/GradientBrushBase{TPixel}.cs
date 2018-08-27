// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Base class for Gradient brushes
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public abstract class GradientBrushBase<TPixel> : IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc cref="IBrush{TPixel}"/>
        /// <param name="repetitionMode">Defines how the colors are repeated beyond the interval [0..1]</param>
        /// <param name="colorStops">The gradient colors.</param>
        protected GradientBrushBase(
            GradientRepetitionMode repetitionMode,
            params ColorStop<TPixel>[] colorStops)
        {
            this.RepetitionMode = repetitionMode;
            this.ColorStops = colorStops;
        }

        /// <summary>
        /// Gets how the colors are repeated beyond the interval [0..1].
        /// </summary>
        protected GradientRepetitionMode RepetitionMode { get; }

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
        protected abstract class GradientBrushApplicatorBase : BrushApplicator<TPixel>
        {
            private readonly ColorStop<TPixel>[] colorStops;

            private readonly GradientRepetitionMode repetitionMode;

            /// <summary>
            /// Initializes a new instance of the <see cref="GradientBrushApplicatorBase"/> class.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="options">The options.</param>
            /// <param name="colorStops">An array of color stops sorted by their position.</param>
            /// <param name="repetitionMode">Defines if and how the gradient should be repeated.</param>
            protected GradientBrushApplicatorBase(
                ImageFrame<TPixel> target,
                GraphicsOptions options,
                ColorStop<TPixel>[] colorStops,
                GradientRepetitionMode repetitionMode)
                : base(target, options)
            {
                this.colorStops = colorStops; // TODO: requires colorStops to be sorted by position - should that be checked?
                this.repetitionMode = repetitionMode;
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

                    switch (this.repetitionMode)
                    {
                        case GradientRepetitionMode.None:
                            // do nothing. The following could be done, but is not necessary:
                            // onLocalGradient = Math.Min(0, Math.Max(1, onLocalGradient));
                            break;
                        case GradientRepetitionMode.Repeat:
                            positionOnCompleteGradient = positionOnCompleteGradient % 1;
                            break;
                        case GradientRepetitionMode.Reflect:
                            positionOnCompleteGradient = positionOnCompleteGradient % 2;
                            if (positionOnCompleteGradient > 1)
                            {
                                positionOnCompleteGradient = 2 - positionOnCompleteGradient;
                            }

                            break;
                        case GradientRepetitionMode.DontFill:
                            if (positionOnCompleteGradient > 1 || positionOnCompleteGradient < 0)
                            {
                                return NamedColors<TPixel>.Transparent;
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    (ColorStop<TPixel> from, ColorStop<TPixel> to) = this.GetGradientSegment(positionOnCompleteGradient);

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
                        Vector4 result = PorterDuffFunctions.NormalSrcOver(
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
                ColorStop<TPixel> localGradientFrom = this.colorStops[0];
                ColorStop<TPixel> localGradientTo = default;

                // TODO: ensure colorStops has at least 2 items (technically 1 would be okay, but that's no gradient)
                foreach (ColorStop<TPixel> colorStop in this.colorStops)
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