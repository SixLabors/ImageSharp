// <copyright file="RobertsCross.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    /// <summary>
    /// The Roberts Cross operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Roberts_cross"/>
    /// </summary>
    public class RobertsCross : EdgeDetector2DFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelX => new float[,]
        {
            { 1, 0 },
            { 0, -1 }
        };

        /// <inheritdoc/>
        public override float[,] KernelY => new float[,]
        {
            { 0, 1 },
            { -1, 0 }
        };
    }
}
