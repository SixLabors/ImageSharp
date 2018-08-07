// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
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
        /// <param name="targetColor">Color of the target.</param>
        /// <param name="threshold">The threshold as a value between 0 and 1.</param>
        public RecolorBrush(TPixel sourceColor, TPixel targetColor, float threshold)
        {
            this.SourceColor = sourceColor;
            this.Threshold = threshold;
            this.TargetColor = targetColor;
        }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
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
        public TPixel TargetColor { get; }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options)
        {
            return new RecolorBrushApplicator(source, this.SourceColor, this.TargetColor, this.Threshold, options);
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
            public RecolorBrushApplicator(ImageFrame<TPixel> source, TPixel sourceColor, TPixel targetColor, float threshold, GraphicsOptions options)
                : base(source, options)
            {
                this.sourceColor = sourceColor.ToVector4();
                this.targetColor = targetColor.ToVector4();
                this.targetColorPixel = targetColor;

                // Lets hack a min max extremes for a color space by letting the IPackedPixel clamp our values to something in the correct spaces :)
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
                MemoryAllocator memoryAllocator = this.Target.MemoryAllocator;

                using (IMemoryOwner<float> amountBuffer = memoryAllocator.Allocate<float>(scanline.Length))
                using (IMemoryOwner<TPixel> overlay = memoryAllocator.Allocate<TPixel>(scanline.Length))
                {
                    Span<float> amountSpan = amountBuffer.GetSpan();
                    Span<TPixel> overlaySpan = overlay.GetSpan();

                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountSpan[i] = scanline[i] * this.Options.BlendPercentage;

                        int offsetX = x + i;

                        // No doubt this one can be optimized further but I can't imagine its
                        // actually being used and can probably be removed/internalized for now
                        overlaySpan[i] = this[offsetX, y];
                    }

                    Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);
                    this.Blender.Blend(memoryAllocator, destinationRow, destinationRow, overlaySpan, amountSpan);
                }
            }
        }
    }
}