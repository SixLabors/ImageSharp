// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies order dithering using the 3x3 dithering matrix.
    /// </summary>
    public sealed class OrderedDither3x3 : OrderedDither
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDither3x3"/> class.
        /// </summary>
        public OrderedDither3x3()
            : base(3)
        {
        }
    }
}