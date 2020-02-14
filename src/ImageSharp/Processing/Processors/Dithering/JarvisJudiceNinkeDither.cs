// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the JarvisJudiceNinke image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class JarvisJudiceNinkeDither : ErrorDither
    {
        private const float Divisor = 48F;
        private const int Offset = 2;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> JarvisJudiceNinkeMatrix =
            new float[,]
            {
                { 0, 0, 0, 7 / Divisor, 5 / Divisor },
                { 3 / Divisor, 5 / Divisor, 7 / Divisor, 5 / Divisor, 3 / Divisor },
                { 1 / Divisor, 3 / Divisor, 5 / Divisor, 3 / Divisor, 1 / Divisor }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="JarvisJudiceNinkeDither"/> class.
        /// </summary>
        public JarvisJudiceNinkeDither()
            : base(JarvisJudiceNinkeMatrix, Offset)
        {
        }
    }
}
