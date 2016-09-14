// <copyright file="LaplacianOfGaussianProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Laplacian of Gaussian operator filter.
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class LaplacianOfGaussianProcessor<TColor, TPacked> : EdgeDetectorFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LaplacianOfGaussianProcessor{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public LaplacianOfGaussianProcessor(bool grayscale)
            : base(Kernel, grayscale)
        {
        }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public static float[,] Kernel => new float[,]
        {
            { 0, 0, -1,  0,  0 },
            { 0, -1, -2, -1,  0 },
            { -1, -2, 16, -2, -1 },
            { 0, -1, -2, -1,  0 },
            { 0, 0, -1,  0,  0 }
        };
    }
}