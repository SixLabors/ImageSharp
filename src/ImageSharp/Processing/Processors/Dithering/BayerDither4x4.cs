// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies order dithering using the 4x4 Bayer dithering matrix.
    /// </summary>
    public sealed class BayerDither4x4 : OrderedDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayerDither4x4"/> class.
        /// </summary>
        public BayerDither4x4()
            : base(4)
        {
        }
    }
}