// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// An interface to queue up image operations to apply to an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public interface IImageProcessingContext<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets a reference to the <see cref="MemoryAllocator" /> used to allocate buffers
        /// for this context.
        /// </summary>
        MemoryAllocator MemoryAllocator { get; }

        /// <summary>
        /// Gets the image dimensions at the current point in the processing pipeline.
        /// </summary>
        /// <returns>The <see cref="Rectangle"/></returns>
        Size GetCurrentSize();

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
        /// <returns>The current image or a new image depending on whether this is allowed to mutate the source image.</returns>
        Image<TPixel> Apply();
    }
}