// <copyright file="Sobel.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// The Sobel operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator"/>
    /// </summary>
    public class Sobel : Convolution2DFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public override float[,] KernelX => new float[,]
        {
            { -1, 0, 1 },
            { -2, 0, 2 },
            { -1, 0, 1 }
        };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public override float[,] KernelY => new float[,]
        {
            { 1, 2, 1 },
            { 0, 0, 0 },
            { -1, -2, -1 }
        };
    }
}
