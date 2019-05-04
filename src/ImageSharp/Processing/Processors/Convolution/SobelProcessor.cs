// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using the Sobel operator filter.
    /// See <see href="http://en.wikipedia.org/wiki/Sobel_operator"/>.
    /// </summary>
    public class SobelProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SobelProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public SobelProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetector2DProcessor<TPixel>(SobelKernels.SobelX, SobelKernels.SobelY, this.Grayscale);
        }

        // TODO: Move this to an appropriate extension method if possible.
        internal void ApplyToFrame<TPixel>(ImageFrame<TPixel> frame, Rectangle sourceRectangle, Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            var processorImpl = new EdgeDetector2DProcessor<TPixel>(SobelKernels.SobelX, SobelKernels.SobelY, this.Grayscale);
            processorImpl.Apply(frame, sourceRectangle, configuration);
        }
    }
}