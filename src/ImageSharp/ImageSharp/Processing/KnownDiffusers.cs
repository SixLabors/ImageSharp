// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known error diffusion algorithms
    /// </summary>
    public static class KnownDiffusers
    {
        /// <summary>
        /// Gets the error diffuser that implements the Atkinson algorithm.
        /// </summary>
        public static IErrorDiffuser Atkinson { get; } = new AtkinsonDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Burks algorithm.
        /// </summary>
        public static IErrorDiffuser Burks { get; } = new BurksDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Floyd-Steinberg algorithm.
        /// </summary>
        public static IErrorDiffuser FloydSteinberg { get; } = new FloydSteinbergDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Jarvis-Judice-Ninke algorithm.
        /// </summary>
        public static IErrorDiffuser JarvisJudiceNinke { get; } = new JarvisJudiceNinkeDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Sierra-2 algorithm.
        /// </summary>
        public static IErrorDiffuser Sierra2 { get; } = new Sierra2Diffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Sierra-3 algorithm.
        /// </summary>
        public static IErrorDiffuser Sierra3 { get; } = new Sierra3Diffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Sierra-Lite algorithm.
        /// </summary>
        public static IErrorDiffuser SierraLite { get; } = new SierraLiteDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Stevenson-Arce algorithm.
        /// </summary>
        public static IErrorDiffuser StevensonArce { get; } = new StevensonArceDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Stucki algorithm.
        /// </summary>
        public static IErrorDiffuser Stucki { get; } = new StuckiDiffuser();
    }
}