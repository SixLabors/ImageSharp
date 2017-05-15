// <copyright file="Bayer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering.Ordered
{
    using ImageSharp.Memory;

    /// <summary>
    /// Applies error diffusion based dithering using the 4x4 Bayer dithering matrix.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Bayer : OrderedDither4x4
    {
        /// <summary>
        /// The threshold matrix.
        /// This is calculated by multiplying each value in the original matrix by 16 and subtracting 1
        /// </summary>
        private static readonly Fast2DArray<byte> ThresholdMatrix =
            new byte[,]
            {
                { 15, 143, 47, 175 },
                { 207, 79, 239, 111 },
                { 63, 191, 31, 159 },
                { 255, 127, 223, 95 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Bayer"/> class.
        /// </summary>
        public Bayer()
            : base(ThresholdMatrix)
        {
        }
    }
}