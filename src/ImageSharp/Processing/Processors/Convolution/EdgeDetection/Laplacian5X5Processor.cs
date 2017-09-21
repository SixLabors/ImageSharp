// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The Laplacian 5 x 5 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class Laplacian5X5Processor<TPixel> : EdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> Laplacian5X5XY =
            new float[,]
            {
                { -1, -1, -1, -1, -1 },
                { -1, -1, -1, -1, -1 },
                { -1, -1, 24, -1, -1 },
                { -1, -1, -1, -1, -1 },
                { -1, -1, -1, -1, -1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Laplacian5X5Processor{TPixel}"/> class.
        /// </summary>
        public Laplacian5X5Processor()
            : base(Laplacian5X5XY)
        {
        }
    }
}