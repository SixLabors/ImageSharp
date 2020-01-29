// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined, position aware, row processing delegate to the image.
    /// </summary>
    internal sealed class PositionAwarePixelRowDelegateProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionAwarePixelRowDelegateProcessor"/> class.
        /// </summary>
        /// <param name="pixelRowOperation">The user defined, position aware, row processing delegate.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PositionAwarePixelRowDelegateProcessor(PixelRowOperation<Point> pixelRowOperation, PixelConversionModifiers modifiers)
        {
            this.PixelRowOperation = pixelRowOperation;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Gets the user defined, position aware, row processing delegate.
        /// </summary>
        public PixelRowOperation<Point> PixelRowOperation { get; }

        /// <summary>
        /// Gets the <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        public PixelConversionModifiers Modifiers { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
            => new PositionAwarePixelRowDelegateProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
