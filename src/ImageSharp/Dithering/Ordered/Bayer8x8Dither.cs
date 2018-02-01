// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies order dithering using the 8x8 Bayer dithering matrix.
    /// </summary>
    public sealed class Bayer8x8Dither : BayerDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bayer8x8Dither"/> class.
        /// </summary>
        public Bayer8x8Dither()
            : base(3)
        {
        }
    }
}