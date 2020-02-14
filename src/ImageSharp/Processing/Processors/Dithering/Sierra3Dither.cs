// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Sierra3 image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Sierra3Dither : ErrorDither
    {
        private const float Divisor = 32F;
        private const int Offset = 2;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> Sierra3Matrix =
            new float[,]
            {
               { 0, 0, 0, 5 / Divisor, 3 / Divisor },
               { 2 / Divisor, 4 / Divisor, 5 / Divisor, 4 / Divisor, 2 / Divisor },
               { 0, 2 / Divisor, 3 / Divisor, 2 / Divisor, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Sierra3Dither"/> class.
        /// </summary>
        public Sierra3Dither()
            : base(Sierra3Matrix, Offset)
        {
        }
    }
}
