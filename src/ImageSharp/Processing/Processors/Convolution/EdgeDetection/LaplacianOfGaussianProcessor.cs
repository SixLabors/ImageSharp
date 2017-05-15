// <copyright file="LaplacianOfGaussianProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// The Laplacian of Gaussian operator filter.
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class LaplacianOfGaussianProcessor<TPixel> : EdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> LaplacianOfGaussianXY =
            new float[,]
            {
                { 0, 0, -1,  0,  0 },
                { 0, -1, -2, -1,  0 },
                { -1, -2, 16, -2, -1 },
                { 0, -1, -2, -1,  0 },
                { 0, 0, -1,  0,  0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="LaplacianOfGaussianProcessor{TPixel}"/> class.
        /// </summary>
        public LaplacianOfGaussianProcessor()
            : base(LaplacianOfGaussianXY)
        {
        }
    }
}