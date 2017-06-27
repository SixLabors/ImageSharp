// <copyright file="DrawImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DrawImageProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly PixelBlender<TPixel> blender;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="options">The opacity of the image to blend. Between 0 and 100.</param>
        public DrawImageProcessor(Image<TPixel> image, Size size, Point location, GraphicsOptions options)
        {
            Guard.MustBeBetweenOrEqualTo(options.BlendPercentage, 0, 1, nameof(options.BlendPercentage));
            this.Image = image;
            this.Size = size;
            this.Alpha = options.BlendPercentage;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(options.BlenderMode);
            this.Location = location;
        }

        /// <summary>
        /// Gets the image to blend.
        /// </summary>
        public Image<TPixel> Image { get; private set; }

        /// <summary>
        /// Gets the alpha percentage value.
        /// </summary>
        public float Alpha { get; }

        /// <summary>
        /// Gets the size to draw the blended image.
        /// </summary>
        public Size Size { get; }

        /// <summary>
        /// Gets the location to draw the blended image.
        /// </summary>
        public Point Location { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            Image<TPixel> disposableImage = null;
            Image<TPixel> targetImage = this.Image;

            try
            {
                if (targetImage.Bounds.Size != this.Size)
                {
                    targetImage = disposableImage = new Image<TPixel>(this.Image).Resize(this.Size.Width, this.Size.Height);
                }

                // Align start/end positions.
                Rectangle bounds = this.Image.Bounds;
                int minX = Math.Max(this.Location.X, sourceRectangle.X);
                int maxX = Math.Min(this.Location.X + bounds.Width, sourceRectangle.Width);
                maxX = Math.Min(this.Location.X + this.Size.Width, maxX);

                int minY = Math.Max(this.Location.Y, sourceRectangle.Y);
                int maxY = Math.Min(this.Location.Y + bounds.Height, sourceRectangle.Bottom);

                maxY = Math.Min(this.Location.Y + this.Size.Height, maxY);

                int width = maxX - minX;
                using (Buffer<float> amount = new Buffer<float>(width))
                using (PixelAccessor<TPixel> toBlendPixels = targetImage.Lock())
                using (PixelAccessor<TPixel> sourcePixels = source.Lock())
                {
                    for (int i = 0; i < width; i++)
                    {
                        amount[i] = this.Alpha;
                    }

                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                            {
                                Span<TPixel> background = sourcePixels.GetRowSpan(y).Slice(minX, width);
                                Span<TPixel> foreground = toBlendPixels.GetRowSpan(y - this.Location.Y).Slice(0, width);
                                this.blender.Blend(background, background, foreground, amount);
                            });
                }
            }
            finally
            {
                disposableImage?.Dispose();
            }
        }
    }
}