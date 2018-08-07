// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
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
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image.</param>
        public ImageBrush(ImageFrame<TPixel> image)
        {
            this.image = image;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image.</param>
        public ImageBrush(Image<TPixel> image)
            : this(image.Frames.RootFrame)
        {
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options)
            => new ImageBrushApplicator(source, this.image, region, options);

        /// <summary>
        /// The image brush applicator.
        /// </summary>
        private class ImageBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The source image.
            /// </summary>
            private readonly ImageFrame<TPixel> source;

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
            public ImageBrushApplicator(ImageFrame<TPixel> target, ImageFrame<TPixel> image, RectangleF region, GraphicsOptions options)
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
                using (IMemoryOwner<float> amountBuffer = this.Target.MemoryAllocator.Allocate<float>(scanline.Length))
                using (IMemoryOwner<TPixel> overlay = this.Target.MemoryAllocator.Allocate<TPixel>(scanline.Length))
                {
                    Span<float> amountSpan = amountBuffer.GetSpan();
                    Span<TPixel> overlaySpan = overlay.GetSpan();

                    int sourceY = (y - this.offsetY) % this.yLength;
                    int offsetX = x - this.offsetX;
                    Span<TPixel> sourceRow = this.source.GetPixelRowSpan(sourceY);

                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountSpan[i] = scanline[i] * this.Options.BlendPercentage;

                        int sourceX = (i + offsetX) % this.xLength;
                        TPixel pixel = sourceRow[sourceX];
                        overlaySpan[i] = pixel;
                    }

                    Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);
                    this.Blender.Blend(this.source.MemoryAllocator, destinationRow, destinationRow, overlaySpan, amountSpan);
                }
            }
        }
    }
}