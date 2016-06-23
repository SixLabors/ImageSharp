// <copyright file="PrewittProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Prewitt operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Prewitt_operator"/>
    /// </summary>
    public class PrewittProcessor : EdgeDetector2DFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelX => new float[,]
        {
            { -1, 0, 1 },
            { -1, 0, 1 },
            { -1, 0, 1 }
        };

        /// <inheritdoc/>
        public override float[,] KernelY => new float[,]
        {
            { 1, 1, 1 },
            { 0, 0, 0 },
            { -1, -1, -1 }
        };
    }
}
