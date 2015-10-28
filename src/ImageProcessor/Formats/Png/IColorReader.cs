// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IColorReader.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods for color readers, which are responsible for reading
//   different color formats from a png file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Encapsulates methods for color readers, which are responsible for reading 
    /// different color formats from a png file.
    /// </summary>
    public interface IColorReader
    {
        /// <summary>
        /// Reads the specified scanline.
        /// </summary>
        /// <param name="scanline">The scanline.</param>
        /// <param name="pixels">The pixels, where the colors should be stored in RGBA format.</param>
        /// <param name="header">
        /// The header, which contains information about the png file, like
        /// the width of the image and the height.
        /// </param>
        void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header);
    }
}
