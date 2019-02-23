// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Enumerates the different convolution pass types.
    /// </summary>
    internal enum ConvolutionPassType
    {
        /// <summary>
        /// A single pass.
        /// </summary>
        Single,

        /// <summary>
        /// The first pass of a two pass convolution.
        /// </summary>
        First,

        /// <summary>
        /// The second pass of a two pass convolution.
        /// </summary>
        Second
    }
}
