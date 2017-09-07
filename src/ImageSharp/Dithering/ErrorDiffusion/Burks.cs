﻿// <copyright file="Burks.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the Burks image dithering algorithm.
    /// See <a href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Burks : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> BurksMatrix =
            new float[,]
            {
                { 0, 0, 0, 8, 4 },
                { 2, 4, 8, 4, 2 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Burks"/> class.
        /// </summary>
        public Burks()
            : base(BurksMatrix, 32)
        {
        }
    }
}