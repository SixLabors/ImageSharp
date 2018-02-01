// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies order dithering using the 4x4 Bayer dithering matrix.
    /// </summary>
    public sealed class Bayer4x4Dither : BayerDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bayer4x4Dither"/> class.
        /// </summary>
        public Bayer4x4Dither()
            : base(2)
        {
        }
    }
}