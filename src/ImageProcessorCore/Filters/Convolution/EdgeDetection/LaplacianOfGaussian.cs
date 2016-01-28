// <copyright file="LaplacianOfGaussian.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    /// <summary>
    /// The Laplacian of Gaussian operator filter.
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>
    /// </summary>
    public class LaplacianOfGaussian : EdgeDetectorFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelXY => new float[,]
        {
            { 0, 0, -1,  0,  0 },
            { 0, -1, -2, -1,  0 },
            { -1, -2, 16, -2, -1 },
            { 0, -1, -2, -1,  0 },
            { 0, 0, -1,  0,  0 }
        };
    }
}
