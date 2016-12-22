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
        /// <param name="threashold">The threashold as a value between 0 and 1.</param>
        public RecolorBrush(TColor sourceColor, TColor targetColor, float threashold)
        {
            this.SourceColor = sourceColor;
            this.Threashold = threashold;
            this.TargetColor = targetColor;
        }

        /// <summary>
        /// Gets the threashold.
        /// </summary>
        /// <value>
        /// The threashold.
        /// </value>
        public float Threashold { get; }

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
        public IBrushApplicator<TColor> CreateApplicator(IReadonlyPixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            return new RecolorBrushApplicator(sourcePixels, this.SourceColor, this.TargetColor, this.Threashold);
        }

        /// <summary>
        /// The recolor brush applicator.
        /// </summary>
        private class RecolorBrushApplicator : IBrushApplicator<TColor>
        {
            /// <summary>
            /// The source pixel accessor.
            /// </summary>
            private readonly IReadonlyPixelAccessor<TColor> source;
            private readonly Vector4 sourceColor;
            private readonly Vector4 targetColor;
            private readonly float threashold;
            private readonly float totalDistance;

            /// <summary>
            /// Initializes a new instance of the <see cref="RecolorBrushApplicator" /> class.
            /// </summary>
            /// <param name="sourcePixels">The source pixels.</param>
            /// <param name="sourceColor">Color of the source.</param>
            /// <param name="targetColor">Color of the target.</param>
            /// <param name="threashold">The threashold .</param>
            public RecolorBrushApplicator(IReadonlyPixelAccessor<TColor> sourcePixels, TColor sourceColor, TColor targetColor, float threashold)
            {
                this.source = sourcePixels;
                this.sourceColor = sourceColor.ToVector4();
                this.targetColor = targetColor.ToVector4();

                // lets hack a min max extreams for a color space by letteing the IPackedPixle clamp our values to something in the correct spaces :)
                TColor maxColor = default(TColor);
                maxColor.PackFromVector4(new Vector4(float.MaxValue));
                TColor minColor = default(TColor);
                minColor.PackFromVector4(new Vector4(float.MinValue));
                this.totalDistance = Vector4.DistanceSquared(maxColor.ToVector4(), minColor.ToVector4());
                this.threashold = this.totalDistance * threashold;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <returns>
            /// The color
            /// </returns>
            public TColor GetColor(Vector2 point)
            {
                // Offset the requested pixel by the value in the rectangle (the shapes position)
                TColor result = this.source[(int)point.X, (int)point.Y];
                Vector4 background = result.ToVector4();
                float distance = Vector4.DistanceSquared(background, this.sourceColor);
                if (distance <= this.threashold)
                {
                    var lerpAmount = (this.threashold - distance) / this.threashold;
                    Vector4 blended = Vector4BlendTransforms.PremultipliedLerp(background, this.targetColor, lerpAmount);
                    result.PackFromVector4(blended);
                }

                return result;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                // we didn't make the lock on the PixelAccessor we shouldn't release it.
            }
        }
    }
}