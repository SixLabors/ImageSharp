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
        /// Gets the error diffuser that implements the Floyd-Steinberg algorithm.
        /// </summary>
        public static IErrorDiffuser FloydSteinberg { get; } = new FloydSteinbergDiffuser();

        /// <summary>
        /// Gets the error diffuser that implements the Jarvis-Judice-Ninke algorithm.
        /// </summary>
        public static IErrorDiffuser JarvisJudiceNinke { get; } = new JarvisJudiceNinkeDiffuser();
    }
}