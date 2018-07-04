// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a filter matrix recreating an old Kodachrome camera effect matrix to the image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class KodachromeProcessor<TPixel> : FilterProcessor<TPixel>
          where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KodachromeProcessor{TPixel}"/> class.
        /// </summary>
        public KodachromeProcessor()
            : base(KnownFilterMatrices.KodachromeFilter)
        {
        }
    }
}