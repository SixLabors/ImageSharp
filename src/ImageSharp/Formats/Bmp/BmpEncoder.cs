﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    /// <remarks>The encoder can currently only write 24-bit RGB images to streams.</remarks>
    public sealed class BmpEncoder : IImageEncoder, IBmpEncoderOptions
    {
        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.RGB24;

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var encoder = new BmpEncoderCore(this);
            encoder.Encode(image, stream);
        }
    }
}
