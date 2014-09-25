// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PixelData.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Contains the component parts that make up a single pixel.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// Contains the component parts that make up a single pixel.
    /// </summary>
    public struct PixelData
    {
        /// <summary>
        /// The blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// The green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// The red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// The alpha component.
        /// </summary>
        public byte A;
    }
}