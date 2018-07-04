// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Gradient Brush with elliptic shape.
    /// The ellipse is defined by a center point,
    /// a point on the longest extension of the ellipse and
    /// the ratio between longest and shortest extension.
    /// </summary>
    /// <typeparam name="TPixel">The Pixel format that is used.</typeparam>
    public sealed class EllipticGradientBrush<TPixel> : GradientBrushBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Point center;

        private readonly Point referenceAxisEnd;

        private readonly float axisRatio;

        /// <inheritdoc cref="GradientBrushBase{TPixel}" />
        /// <param name="center">The center of the elliptical gradient and 0 for the color stops.</param>
        /// <param name="referenceAxisEnd">The end point of the reference axis of the ellipse.</param>
        /// <param name="axisRatio">
        ///   The ratio of the axis widths.
        ///   The second axis' is perpendicular to the reference axis and
        ///   it's length is the reference axis' length multiplied by this factor.
        /// </param>
        /// <param name="repetitionMode">Defines how the colors of the gradients are repeated.</param>
        /// <param name="colorStops">the color stops as defined in base class.</param>
        public EllipticGradientBrush(
            Point center,
            Point referenceAxisEnd,
            float axisRatio,
            GradientRepetitionMode repetitionMode,
            params ColorStop<TPixel>[] colorStops)
            : base(repetitionMode, colorStops)
        {
            this.center = center;
            this.referenceAxisEnd = referenceAxisEnd;
            this.axisRatio = axisRatio;
        }

        /// <inheritdoc cref="CreateApplicator" />
        public override BrushApplicator<TPixel> CreateApplicator(
            ImageFrame<TPixel> source,
            RectangleF region,
            GraphicsOptions options) =>
            new RadialGradientBrushApplicator(
                source,
                options,
                this.center,
                this.referenceAxisEnd,
                this.axisRatio,
                this.ColorStops,
                this.RepetitionMode);

        /// <inheritdoc />
        private sealed class RadialGradientBrushApplicator : GradientBrushApplicatorBase
        {
            private readonly Point center;

            private readonly Point referenceAxisEnd;

            private readonly float axisRatio;

            private readonly double rotation;

            private readonly float referenceRadius;

            private readonly float secondRadius;

            private readonly float cosRotation;

            private readonly float sinRotation;

            private readonly float secondRadiusSquared;

            private readonly float referenceRadiusSquared;

            /// <summary>
            /// Initializes a new instance of the <see cref="RadialGradientBrushApplicator" /> class.
            /// </summary>
            /// <param name="target">The target image</param>
            /// <param name="options">The options</param>
            /// <param name="center">Center of the ellipse</param>
            /// <param name="referenceAxisEnd">Point on one angular points of the ellipse.</param>
            /// <param name="axisRatio">
            /// Ratio of the axis length's. Used to determine the length of the second axis,
            /// the first is defined by <see cref="center"/> and <see cref="referenceAxisEnd"/>.</param>
            /// <param name="colorStops">Definition of colors</param>
            /// <param name="repetitionMode">Defines how the gradient colors are repeated.</param>
            public RadialGradientBrushApplicator(
                ImageFrame<TPixel> target,
                GraphicsOptions options,
                Point center,
                Point referenceAxisEnd,
                float axisRatio,
                ColorStop<TPixel>[] colorStops,
                GradientRepetitionMode repetitionMode)
                : base(target, options, colorStops, repetitionMode)
            {
                this.center = center;
                this.referenceAxisEnd = referenceAxisEnd;
                this.axisRatio = axisRatio;
                this.rotation = this.AngleBetween(
                    this.center,
                    new PointF(this.center.X + 1, this.center.Y),
                    this.referenceAxisEnd);
                this.referenceRadius = this.DistanceBetween(this.center, this.referenceAxisEnd);
                this.secondRadius = this.referenceRadius * this.axisRatio;

                this.referenceRadiusSquared = this.referenceRadius * this.referenceRadius;
                this.secondRadiusSquared = this.secondRadius * this.secondRadius;

                this.sinRotation = (float)Math.Sin(this.rotation);
                this.cosRotation = (float)Math.Cos(this.rotation);
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }

            /// <inheritdoc />
            protected override float PositionOnGradient(int xt, int yt)
            {
                float x0 = xt - this.center.X;
                float y0 = yt - this.center.Y;

                float x = (x0 * this.cosRotation) - (y0 * this.sinRotation);
                float y = (x0 * this.sinRotation) + (y0 * this.cosRotation);

                float xSquared = x * x;
                float ySquared = y * y;

                var inBoundaryChecker = (xSquared / this.referenceRadiusSquared)
                                        + (ySquared / this.secondRadiusSquared);

                return inBoundaryChecker;
            }

            private float AngleBetween(PointF junction, PointF a, PointF b)
            {
                var vA = a - junction;
                var vB = b - junction;
                return (float)(Math.Atan2(vB.Y, vB.X)
                       - Math.Atan2(vA.Y, vA.X));
            }

            private float DistanceBetween(
                PointF p1,
                PointF p2)
            {
                float dX = p1.X - p2.X;
                float dXsquared = dX * dX;

                float dY = p1.Y - p2.Y;
                float dYsquared = dY * dY;
                return (float)Math.Sqrt(dXsquared + dYsquared);
            }
        }
    }
}