// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies order dithering using the 2x2 Bayer dithering matrix.
    /// </summary>
    public sealed class Bayer2x2Dither : BayerDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bayer2x2Dither"/> class.
        /// </summary>
        public Bayer2x2Dither()
            : base(1)
        {
        }
    }
}