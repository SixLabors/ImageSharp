// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies order dithering using the 8x8 Bayer dithering matrix.
    /// </summary>
    public sealed class BayerDither8x8 : OrderedDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayerDither8x8"/> class.
        /// </summary>
        public BayerDither8x8()
            : base(8)
        {
        }
    }
}