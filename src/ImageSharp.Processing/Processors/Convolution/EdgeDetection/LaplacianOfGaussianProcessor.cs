// <copyright file="LaplacianOfGaussianProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Laplacian of Gaussian operator filter.
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class LaplacianOfGaussianProcessor<TColor> : EdgeDetectorProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly float[][] LaplacianOfGaussianXY =
        {
           new float[] { 0, 0, -1,  0,  0 },
           new float[] { 0, -1, -2, -1,  0 },
           new float[] { -1, -2, 16, -2, -1 },
           new float[] { 0, -1, -2, -1,  0 },
           new float[] { 0, 0, -1,  0,  0 }
        };

        /// <inheritdoc/>
        public override float[][] KernelXY => LaplacianOfGaussianXY;
    }
}