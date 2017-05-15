// <copyright file="Ordered.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering.Ordered
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the 4x4 ordered dithering matrix.
    /// <see href="https://en.wikipedia.org/wiki/Ordered_dithering"/>
    /// </summary>
    public sealed class Ordered : OrderedDither4x4
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
        /// Initializes a new instance of the <see cref="Ordered"/> class.
        /// </summary>
        public Ordered()
            : base(ThresholdMatrix)
        {
        }
    }
}