// <copyright file="ImageBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Provides an implementation of an image brush for painting images within areas.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class ImageBrush<TPixel> : IBrush<TPixel>
    where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The image to paint.
        /// </summary>
        private readonly ImageBase<TPixel> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image.</param>
        public ImageBrush(ImageBase<TPixel> image)
        {
            this.image = image;
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageBase<TPixel> source, RectangleF region, GraphicsOptions options)
        {
            return new ImageBrushApplicator(source, this.image, region, options);
        }

        /// <summary>
        /// The image brush applicator.
        /// </summary>
        private class ImageBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The source image.
            /// </summary>
            private readonly ImageBase<TPixel> source;

            /// <summary>
            /// The y-length.
            /// </summary>
            private readonly int yLength;

            /// <summary>
            /// The x-length.
            /// </summary>
            private readonly int xLength;

            /// <summary>
            /// The Y offset.
            /// </summary>
            private readonly int offsetY;

            /// <summary>
            /// The X offset.
            /// </summary>
            private readonly int offsetX;

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageBrushApplicator"/> class.
            /// </summary>
            /// <param name="target">The target image.</param>
            /// <param name="image">The image.</param>
            /// <param name="region">The region.</param>
            /// <param name="options">The options</param>
            public ImageBrushApplicator(ImageBase<TPixel> target, ImageBase<TPixel> image, RectangleF region, GraphicsOptions options)
                : base(target, options)
            {
                this.source = image;
                this.xLength = image.Width;
                this.yLength = image.Height;
                this.offsetY = (int)MathF.Max(MathF.Floor(region.Top), 0);
                this.offsetX = (int)MathF.Max(MathF.Floor(region.Left), 0);
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
                    int srcX = (x - this.offsetX) % this.xLength;
                    int srcY = (y - this.offsetY) % this.yLength;
                    return this.source[srcX, srcY];
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                this.source.Dispose();
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                // Create a span for colors
                using (var amountBuffer = new Buffer<float>(scanline.Length))
                using (var overlay = new Buffer<TPixel>(scanline.Length))
                {
                    int sourceY = (y - this.offsetY) % this.yLength;
                    int offsetX = x - this.offsetX;
                    Span<TPixel> sourceRow = this.source.GetRowSpan(sourceY);

                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountBuffer[i] = scanline[i] * this.Options.BlendPercentage;

                        int sourceX = (i + offsetX) % this.xLength;
                        TPixel pixel = sourceRow[sourceX];
                        overlay[i] = pixel;
                    }

                    Span<TPixel> destinationRow = this.Target.GetRowSpan(x, y).Slice(0, scanline.Length);
                    this.Blender.Blend(destinationRow, destinationRow, overlay, amountBuffer);
                }
            }
        }
    }
}