// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Defines quantization processing for images to reduce the number of colors used in the image palette.
    /// </summary>
    public class QuantizeProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizeProcessor"/> class.
        /// </summary>
        /// <param name="quantizer">The quantizer used to reduce the color palette.</param>
        public QuantizeProcessor(IQuantizer quantizer)
        {
            this.Quantizer = quantizer;
        }

        /// <summary>
        /// Gets the quantizer.
        /// </summary>
        public IQuantizer Quantizer { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new QuantizeProcessor<TPixel>(this.Quantizer);
        }
    }
}