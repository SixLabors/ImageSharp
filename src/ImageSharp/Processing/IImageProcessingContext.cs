// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A pixel-agnostic interface to queue up image operations to apply to an image.
    /// </summary>
    public interface IImageProcessingContext
    {
        /// <summary>
        /// Gets a reference to the <see cref="MemoryAllocator" /> used to allocate buffers
        /// for this context.
        /// </summary>
        MemoryAllocator MemoryAllocator { get; }

        /// <summary>
        /// Gets the image dimensions at the current point in the processing pipeline.
        /// </summary>
        /// <returns>The <see cref="Rectangle"/>.</returns>
        Size GetCurrentSize();

        /// <summary>
        /// Adds the processor to the current set of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply.</param>
        /// <param name="rectangle">The area to apply it to.</param>
        /// <returns>The current operations class to allow chaining of operations.</returns>
        IImageProcessingContext ApplyProcessor(IImageProcessor processor, Rectangle rectangle);

        /// <summary>
        /// Adds the processor to the current set of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply.</param>
        /// <returns>The current operations class to allow chaining of operations.</returns>
        IImageProcessingContext ApplyProcessor(IImageProcessor processor);
    }
}