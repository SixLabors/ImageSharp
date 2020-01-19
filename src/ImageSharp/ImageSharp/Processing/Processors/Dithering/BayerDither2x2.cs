// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies order dithering using the 2x2 Bayer dithering matrix.
    /// </summary>
    public sealed class BayerDither2x2 : OrderedDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayerDither2x2"/> class.
        /// </summary>
        public BayerDither2x2()
            : base(2)
        {
        }
    }
}