// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known quantizing algorithms
    /// </summary>
    public static class KnownQuantizers
    {
        /// <summary>
        /// Gets the Xiaolin Wu's Color Quantizer which generates high quality output.
        /// </summary>
        public static IQuantizer Wu { get; } = new WuQuantizer();
    }
}