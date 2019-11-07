// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Stevenson-Arce image dithering algorithm.
    /// </summary>
    public sealed class StevensonArceDiffuser : ErrorDiffuser
    {
        private const float Divisor = 200F;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> StevensonArceMatrix =
            new float[,]
            {
               { 0,  0,  0,  0,  0, 32 / Divisor,  0 },
               { 12 / Divisor, 0, 26 / Divisor,  0, 30 / Divisor,  0, 16 / Divisor },
               { 0, 12 / Divisor,  0, 26 / Divisor,  0, 12 / Divisor,  0 },
               { 5 / Divisor,  0, 12 / Divisor,  0, 12 / Divisor,  0,  5 / Divisor }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="StevensonArceDiffuser"/> class.
        /// </summary>
        public StevensonArceDiffuser()
            : base(StevensonArceMatrix)
        {
        }
    }
}
