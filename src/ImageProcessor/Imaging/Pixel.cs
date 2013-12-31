// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Pixel.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents a single pixel.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// Represents a single pixel.
    /// </summary>
    public struct Pixel
    {
        /// <summary>
        /// The red component of the pixel.
        /// </summary>
        public double R;

        /// <summary>
        /// The green component of the pixel.
        /// </summary>
        public double G;

        /// <summary>
        /// The blue component of the pixel.
        /// </summary>
        public double B;

        /// <summary>
        /// The alpha component of the pixel.
        /// </summary>
        public double A;
    }
}
