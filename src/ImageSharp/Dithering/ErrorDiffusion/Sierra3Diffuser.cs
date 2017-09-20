﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Dithering
{
    using SixLabors.ImageSharp.Dithering.Base;
    using SixLabors.ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the Sierra3 image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Sierra3Diffuser : ErrorDiffuserBase
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly Fast2DArray<float> Sierra3Matrix =
            new float[,]
            {
               { 0, 0, 0, 5, 3 },
               { 2, 4, 5, 4, 2 },
               { 0, 2, 3, 2, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Sierra3Diffuser"/> class.
        /// </summary>
        public Sierra3Diffuser()
            : base(Sierra3Matrix, 32)
        {
        }
    }
}