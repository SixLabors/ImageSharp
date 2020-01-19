// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known ordered dither matrices
    /// </summary>
    public static class KnownDitherers
    {
        /// <summary>
        /// Gets the order ditherer using the 2x2 Bayer dithering matrix
        /// </summary>
        public static IOrderedDither BayerDither2x2 { get; } = new BayerDither2x2();

        /// <summary>
        /// Gets the order ditherer using the 3x3 dithering matrix
        /// </summary>
        public static IOrderedDither OrderedDither3x3 { get; } = new OrderedDither3x3();

        /// <summary>
        /// Gets the order ditherer using the 4x4 Bayer dithering matrix
        /// </summary>
        public static IOrderedDither BayerDither4x4 { get; } = new BayerDither4x4();

        /// <summary>
        /// Gets the order ditherer using the 8x8 Bayer dithering matrix
        /// </summary>
        public static IOrderedDither BayerDither8x8 { get; } = new BayerDither8x8();
    }
}