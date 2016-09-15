// <copyright file="EdgeDetection.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Enumerates the various types of defined edge detection filters.
    /// </summary>
    public enum EdgeDetection
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
        /// The Lapacian3X3 operator filter.
        /// </summary>
        Lapacian3X3,

        /// <summary>
        /// The Lapacian5X5 operator filter.
        /// </summary>
        Lapacian5X5,

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
