// <copyright file="RecolorBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Provides an implementation of a brush that can recolor an image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class RecolorBrush<TPixel> : IBrush<TPixel>
    where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecolorBrush{TPixel}" /> class.
        /// </summary>
        /// <param name="sourceColor">Color of the source.</param>
        /// <param name="targeTPixel">Color of the target.</param>
        /// <param name="threshold">The threshold as a value between 0 and 1.</param>
        public RecolorBrush(TPixel sourceColor, TPixel targeTPixel, float threshold)
        {
            this.SourceColor = sourceColor;
            this.Threshold = threshold;
            this.TargeTPixel = targeTPixel;
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
        public TPixel SourceColor { get; }

        /// <summary>
        /// Gets the target color.
        /// </summary>
        /// <value>
        /// The color of the target.
        /// </value>
        public TPixel TargeTPixel { get; }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageBase<TPixel> source, RectangleF region, GraphicsOptions options)
        {
            return new RecolorBrushApplicator(source, this.SourceColor, this.TargeTPixel, this.Threshold, options);
        }

        /// <summary>
        /// The recolor brush applicator.
        /// </summary>
        private class RecolorBrushApplicator : BrushApplicator<TPixel>
        {
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

            private readonly TPixel targetColorPixel;

            /// <summary>
            /// Initializes a new instance of the <see cref="RecolorBrushApplicator" /> class.
            /// </summary>
            /// <param name="source">The source image.</param>
            /// <param name="sourceColor">Color of the source.</param>
            /// <param name="targetColor">Color of the target.</param>
            /// <param name="threshold">The threshold .</param>
            /// <param name="options">The options</param>
            public RecolorBrushApplicator(ImageBase<TPixel> source, TPixel sourceColor, TPixel targetColor, float threshold, GraphicsOptions options)
                : base(source, options)
            {
                this.sourceColor = sourceColor.ToVector4();
                this.targetColor = targetColor.ToVector4();
                this.targetColorPixel = targetColor;

                // Lets hack a min max extreams for a color space by letting the IPackedPixel clamp our values to something in the correct spaces :)
                var maxColor = default(TPixel);
                maxColor.PackFromVector4(new Vector4(float.MaxValue));
                var minColor = default(TPixel);
                minColor.PackFromVector4(new Vector4(float.MinValue));
                this.threshold = Vector4.DistanceSquared(maxColor.ToVector4(), minColor.ToVector4()) * threshold;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    // Offset the requested pixel by the value in the rectangle (the shapes position)
                    TPixel result = this.Target[x, y];
                    var background = result.ToVector4();
                    float distance = Vector4.DistanceSquared(background, this.sourceColor);
                    if (distance <= this.threshold)
                    {
                        float lerpAmount = (this.threshold - distance) / this.threshold;
                        return this.Blender.Blend(
                            result,
                            this.targetColorPixel,
                            lerpAmount);
                    }

                    return result;
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                using (var amountBuffer = new Buffer<float>(scanline.Length))
                using (var overlay = new Buffer<TPixel>(scanline.Length))
                {
                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountBuffer[i] = scanline[i] * this.Options.BlendPercentage;

                        int offsetX = x + i;

                        // no doubt this one can be optermised further but I can't imagine its
                        // actually being used and can probably be removed/interalised for now
                        overlay[i] = this[offsetX, y];
                    }

                    Span<TPixel> destinationRow = this.Target.GetRowSpan(x, y).Slice(0, scanline.Length);
                    this.Blender.Blend(destinationRow, destinationRow, overlay, amountBuffer);
                }
            }
        }
    }
}