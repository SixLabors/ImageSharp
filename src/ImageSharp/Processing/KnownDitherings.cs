// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known dithering algorithms.
    /// </summary>
    public static class KnownDitherings
    {
        /// <summary>
        /// Gets the order ditherer using the 2x2 Bayer dithering matrix
        /// </summary>
        public static IDither Bayer2x2 { get; } = OrderedDither.Bayer2x2;

        /// <summary>
        /// Gets the order ditherer using the 3x3 dithering matrix
        /// </summary>
        public static IDither Ordered3x3 { get; } = OrderedDither.Ordered3x3;

        /// <summary>
        /// Gets the order ditherer using the 4x4 Bayer dithering matrix
        /// </summary>
        public static IDither Bayer4x4 { get; } = OrderedDither.Bayer4x4;

        /// <summary>
        /// Gets the order ditherer using the 8x8 Bayer dithering matrix
        /// </summary>
        public static IDither Bayer8x8 { get; } = OrderedDither.Bayer8x8;

        /// <summary>
        /// Gets the error Dither that implements the Atkinson algorithm.
        /// </summary>
        public static IDither Atkinson { get; } = ErrorDither.Atkinson;

        /// <summary>
        /// Gets the error Dither that implements the Burks algorithm.
        /// </summary>
        public static IDither Burks { get; } = ErrorDither.Burkes;

        /// <summary>
        /// Gets the error Dither that implements the Floyd-Steinberg algorithm.
        /// </summary>
        public static IDither FloydSteinberg { get; } = ErrorDither.FloydSteinberg;

        /// <summary>
        /// Gets the error Dither that implements the Jarvis-Judice-Ninke algorithm.
        /// </summary>
        public static IDither JarvisJudiceNinke { get; } = ErrorDither.JarvisJudiceNinke;

        /// <summary>
        /// Gets the error Dither that implements the Sierra-2 algorithm.
        /// </summary>
        public static IDither Sierra2 { get; } = ErrorDither.Sierra2;

        /// <summary>
        /// Gets the error Dither that implements the Sierra-3 algorithm.
        /// </summary>
        public static IDither Sierra3 { get; } = ErrorDither.Sierra3;

        /// <summary>
        /// Gets the error Dither that implements the Sierra-Lite algorithm.
        /// </summary>
        public static IDither SierraLite { get; } = ErrorDither.SierraLite;

        /// <summary>
        /// Gets the error Dither that implements the Stevenson-Arce algorithm.
        /// </summary>
        public static IDither StevensonArce { get; } = ErrorDither.StevensonArce;

        /// <summary>
        /// Gets the error Dither that implements the Stucki algorithm.
        /// </summary>
        public static IDither Stucki { get; } = ErrorDither.Stucki;
    }
}
