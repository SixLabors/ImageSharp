// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Floyd–Steinberg image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class FloydSteinbergDiffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> FloydSteinbergMatrix =
            new float[,]
            {
                { 0, 0, 7 },
                { 3, 5, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="FloydSteinbergDiffuser"/> class.
        /// </summary>
        public FloydSteinbergDiffuser()
            : base(FloydSteinbergMatrix, 16)
        {
        }
    }
}