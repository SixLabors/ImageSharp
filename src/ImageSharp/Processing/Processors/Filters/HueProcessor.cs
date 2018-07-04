// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a hue filter matrix using the given angle of rotation in degrees
    /// </summary>
    internal class HueProcessor<TPixel> : FilterProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HueProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees</param>
        public HueProcessor(float degrees)
            : base(KnownFilterMatrices.CreateHueFilter(degrees))
        {
            this.Degrees = degrees;
        }

        /// <summary>
        /// Gets the angle of rotation in degrees
        /// </summary>
        public float Degrees { get; }
    }
}