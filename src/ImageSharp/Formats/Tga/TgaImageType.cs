// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.
    ImageSharp.Formats.Tga
{
    /// <summary>
    /// Defines the tga image type. The TGA File Format can be used to store Pseudo-Color,
    /// True-Color and Direct-Color images of various pixel depths.
    /// </summary>
    public enum TgaImageType : byte
    {
        /// <summary>
        /// No image data included.
        /// </summary>
        NoImageData = 0,

        /// <summary>
        /// Uncompressed, color mapped image.
        /// </summary>
        ColorMapped = 1,

        /// <summary>
        /// Uncompressed true color image.
        /// </summary>
        TrueColor = 2,

        /// <summary>
        /// Uncompressed Black and white (grayscale) image.
        /// </summary>
        BlackAndWhite = 3,

        /// <summary>
        /// Run length encoded, color mapped image.
        /// </summary>
        RleColorMapped = 9,

        /// <summary>
        /// Run length encoded, true color image.
        /// </summary>
        RleTrueColor = 10,

        /// <summary>
        /// Run length encoded, black and white (grayscale) image.
        /// </summary>
        RleBlackAndWhite = 11,
    }
}
