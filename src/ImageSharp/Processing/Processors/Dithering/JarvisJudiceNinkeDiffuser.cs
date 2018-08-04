// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the JarvisJudiceNinke image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class JarvisJudiceNinkeDiffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly DenseMatrix<float> JarvisJudiceNinkeMatrix =
            new float[,]
            {
                { 0, 0, 0, 7, 5 },
                { 3, 5, 7, 5, 3 },
                { 1, 3, 5, 3, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="JarvisJudiceNinkeDiffuser"/> class.
        /// </summary>
        public JarvisJudiceNinkeDiffuser()
            : base(JarvisJudiceNinkeMatrix, 48)
        {
        }
    }
}