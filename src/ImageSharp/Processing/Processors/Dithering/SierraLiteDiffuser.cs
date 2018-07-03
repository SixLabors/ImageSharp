// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the SierraLite image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class SierraLiteDiffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> SierraLiteMatrix =
            new float[,]
            {
               { 0, 0, 2 },
               { 1, 1, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="SierraLiteDiffuser"/> class.
        /// </summary>
        public SierraLiteDiffuser()
            : base(SierraLiteMatrix, 4)
        {
        }
    }
}