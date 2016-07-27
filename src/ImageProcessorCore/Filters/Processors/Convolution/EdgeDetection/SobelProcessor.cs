// <copyright file="SobelProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Sobel operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator"/>
    /// </summary>
    public class SobelProcessor<T, TP> : EdgeDetector2DFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public override float[,] KernelX => new float[,]
        {
            { -1, 0, 1 },
            { -2, 0, 2 },
            { -1, 0, 1 }
        };

        /// <inheritdoc/>
        public override float[,] KernelY => new float[,]
        {
            { 1, 2, 1 },
            { 0, 0, 0 },
            { -1, -2, -1 }
        };
    }
}
