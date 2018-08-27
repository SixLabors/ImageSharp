// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DrawImageProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
        /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        public DrawImageProcessor(Image<TPixel> image, Point location, PixelColorBlendingMode colorBlendingMode, PixelAlphaCompositionMode alphaCompositionMode, float opacity)
        {
            Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

            this.Image = image;
            this.Opacity = opacity;
            this.Blender = PixelOperations<TPixel>.Instance.GetPixelBlender(colorBlendingMode, alphaCompositionMode);
            this.Location = location;
        }

        /// <summary>
        /// Gets the image to blend
        /// </summary>
        public Image<TPixel> Image { get; }

        /// <summary>
        /// Gets the opacity of the image to blend
        /// </summary>
        public float Opacity { get; }

        /// <summary>
        /// Gets the pixel blender
        /// </summary>
        public PixelBlender<TPixel> Blender { get; }

        /// <summary>
        /// Gets the location to draw the blended image
        /// </summary>
        public Point Location { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            Image<TPixel> targetImage = this.Image;
            PixelBlender<TPixel> blender = this.Blender;
            int locationY = this.Location.Y;

            // Align start/end positions.
            Rectangle bounds = targetImage.Bounds();

            int minX = Math.Max(this.Location.X, sourceRectangle.X);
            int maxX = Math.Min(this.Location.X + bounds.Width, sourceRectangle.Width);
            int targetX = minX - this.Location.X;

            int minY = Math.Max(this.Location.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.Location.Y + bounds.Height, sourceRectangle.Bottom);

            int width = maxX - minX;

            MemoryAllocator memoryAllocator = this.Image.GetConfiguration().MemoryAllocator;

            using (IMemoryOwner<float> amount = memoryAllocator.Allocate<float>(width))
            {
                amount.GetSpan().Fill(this.Opacity);

                ParallelFor.WithConfiguration(
                    minY,
                    maxY,
                    configuration,
                    y =>
                        {
                            Span<TPixel> background = source.GetPixelRowSpan(y).Slice(minX, width);
                            Span<TPixel> foreground = targetImage.GetPixelRowSpan(y - locationY).Slice(targetX, width);
                            blender.Blend(memoryAllocator, background, background, foreground, amount.GetSpan());
                        });
            }
        }
    }
}