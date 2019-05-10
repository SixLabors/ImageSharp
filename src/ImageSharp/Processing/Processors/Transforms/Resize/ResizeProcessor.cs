// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines an image resizing operation with the given <see cref="IResampler"/> and dimensional parameters.
    /// </summary>
    public class ResizeProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor"/> class.
        /// </summary>
        /// <param name="sampler">The <see cref="IResampler"/>.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="sourceSize">The size of the source image.</param>
        /// <param name="targetRectangle">The target rectangle to resize into.</param>
        /// <param name="compand">A value indicating whether to apply RGBA companding.</param>
        public ResizeProcessor(IResampler sampler, int width, int height, Size sourceSize, Rectangle targetRectangle, bool compand)
        {
            Guard.NotNull(sampler, nameof(sampler));

            // Ensure size is populated across both dimensions.
            // If only one of the incoming dimensions is 0, it will be modified here to maintain aspect ratio.
            // If it is not possible to keep aspect ratio, make sure at least the minimum is is kept.
            const int min = 1;
            if (width == 0 && height > 0)
            {
                width = (int)MathF.Max(min, MathF.Round(sourceSize.Width * height / (float)sourceSize.Height));
                targetRectangle.Width = width;
            }

            if (height == 0 && width > 0)
            {
                height = (int)MathF.Max(min, MathF.Round(sourceSize.Height * width / (float)sourceSize.Width));
                targetRectangle.Height = height;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Sampler = sampler;
            this.Width = width;
            this.Height = height;
            this.TargetRectangle = targetRectangle;
            this.Compand = compand;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor"/> class.
        /// </summary>
        /// <param name="options">The resize options.</param>
        /// <param name="sourceSize">The source image size.</param>
        public ResizeProcessor(ResizeOptions options, Size sourceSize)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(options.Sampler, nameof(options.Sampler));

            int targetWidth = options.Size.Width;
            int targetHeight = options.Size.Height;

            // Ensure size is populated across both dimensions.
            // These dimensions are used to calculate the final dimensions determined by the mode algorithm.
            // If only one of the incoming dimensions is 0, it will be modified here to maintain aspect ratio.
            // If it is not possible to keep aspect ratio, make sure at least the minimum is is kept.
            const int min = 1;
            if (targetWidth == 0 && targetHeight > 0)
            {
                targetWidth = (int)MathF.Max(min, MathF.Round(sourceSize.Width * targetHeight / (float)sourceSize.Height));
            }

            if (targetHeight == 0 && targetWidth > 0)
            {
                targetHeight = (int)MathF.Max(min, MathF.Round(sourceSize.Height * targetWidth / (float)sourceSize.Width));
            }

            Guard.MustBeGreaterThan(targetWidth, 0, nameof(targetWidth));
            Guard.MustBeGreaterThan(targetHeight, 0, nameof(targetHeight));

            (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(sourceSize, options, targetWidth, targetHeight);

            this.Sampler = options.Sampler;
            this.Width = size.Width;
            this.Height = size.Height;
            this.TargetRectangle = rectangle;
            this.Compand = options.Compand;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="sourceSize">The source image size</param>
        public ResizeProcessor(IResampler sampler, int width, int height, Size sourceSize)
            : this(sampler, width, height, sourceSize, new Rectangle(0, 0, width, height), false)
        {
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the target width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the target height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle TargetRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new ResizeProcessor<TPixel>(this);
        }
    }
}