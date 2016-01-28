// <copyright file="Laplacian3X3.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public class Laplacian3X3 : EdgeDetectorFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelXY => new float[,]
        {
            { -1, -1, -1 },
            { -1,  8, -1 },
            { -1, -1, -1 }
        };
    }
}
