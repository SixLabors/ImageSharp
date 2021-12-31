// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Configuration options for use during OpenExr encoding.
    /// </summary>
    internal interface IExrEncoderOptions
    {
        /// <summary>
        /// Gets the pixel type of the image.
        /// </summary>
        ExrPixelType? PixelType { get; }
    }
}
