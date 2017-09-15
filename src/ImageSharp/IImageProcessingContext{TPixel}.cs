// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// An interface to queue up image operations to apply to an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public interface IImageProcessingContext<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Adds the processor to the current set of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply</param>
        /// <param name="rectangle">The area to apply it to</param>
        /// <returns>The current operations class to allow chaining of operations.</returns>
        IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle);

        /// <summary>
        /// Adds the processor to the current set of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply</param>
        /// <returns>The current operations class to allow chaining of operations.</returns>
        IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor);
    }

    /// <summary>
    /// An internal interface to queue up image operations and have a method to apply them to and return a result
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal interface IInternalImageProcessingContext<TPixel> : IImageProcessingContext<TPixel>
       where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Adds the processors to the current image
        /// </summary>
        /// <returns>The current image or a new image depending on withere this is alloed to mutate the source image.</returns>
        Image<TPixel> Apply();
    }
}