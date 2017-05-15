// <copyright file="SierraLite.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the SierraLite image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class SierraLite : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> SierraLiteMatrix =
            new float[,]
            {
               { 0, 0, 2 },
               { 1, 1, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="SierraLite"/> class.
        /// </summary>
        public SierraLite()
            : base(SierraLiteMatrix, 4)
        {
        }
    }
}