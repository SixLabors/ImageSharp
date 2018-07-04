// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Enumerates the various types of defined edge detection filters.
    /// </summary>
    public enum EdgeDetectionOperators
    {
        /// <summary>
        /// The Kayyali operator filter.
        /// </summary>
        Kayyali,

        /// <summary>
        /// The Kirsch operator filter.
        /// </summary>
        Kirsch,

        /// <summary>
        /// The Laplacian3X3 operator filter.
        /// </summary>
        Laplacian3x3,

        /// <summary>
        /// The Laplacian5X5 operator filter.
        /// </summary>
        Laplacian5x5,

        /// <summary>
        /// The LaplacianOfGaussian operator filter.
        /// </summary>
        LaplacianOfGaussian,

        /// <summary>
        /// The Prewitt operator filter.
        /// </summary>
        Prewitt,

        /// <summary>
        /// The RobertsCross operator filter.
        /// </summary>
        RobertsCross,

        /// <summary>
        /// The Robinson operator filter.
        /// </summary>
        Robinson,

        /// <summary>
        /// The Scharr operator filter.
        /// </summary>
        Scharr,

        /// <summary>
        /// The Sobel operator filter.
        /// </summary>
        Sobel
    }
}