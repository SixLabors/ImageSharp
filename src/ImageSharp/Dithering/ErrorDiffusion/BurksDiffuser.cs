﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Dithering
{
    using SixLabors.ImageSharp.Dithering.Base;
    using SixLabors.ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the Burks image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class BurksDiffuser : ErrorDiffuserBase
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
        /// Initializes a new instance of the <see cref="BurksDiffuser"/> class.
        /// </summary>
        public BurksDiffuser()
            : base(BurksMatrix, 32)
        {
        }
    }
}