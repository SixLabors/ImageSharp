// <copyright file="Laplacian3X3Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class Laplacian3X3Processor<TColor, TPacked> : EdgeDetectorFilter<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly float[][] Laplacian3X3XY = new float[][]
        {
           new float[] { -1, -1, -1 },
           new float[] { -1,  8, -1 },
           new float[] { -1, -1, -1 }
        };

        /// <inheritdoc/>
        public override float[][] KernelXY => Laplacian3X3XY;
    }
}
