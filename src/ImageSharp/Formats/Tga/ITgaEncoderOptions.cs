// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Configuration options for use during tga encoding.
    /// </summary>
    internal interface ITgaEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        TgaBitsPerPixel? BitsPerPixel { get; }

        /// <summary>
        /// Gets a value indicating whether run length compression should be used.
        /// </summary>
        TgaCompression Compression { get; }
    }
}
