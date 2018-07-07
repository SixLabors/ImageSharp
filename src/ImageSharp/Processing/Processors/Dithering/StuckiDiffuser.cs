// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Stucki image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class StuckiDiffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> StuckiMatrix =
            new float[,]
            {
               { 0, 0, 0, 8, 4 },
               { 2, 4, 8, 4, 2 },
               { 1, 2, 4, 2, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="StuckiDiffuser"/> class.
        /// </summary>
        public StuckiDiffuser()
            : base(StuckiMatrix, 42)
        {
        }
    }
}