// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides an implementation of a brush for painting linear gradients within areas.
    /// Supported right now:
    /// - a set of colors in relative distances to each other.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public sealed class LinearGradientBrush<TPixel> : GradientBrushBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Point p1;

        private readonly Point p2;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearGradientBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="p1">Start point</param>
        /// <param name="p2">End point</param>
        /// <param name="repetitionMode">defines how colors are repeated.</param>
        /// <param name="colorStops"><inheritdoc /></param>
        public LinearGradientBrush(
            Point p1,
            Point p2,
            GradientRepetitionMode repetitionMode,
            params ColorStop<TPixel>[] colorStops)
            : base(repetitionMode, colorStops)
        {
            this.p1 = p1;
            this.p2 = p2;
        }

        /// <inheritdoc />
        public override BrushApplicator<TPixel> CreateApplicator(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options)
            => new LinearGradientBrushApplicator(source, this.p1, this.p2, this.ColorStops, this.RepetitionMode, options);

        /// <summary>
        /// The linear gradient brush applicator.
        /// </summary>
        private sealed class LinearGradientBrushApplicator : GradientBrushApplicatorBase
        {
            private readonly Point start;

            private readonly Point end;

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
            /// <param name="repetitionMode">defines how the gradient colors are repeated.</param>
            /// <param name="options">the graphics options</param>
            public LinearGradientBrushApplicator(
                ImageFrame<TPixel> source,
                Point start,
                Point end,
                ColorStop<TPixel>[] colorStops,
                GradientRepetitionMode repetitionMode,
                GraphicsOptions options)
                : base(source, options, colorStops, repetitionMode)
            {
                this.start = start;
                this.end = end;

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

            protected override float PositionOnGradient(int x, int y)
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

            public override void Dispose()
            {
            }
        }
    }
}