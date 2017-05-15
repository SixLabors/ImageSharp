// <copyright file="FloydSteinberg.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the Floyd–Steinberg image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class FloydSteinberg : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> FloydSteinbergMatrix =
            new float[,]
            {
                { 0, 0, 7 },
                { 3, 5, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="FloydSteinberg"/> class.
        /// </summary>
        public FloydSteinberg()
            : base(FloydSteinbergMatrix, 16)
        {
        }
    }
}