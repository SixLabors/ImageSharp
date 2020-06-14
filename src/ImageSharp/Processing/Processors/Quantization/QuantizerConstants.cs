// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Contains color quantization specific constants.
    /// </summary>
    public static class QuantizerConstants
    {
        /// <summary>
        /// The minimum number of colors to use when quantizing an image.
        /// </summary>
        public const int MinColors = 1;

        /// <summary>
        /// The maximum number of colors to use when quantizing an image.
        /// </summary>
        public const int MaxColors = 256;

        /// <summary>
        /// The minumim dithering scale used to adjust the amount of dither.
        /// </summary>
        public const float MinDitherScale = 0;

        /// <summary>
        /// The max dithering scale used to adjust the amount of dither.
        /// </summary>
        public const float MaxDitherScale = 1F;

        /// <summary>
        /// Gets the default dithering algorithm to use.
        /// </summary>
        public static IDither DefaultDither { get; } = KnownDitherings.FloydSteinberg;
    }
}
