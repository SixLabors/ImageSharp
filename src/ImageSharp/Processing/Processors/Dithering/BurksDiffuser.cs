// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Burks image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class BurksDiffuser : ErrorDiffuser
    {
        private const float Divisor = 32F;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> BurksMatrix =
            new float[,]
            {
                { 0, 0, 0, 8 / Divisor, 4 / Divisor },
                { 2 / Divisor, 4 / Divisor, 8 / Divisor, 4 / Divisor, 2 / Divisor }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="BurksDiffuser"/> class.
        /// </summary>
        public BurksDiffuser()
            : base(BurksMatrix)
        {
        }
    }
}
