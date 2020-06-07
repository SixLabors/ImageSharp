// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A pixel-agnostic interface to queue up image operations to apply to an image.
    /// </summary>
    public interface IImageProcessingContext
    {
        /// <summary>
        /// Gets the configuration which allows altering default behaviour or extending the library.
        /// </summary>
        Configuration Configuration { get; }

        /// <summary>
        /// Gets a set of properties for the Image Processing Context.
        /// </summary>
        /// <remarks>This can be used for storing global settings and defaults to be accessable to processors.</remarks>
        IDictionary<object, object> Properties { get; }

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
