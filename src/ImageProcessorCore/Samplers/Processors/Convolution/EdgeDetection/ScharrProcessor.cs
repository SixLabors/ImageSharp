// <copyright file="ScharrProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Scharr operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator#Alternative_operators"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class ScharrProcessor<TColor, TPacked> : EdgeDetector2DFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        private static readonly float[][] kernelX = new float[3][]
        {
            new float[] { -3, 0, 3 },
            new float[] { -10, 0, 10 },
            new float[] { -3, 0, 3 }
        };

        private static readonly float[][] kernelY = new float[3][]
        {
            new float[] { 3, 10, 3 },
            new float[] { 0, 0, 0 },
            new float[] { -3, -10, -3 }
        };

        /// <inheritdoc/>
        public override float[][] KernelX => kernelX;

        /// <inheritdoc/>
        public override float[][] KernelY => kernelY;
    }
}