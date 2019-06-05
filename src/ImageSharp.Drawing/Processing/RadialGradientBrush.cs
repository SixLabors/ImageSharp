// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A Circular Gradient Brush, defined by center point and radius.
    /// </summary>
    public sealed class RadialGradientBrush : GradientBrush
    {
        private readonly PointF center;

        private readonly float radius;

        /// <inheritdoc cref="GradientBrush" />
        /// <param name="center">The center of the circular gradient and 0 for the color stops.</param>
        /// <param name="radius">The radius of the circular gradient and 1 for the color stops.</param>
        /// <param name="repetitionMode">Defines how the colors in the gradient are repeated.</param>
        /// <param name="colorStops">the color stops as defined in base class.</param>
        public RadialGradientBrush(
            PointF center,
            float radius,
            GradientRepetitionMode repetitionMode,
            params ColorStop[] colorStops)
            : base(repetitionMode, colorStops)
        {
            this.center = center;
            this.radius = radius;
        }

        /// <inheritdoc />
        public override BrushApplicator<TPixel> CreateApplicator<TPixel>(
            ImageFrame<TPixel> source,
            RectangleF region,
            GraphicsOptions options) =>
            new RadialGradientBrushApplicator<TPixel>(
                source,
                options,
                this.center,
                this.radius,
                this.ColorStops,
                this.RepetitionMode);

        /// <inheritdoc />
        private sealed class RadialGradientBrushApplicator<TPixel> : GradientBrushApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly PointF center;

            private readonly float radius;

            /// <summary>
            /// Initializes a new instance of the <see cref="RadialGradientBrushApplicator{TPixel}" /> class.
            /// </summary>
            /// <param name="target">The target image</param>
            /// <param name="options">The options.</param>
            /// <param name="center">Center point of the gradient.</param>
            /// <param name="radius">Radius of the gradient.</param>
            /// <param name="colorStops">Definition of colors.</param>
            /// <param name="repetitionMode">How the colors are repeated beyond the first gradient.</param>
            public RadialGradientBrushApplicator(
                ImageFrame<TPixel> target,
                GraphicsOptions options,
                PointF center,
                float radius,
                ColorStop[] colorStops,
                GradientRepetitionMode repetitionMode)
                : base(target, options, colorStops, repetitionMode)
            {
                this.center = center;
                this.radius = radius;
            }

            /// <inheritdoc cref="Dispose" />
            public override void Dispose()
            {
            }

            /// <summary>
            /// As this is a circular gradient, the position on the gradient is based on
            /// the distance of the point to the center.
            /// </summary>
            /// <param name="x">The X coordinate of the target pixel.</param>
            /// <param name="y">The Y coordinate of the target pixel.</param>
            /// <returns>the position on the color gradient.</returns>
            protected override float PositionOnGradient(float x, float y)
            {
                float distance = MathF.Sqrt(MathF.Pow(this.center.X - x, 2) + MathF.Pow(this.center.Y - y, 2));
                return distance / this.radius;
            }

            internal override void Apply(Span<float> scanline, int x, int y)
            {
                // TODO: each row is symmetric across center, so we can calculate half of it and mirror it to improve performance.
                base.Apply(scanline, x, y);
            }
        }
    }
}