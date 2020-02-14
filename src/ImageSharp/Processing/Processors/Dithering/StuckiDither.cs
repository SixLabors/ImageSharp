// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Stucki image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class StuckiDither : ErrorDither
    {
        private const float Divisor = 42F;
        private const int Offset = 2;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> StuckiMatrix =
            new float[,]
            {
               { 0, 0, 0, 8 / Divisor, 4 / Divisor },
               { 2 / Divisor, 4 / Divisor, 8 / Divisor, 4 / Divisor, 2 / Divisor },
               { 1 / Divisor, 2 / Divisor, 4 / Divisor, 2 / Divisor, 1 / Divisor }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="StuckiDither"/> class.
        /// </summary>
        public StuckiDither()
            : base(StuckiMatrix, Offset)
        {
        }
    }
}
