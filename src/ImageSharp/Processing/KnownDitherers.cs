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
        public static IDither BayerDither2x2 { get; } = new BayerDither2x2();

        /// <summary>
        /// Gets the order ditherer using the 3x3 dithering matrix
        /// </summary>
        public static IDither OrderedDither3x3 { get; } = new OrderedDither3x3();

        /// <summary>
        /// Gets the order ditherer using the 4x4 Bayer dithering matrix
        /// </summary>
        public static IDither BayerDither4x4 { get; } = new BayerDither4x4();

        /// <summary>
        /// Gets the order ditherer using the 8x8 Bayer dithering matrix
        /// </summary>
        public static IDither BayerDither8x8 { get; } = new BayerDither8x8();

        /// <summary>
        /// Gets the error Dither that implements the Atkinson algorithm.
        /// </summary>
        public static IDither Atkinson { get; } = new AtkinsonDither();

        /// <summary>
        /// Gets the error Dither that implements the Burks algorithm.
        /// </summary>
        public static IDither Burks { get; } = new BurksDither();

        /// <summary>
        /// Gets the error Dither that implements the Floyd-Steinberg algorithm.
        /// </summary>
        public static IDither FloydSteinberg { get; } = new FloydSteinbergDither();

        /// <summary>
        /// Gets the error Dither that implements the Jarvis-Judice-Ninke algorithm.
        /// </summary>
        public static IDither JarvisJudiceNinke { get; } = new JarvisJudiceNinkeDither();

        /// <summary>
        /// Gets the error Dither that implements the Sierra-2 algorithm.
        /// </summary>
        public static IDither Sierra2 { get; } = new Sierra2Dither();

        /// <summary>
        /// Gets the error Dither that implements the Sierra-3 algorithm.
        /// </summary>
        public static IDither Sierra3 { get; } = new Sierra3Dither();

        /// <summary>
        /// Gets the error Dither that implements the Sierra-Lite algorithm.
        /// </summary>
        public static IDither SierraLite { get; } = new SierraLiteDither();

        /// <summary>
        /// Gets the error Dither that implements the Stevenson-Arce algorithm.
        /// </summary>
        public static IDither StevensonArce { get; } = new StevensonArceDither();

        /// <summary>
        /// Gets the error Dither that implements the Stucki algorithm.
        /// </summary>
        public static IDither Stucki { get; } = new StuckiDither();
    }
}
