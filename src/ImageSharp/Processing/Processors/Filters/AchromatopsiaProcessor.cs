// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Achromatopsia (Monochrome) color blindness.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AchromatopsiaProcessor<TPixel> : FilterProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AchromatopsiaProcessor{TPixel}"/> class.
        /// </summary>
        public AchromatopsiaProcessor()
            : base(KnownFilterMatrices.AchromatopsiaFilter)
        {
        }
    }
}
