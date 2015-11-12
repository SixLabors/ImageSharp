// <copyright file="Laplacian5X5.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// The Laplacian 5 x 5 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public class Laplacian5X5 : EdgeDetectorFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelXY => new float[,]
        {
            { -1, -1, -1, -1, -1 },
            { -1, -1, -1, -1, -1 },
            { -1, -1, 24, -1, -1 },
            { -1, -1, -1, -1, -1 },
            { -1, -1, -1, -1, -1 }
        };
    }
}
