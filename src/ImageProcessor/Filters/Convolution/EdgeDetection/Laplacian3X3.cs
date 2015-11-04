// <copyright file="Laplacian3X3.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public class Laplacian3X3 : ConvolutionFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public override float[,] KernelX => new float[,]
        {
            { -1, -1, -1 },
            { -1,  8, -1 },
            { -1, -1, -1 }
        };
    }
}
