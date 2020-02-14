// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Sierra2 image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Sierra2Dither : ErrorDither
    {
        private const float Divisor = 16F;
        private const int Offset = 2;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> Sierra2Matrix =
            new float[,]
            {
               { 0, 0, 0, 4 / Divisor, 3 / Divisor },
               { 1 / Divisor, 2 / Divisor, 3 / Divisor, 2 / Divisor, 1 / Divisor }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Sierra2Dither"/> class.
        /// </summary>
        public Sierra2Dither()
            : base(Sierra2Matrix, Offset)
        {
        }
    }
}
