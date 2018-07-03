// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a black and white filter matrix to the image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BlackWhiteProcessor<TPixel> : FilterProcessor<TPixel>
          where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlackWhiteProcessor{TPixel}"/> class.
        /// </summary>
        public BlackWhiteProcessor()
            : base(KnownFilterMatrices.BlackWhiteFilter)
        {
        }
    }
}