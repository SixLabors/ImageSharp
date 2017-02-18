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
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> LaplacianOfGaussianXY =
            new Fast2DArray<float>(new float[,]
            {
                { 0, 0, -1,  0,  0 },
                { 0, -1, -2, -1,  0 },
                { -1, -2, 16, -2, -1 },
                { 0, -1, -2, -1,  0 },
                { 0, 0, -1,  0,  0 }
            });

        /// <summary>
        /// Initializes a new instance of the <see cref="LaplacianOfGaussianProcessor{TColor}"/> class.
        /// </summary>
        public LaplacianOfGaussianProcessor()
            : base(LaplacianOfGaussianXY)
        {
        }
    }
}