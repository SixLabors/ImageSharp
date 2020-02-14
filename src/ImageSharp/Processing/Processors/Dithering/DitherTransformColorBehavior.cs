// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Enumerates the possible dithering algorithm transform behaviors.
    /// </summary>
    public enum DitherTransformColorBehavior
    {
        /// <summary>
        /// The transformed color should be precalulated and passed to the dithering algorithm.
        /// </summary>
        PreOperation,

        /// <summary>
        /// The transformed color should be calculated as a result of the dithering algorithm.
        /// </summary>
        PostOperation
    }
}
