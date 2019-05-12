// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    public sealed class BmpEncoder : IImageEncoder, IBmpEncoderOptions
    {
        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public BmpBitsPerPixel? BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the encoder should support transparency.
        /// Note: Transparency support only works together with 32 bits per pixel. This option will
        /// change the default behavior of the encoder of writing a bitmap version 3 info header with no compression.
        /// Instead a bitmap version 4 info header will be written with the BITFIELDS compression.
        /// </summary>
        public bool SupportTransparency { get; set; }

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count for 8-Bit images.
        /// Defaults to OctreeQuantizer.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var encoder = new BmpEncoderCore(this, image.GetMemoryAllocator());
            encoder.Encode(image, stream);
        }
    }
}