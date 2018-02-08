// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Dithering.Base;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the 4x4 ordered dithering matrix.
    /// <see href="https://en.wikipedia.org/wiki/Ordered_dithering"/>
    /// </summary>
    public sealed class OrderedDither : OrderedDitherBase
    {
        /// <summary>
        /// The threshold matrix.
        /// This is calculated by multiplying each value in the original matrix by 16
        /// </summary>
        private static readonly Fast2DArray<byte> ThresholdMatrix =
            new byte[,]
            {
               { 0, 128, 32, 160 },
               { 192, 64, 224, 96 },
               { 48, 176, 16, 144 },
               { 240, 112, 208, 80 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDither"/> class.
        /// </summary>
        public OrderedDither()
            : base(ThresholdMatrix)
        {
        }
    }
}