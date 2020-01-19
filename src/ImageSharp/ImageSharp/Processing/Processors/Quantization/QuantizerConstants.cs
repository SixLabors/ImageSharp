// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Contains color quantization specific constants.
    /// </summary>
    internal static class QuantizerConstants
    {
        /// <summary>
        /// The minimum number of colors to use when quantizing an image.
        /// </summary>
        public const int MinColors = 1;

        /// <summary>
        /// The maximum number of colors to use when quantizing an image.
        /// </summary>
        public const int MaxColors = 256;
    }
}