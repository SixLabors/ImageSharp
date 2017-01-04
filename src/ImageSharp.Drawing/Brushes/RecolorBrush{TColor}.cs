// <copyright file="RecolorBrush{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using Processors;

    /// <summary>
    /// Provides an implementation of a brush that can recolor an image
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class RecolorBrush<TColor> : IBrush<TColor>
    where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecolorBrush{TColor}" /> class.
        /// </summary>
        /// <param name="sourceColor">Color of the source.</param>
        /// <param name="targetColor">Color of the target.</param>
        /// <param name="threshold">The threshold as a value between 0 and 1.</param>
        public RecolorBrush(TColor sourceColor, TColor targetColor, float threshold)
        {
            this.SourceColor = sourceColor;
            this.Threshold = threshold;
            this.TargetColor = targetColor;
        }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        /// <value>
        /// The threshold.
        /// </value>
        public float Threshold { get; }

        /// <summary>
        /// Gets the source color.
        /// </summary>
        /// <value>
        /// The color of the source.
        /// </value>
        public TColor SourceColor { get; }

        /// <summary>
        /// Gets the target color.
        /// </summary>
        /// <value>
        /// The color of the target.
        /// </value>
        public TColor TargetColor { get; }

        /// <inheritdoc />
        public BrushApplicator<TColor> CreateApplicator(PixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            return new RecolorBrushApplicator(sourcePixels, this.SourceColor, this.TargetColor, this.Threshold);
        }

        /// <summary>
        /// The recolor brush applicator.
        /// </summary>
        private class RecolorBrushApplicator : BrushApplicator<TColor>
        {
            /// <summary>
            /// The source pixel accessor.
            /// </summary>
            private readonly PixelAccessor<TColor> source;

            /// <summary>
            /// The source color.
            /// </summary>
            private readonly Vector4 sourceColor;

            /// <summary>
            /// The target color.
            /// </summary>
            private readonly Vector4 targetColor;

            /// <summary>
            /// The threshold.
            /// </summary>
            private readonly float threshold;

            /// <summary>
            /// Initializes a new instance of the <see cref="RecolorBrushApplicator" /> class.
            /// </summary>
            /// <param name="sourcePixels">The source pixels.</param>
            /// <param name="sourceColor">Color of the source.</param>
            /// <param name="targetColor">Color of the target.</param>
            /// <param name="threshold">The threshold .</param>
            public RecolorBrushApplicator(PixelAccessor<TColor> sourcePixels, TColor sourceColor, TColor targetColor, float threshold)
            {
                this.source = sourcePixels;
                this.sourceColor = sourceColor.ToVector4();
                this.targetColor = targetColor.ToVector4();

                // Lets hack a min max extreams for a color space by letteing the IPackedPixel clamp our values to something in the correct spaces :)
                TColor maxColor = default(TColor);
                maxColor.PackFromVector4(new Vector4(float.MaxValue));
                TColor minColor = default(TColor);
                minColor.PackFromVector4(new Vector4(float.MinValue));
                this.threshold = Vector4.DistanceSquared(maxColor.ToVector4(), minColor.ToVector4()) * threshold;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <returns>
            /// The color
            /// </returns>
            public override TColor GetColor(Vector2 point)
            {
                // Offset the requested pixel by the value in the rectangle (the shapes position)
                TColor result = this.source[(int)point.X, (int)point.Y];
                Vector4 background = result.ToVector4();
                float distance = Vector4.DistanceSquared(background, this.sourceColor);
                if (distance <= this.threshold)
                {
                    var lerpAmount = (this.threshold - distance) / this.threshold;
                    Vector4 blended = Vector4BlendTransforms.PremultipliedLerp(background, this.targetColor, lerpAmount);
                    result.PackFromVector4(blended);
                }

                return result;
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }
        }
    }
}