// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Atkinson image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class AtkinsonDiffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> AtkinsonMatrix =
            new float[,]
            {
               { 0, 0, 1, 1 },
               { 1, 1, 1, 0 },
               { 0, 1, 0, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="AtkinsonDiffuser"/> class.
        /// </summary>
        public AtkinsonDiffuser()
            : base(AtkinsonMatrix, 8)
        {
        }
    }
}
