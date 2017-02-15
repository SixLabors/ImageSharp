// <copyright file="Atkinson.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    /// <summary>
    /// Applies error diffusion based dithering using the Atkinson image dithering algorithm.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public sealed class Atkinson : ErrorDiffuser
    {
        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private static readonly byte[][] AtkinsonMatrix =
            {
               new byte[] { 0, 0, 1, 1 },
               new byte[] { 1, 1, 1, 0 },
               new byte[] { 0, 1, 0, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="Atkinson"/> class.
        /// </summary>
        public Atkinson()
            : base(AtkinsonMatrix, 8)
        {
        }
    }
}
