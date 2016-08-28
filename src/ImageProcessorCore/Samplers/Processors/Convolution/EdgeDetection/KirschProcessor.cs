// <copyright file="KirschProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class KirschProcessor<TColor, TPacked> : EdgeDetector2DFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override float[,] KernelX => new float[,]
        {
            { 5, 5, 5 },
            { -3, 0, -3 },
            { -3, -3, -3 }
        };

        /// <inheritdoc/>
        public override float[,] KernelY => new float[,]
        {
            { 5, -3, -3 },
            { 5,  0, -3 },
            { 5, -3, -3 }
        };
    }
}
