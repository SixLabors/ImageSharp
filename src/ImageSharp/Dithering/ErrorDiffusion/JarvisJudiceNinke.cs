// <copyright file="JarvisJudiceNinke.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the JarvisJudiceNinke image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class JarvisJudiceNinke : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> JarvisJudiceNinkeMatrix =
            new float[,]
            {
                { 0, 0, 0, 7, 5 },
                { 3, 5, 7, 5, 3 },
                { 1, 3, 5, 3, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="JarvisJudiceNinke"/> class.
        /// </summary>
        public JarvisJudiceNinke()
            : base(JarvisJudiceNinkeMatrix, 48)
        {
        }
    }
}