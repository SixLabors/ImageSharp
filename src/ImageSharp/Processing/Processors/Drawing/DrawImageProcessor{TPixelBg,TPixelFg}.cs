// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TPixelBg">The pixel format of destination image.</typeparam>
    /// <typeparam name="TPixelFg">The pixel format of source image.</typeparam>
    internal class DrawImageProcessor<TPixelBg, TPixelFg> : ImageProcessor<TPixelBg>
        where TPixelBg : unmanaged, IPixel<TPixelBg>
        where TPixelFg : unmanaged, IPixel<TPixelFg>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor{TPixelBg, TPixelFg}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="image">The foreground <see cref="Image{TPixelFg}"/> to blend with the currently processing image.</param>
        /// <param name="source">The source <see cref="Image{TPixelBg}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
        /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        public DrawImageProcessor(
            Configuration configuration,
            Image<TPixelFg> image,
            Image<TPixelBg> source,
            Rectangle sourceRectangle,
            Point location,
            PixelColorBlendingMode colorBlendingMode,
            PixelAlphaCompositionMode alphaCompositionMode,
            float opacity)
            : base(configuration, source, sourceRectangle)
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
        protected override void OnFrameApply(ImageFrame<TPixelBg> source)
        {
            Rectangle sourceRectangle = this.SourceRectangle;
            Configuration configuration = this.Configuration;

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

            // Not a valid operation because rectangle does not overlap with this image.
            if (workingRect.Width <= 0 || workingRect.Height <= 0)
            {
                throw new ImageProcessingException(
                    "Cannot draw image because the source image does not overlap the target image.");
            }

            var operation = new RowOperation(source, targetImage, blender, configuration, minX, width, locationY, targetX, this.Opacity);
            ParallelRowIterator.IterateRows(
                configuration,
                workingRect,
                in operation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the draw logic for <see cref="DrawImageProcessor{TPixelBg,TPixelFg}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation
        {
            private readonly ImageFrame<TPixelBg> sourceFrame;
            private readonly Image<TPixelFg> targetImage;
            private readonly PixelBlender<TPixelBg> blender;
            private readonly Configuration configuration;
            private readonly int minX;
            private readonly int width;
            private readonly int locationY;
            private readonly int targetX;
            private readonly float opacity;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                ImageFrame<TPixelBg> sourceFrame,
                Image<TPixelFg> targetImage,
                PixelBlender<TPixelBg> blender,
                Configuration configuration,
                int minX,
                int width,
                int locationY,
                int targetX,
                float opacity)
            {
                this.sourceFrame = sourceFrame;
                this.targetImage = targetImage;
                this.blender = blender;
                this.configuration = configuration;
                this.minX = minX;
                this.width = width;
                this.locationY = locationY;
                this.targetX = targetX;
                this.opacity = opacity;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Span<TPixelBg> background = this.sourceFrame.GetPixelRowSpan(y).Slice(this.minX, this.width);
                Span<TPixelFg> foreground = this.targetImage.GetPixelRowSpan(y - this.locationY).Slice(this.targetX, this.width);
                this.blender.Blend<TPixelFg>(this.configuration, background, background, foreground, this.opacity);
            }
        }
    }
}
