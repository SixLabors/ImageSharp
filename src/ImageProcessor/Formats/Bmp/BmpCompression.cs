// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BmpCompression.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Defines how the compression type of the image data
//   in the bitmap file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Defines how the compression type of the image data
    /// in the bitmap file.
    /// </summary>
    internal enum BmpCompression
    {
        /// <summary>
        /// Each image row has a multiple of four elements. If the 
        /// row has less elements, zeros will be added at the right side.
        /// The format depends on the number of bits, stored in the info header.
        /// If the number of bits are one, four or eight each pixel data is 
        /// a index to the palette. If the number of bits are sixteen, 
        /// twenty-four or thirty-two each pixel contains a color.
        /// </summary>
        RGB = 0,

        /// <summary>
        /// Two bytes are one data record. If the first byte is not zero, the 
        /// next two half bytes will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// Not supported at the moment.
        /// </summary>
        RLE8 = 1,

        /// <summary>
        /// Two bytes are one data record. If the first byte is not zero, the 
        /// next byte will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// Not supported at the moment.
        /// </summary>
        RLE4 = 2,

        /// <summary>
        /// Each image row has a multiple of four elements. If the 
        /// row has less elements, zeros will be added at the right side.
        /// Not supported at the moment.
        /// </summary>
        BitFields = 3,

        /// <summary>
        /// The bitmap contains a JPG image. 
        /// Not supported at the moment.
        /// </summary>
        JPEG = 4,

        /// <summary>
        /// The bitmap contains a PNG image. 
        /// Not supported at the moment.
        /// </summary>
        PNG = 5
    }
}
