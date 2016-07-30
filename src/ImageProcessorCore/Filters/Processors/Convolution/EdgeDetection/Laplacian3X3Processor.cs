// <copyright file="Laplacian3X3Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class Laplacian3X3Processor<T, TP> : EdgeDetectorFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
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
