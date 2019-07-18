// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TPixelBg">The pixel format of destination image.</typeparam>
    /// <typeparam name="TPixelFg">The pixel format of source image.</typeparam>
    internal class DrawImageProcessor<TPixelBg, TPixelFg> : ImageProcessor<TPixelBg>
        where TPixelBg : struct, IPixel<TPixelBg>
        where TPixelFg : struct, IPixel<TPixelFg>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor{TPixelDst, TPixelSrc}"/> class.
        /// </summary>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
        /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        public DrawImageProcessor(
            Image<TPixelFg> image,
            Point location,
            PixelColorBlendingMode colorBlendingMode,
            PixelAlphaCompositionMode alphaCompositionMode,
            float opacity)
        {
            Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

            this.Image = image;
            this.Opacity = opacity;
            this.Blender = PixelOperations<TPixelBg>.Instance.GetPixelBlender(colorBlendingMode, alphaCompositionMode);
            this.Location = location;
        }

        /// <summary>
        /// Gets the image to blend
        /// </summary>
        public Image<TPixelFg> Image { get; }

        /// <summary>
        /// Gets the opacity of the image to blend
        /// </summary>
        public float Opacity { get; }

        /// <summary>
        /// Gets the pixel blender
        /// </summary>
        public PixelBlender<TPixelBg> Blender { get; }

        /// <summary>
        /// Gets the location to draw the blended image
        /// </summary>
        public Point Location { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixelBg> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            Image<TPixelFg> targetImage = this.Image;
            PixelBlender<TPixelBg> blender = this.Blender;
            int locationY = this.Location.Y;

            // Align start/end positions.
            Rectangle bounds = targetImage.Bounds();

            int minX = Math.Max(this.Location.X, sourceRectangle.X);
            int maxX = Math.Min(this.Location.X + bounds.Width, sourceRectangle.Right);
            int targetX = minX - this.Location.X;

            int minY = Math.Max(this.Location.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.Location.Y + bounds.Height, sourceRectangle.Bottom);

            int width = maxX - minX;

            var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

            // not a valid operation because rectangle does not overlap with this image.
            if (workingRect.Width <= 0 || workingRect.Height <= 0)
            {
                throw new ImageProcessingException(
                    "Cannot draw image because the source image does not overlap the target image.");
            }

            ParallelHelper.IterateRows(
                workingRect,
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixelBg> background = source.GetPixelRowSpan(y).Slice(minX, width);
                            Span<TPixelFg> foreground =
                                targetImage.GetPixelRowSpan(y - locationY).Slice(targetX, width);
                            blender.Blend<TPixelFg>(configuration, background, background, foreground, this.Opacity);
                        }
                    });
        }
    }
}