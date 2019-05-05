// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a crop operation on an image.
    /// </summary>
    public sealed class CropProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor"/> class.
        /// </summary>
        /// <param name="cropRectangle">The target cropped rectangle.</param>
        /// <param name="sourceSize">The source image size.</param>
        public CropProcessor(Rectangle cropRectangle, Size sourceSize)
        {
            // Check bounds here and throw if we are passed a rectangle exceeding our source bounds.
            Guard.IsTrue(
                new Rectangle(Point.Empty, sourceSize).Contains(cropRectangle),
                nameof(cropRectangle),
                "Crop rectangle should be smaller than the source bounds.");
            this.CropRectangle = cropRectangle;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public Rectangle CropRectangle { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new CropProcessor<TPixel>(this);
        }
    }
}