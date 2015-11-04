// <copyright file="Kirsch.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    public class Kirsch : Convolution2DFilter
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public override float[,] KernelX => new float[,]
        {
            { 5, 5, 5 },
            { -3, 0, -3 },
            { -3, -3, -3 }
        };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public override float[,] KernelY => new float[,]
        {
            { 5, -3, -3 },
            { 5,  0, -3 },
            { 5, -3, -3 }
        };
    }
}
