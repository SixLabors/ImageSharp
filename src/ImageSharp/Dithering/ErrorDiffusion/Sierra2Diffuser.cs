// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Dithering.Base;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Sierra2 image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Sierra2Diffuser : ErrorDiffuserBase
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
        /// Initializes a new instance of the <see cref="Sierra2Diffuser"/> class.
        /// </summary>
        public Sierra2Diffuser()
            : base(Sierra2Matrix, 16)
        {
        }
    }
}