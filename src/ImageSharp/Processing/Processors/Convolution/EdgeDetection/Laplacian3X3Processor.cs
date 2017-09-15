// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The Laplacian 3 x 3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class Laplacian3X3Processor<TPixel> : EdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> Laplacian3X3XY =
            new float[,]
            {
               { -1, -1, -1 },
               { -1,  8, -1 },
               { -1, -1, -1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Laplacian3X3Processor{TPixel}"/> class.
        /// </summary>
        public Laplacian3X3Processor()
            : base(Laplacian3X3XY)
        {
        }
    }
}
