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
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class KayyaliProcessor<TColor, TPacked> : EdgeDetector2DFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
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
