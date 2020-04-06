// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined row processing delegate to the image.
    /// </summary>
    internal sealed class PixelRowDelegateProcessor<TDelegate> : IImageProcessor
        where TDelegate : struct, IPixelRowDelegate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor{TDelegate}"/> class.
        /// </summary>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PixelRowDelegateProcessor(PixelConversionModifiers modifiers)
        {
            this.PixelRowDelegate = default;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor{TDelegate}"/> class.
        /// </summary>
        /// <param name="pixelRowDelegate">The user defined, row processing delegate.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the pixel conversions.</param>
        public PixelRowDelegateProcessor(TDelegate pixelRowDelegate, PixelConversionModifiers modifiers)
        {
            this.PixelRowDelegate = pixelRowDelegate;
            this.Modifiers = modifiers;
        }

        /// <summary>
        /// Gets the user defined row processing value delegate to the image.
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
            return new PixelRowDelegateProcessor<TPixel, TDelegate>(
                this.PixelRowDelegate,
                configuration,
                this.Modifiers,
                source,
                sourceRectangle);
        }
    }
}
