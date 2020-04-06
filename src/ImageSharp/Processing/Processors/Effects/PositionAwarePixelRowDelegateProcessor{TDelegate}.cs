// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined, position aware, row processing delegate to the image.
    /// </summary>
    internal sealed class PositionAwarePixelRowDelegateProcessor<TDelegate> : IImageProcessor
        where TDelegate : struct, IPixelRowDelegate<Point>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionAwarePixelRowDelegateProcessor{TDelegate}"/> class.
        /// </summary>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PositionAwarePixelRowDelegateProcessor(PixelConversionModifiers modifiers)
        {
            this.PixelRowDelegate = default;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionAwarePixelRowDelegateProcessor{TDelegate}"/> class.
        /// </summary>
        /// <param name="pixelRowDelegate">The user defined, position aware, row processing delegate.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PositionAwarePixelRowDelegateProcessor(TDelegate pixelRowDelegate, PixelConversionModifiers modifiers)
        {
            this.PixelRowDelegate = pixelRowDelegate;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Gets the user defined, position aware, row processing delegate.
        /// </summary>
        public TDelegate PixelRowDelegate { get; }

        /// <summary>
        /// Gets the <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.
        /// </summary>
        public PixelConversionModifiers Modifiers { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return new PositionAwarePixelRowDelegateProcessor<TPixel, TDelegate>(
                this.PixelRowDelegate,
                configuration,
                this.Modifiers,
                source,
                sourceRectangle);
        }
    }
}
