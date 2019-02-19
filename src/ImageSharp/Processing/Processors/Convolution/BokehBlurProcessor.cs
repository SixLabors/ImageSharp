// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies bokeh blur processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BokehBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The maximum size of the kernel in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="SixLabors.ImageSharp.Processing.Processors.Convolution.BokehBlurProcessor{TPixel}"/> class.
        /// </summary>
        public BokehBlurProcessor()
        {
        }

        /// <summary>
        /// Gets the Radius
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <summary>
        /// Gets the kernel scales to adjust the component values in each kernel
        /// </summary>
        private static IReadOnlyList<double> KernelScales { get; } = new[] { 1.4, 1.2, 1.2, 1.2, 1.2, 1.2 };

        /// <summary>
        /// Gets the available bokeh blur kernel parameters
        /// </summary>
        private static IReadOnlyList<double[,]> KernelParameters { get; } = new[]
        {
            // 1 component
            new[,] { { 0.862325, 1.624835, 0.767583, 1.862321 } },

            // 2 components
            new[,]
            {
                { 0.886528, 5.268909, 0.411259, -0.548794 },
                { 1.960518, 1.558213, 0.513282, 4.56111 }
            },

            // 3 components
            new[,]
            {
                { 2.17649, 5.043495, 1.621035, -2.105439 },
                { 1.019306, 9.027613, -0.28086, -0.162882 },
                { 2.81511, 1.597273, -0.366471, 10.300301 }
            },

            // 4 components
            new[,]
            {
                { 4.338459, 1.553635, -5.767909, 46.164397 },
                { 3.839993, 4.693183, 9.795391, -15.227561 },
                { 2.791880, 8.178137, -3.048324, 0.302959 },
                { 1.342190, 12.328289, 0.010001, 0.244650 }
            },

            // 5 components
            new[,]
            {
                { 4.892608, 1.685979, -22.356787, 85.91246 },
                { 4.71187, 4.998496, 35.918936, -28.875618 },
                { 4.052795, 8.244168, -13.212253, -1.578428 },
                { 2.929212, 11.900859, 0.507991, 1.816328 },
                { 1.512961, 16.116382, 0.138051, -0.01 }
            },

            // 6 components
            new[,]
            {
                { 5.143778, 2.079813, -82.326596, 111.231024 },
                { 5.612426, 6.153387, 113.878661, 58.004879 },
                { 5.982921, 9.802895, 39.479083, -162.028887 },
                { 6.505167, 11.059237, -71.286026, 95.027069 },
                { 3.869579, 14.81052, 1.405746, -3.704914 },
                { 2.201904, 19.032909, -0.152784, -0.107988 }
            }
        };

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration) => throw new NotImplementedException();
    }
}
