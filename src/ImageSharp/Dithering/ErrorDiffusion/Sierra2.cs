// <copyright file="Sierra2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the Sierra2 image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Sierra2 : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> Sierra2Matrix =
            new float[,]
            {
               { 0, 0, 0, 4, 3 },
               { 1, 2, 3, 2, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Sierra2"/> class.
        /// </summary>
        public Sierra2()
            : base(Sierra2Matrix, 16)
        {
        }
    }
}