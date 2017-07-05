// <copyright file="BmpEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Configuration options for use during bmp encoding
    /// </summary>
    /// <remarks>The encoder can currently only write 24-bit rgb images to streams.</remarks>
    internal interface IBmpEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        BmpBitsPerPixel BitsPerPixel { get; }
    }
}
