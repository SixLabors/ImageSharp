// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Enum for the different tiff encoding options.
    /// </summary>
    public enum TiffEncodingMode
    {
        /// <summary>
        /// No mode specified. Will preserve the bits per pixels of the input image.
        /// </summary>
        Default = 0,

        /// <summary>
        /// The image will be encoded as RGB, 8 bit per channel.
        /// </summary>
        Rgb = 1,

        /// <summary>
        /// The image will be encoded as RGB with a color palette.
        /// </summary>
        ColorPalette = 2,

        /// <summary>
        /// The image will be encoded as 8 bit gray.
        /// </summary>
        Gray = 3,

        /// <summary>
        /// The image will be written as a white and black image.
        /// </summary>
        BiColor = 4,
    }
}
