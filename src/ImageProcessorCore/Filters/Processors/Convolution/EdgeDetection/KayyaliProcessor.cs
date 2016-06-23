// <copyright file="KayyaliProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    public class KayyaliProcessor : EdgeDetector2DFilter
    {
        /// <inheritdoc/>
        public override float[,] KernelX => new float[,]
        {
            { 6, 0, -6 },
            { 0, 0, 0 },
            { -6, 0, 6 }
        };

        /// <inheritdoc/>
        public override float[,] KernelY => new float[,]
        {
            { -6, 0, 6 },
            { 0, 0, 0 },
            { 6, 0, -6 }
        };
    }
}
