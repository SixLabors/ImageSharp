// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Image encoder for writing image data to a stream in png format.
    /// </summary>
    public sealed class PngEncoder : IImageEncoder, IPngEncoderOptions
    {
        /// <summary>
        /// Gets or sets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="ColorType"/> values.
        /// </summary>
        public PngBitDepth? BitDepth { get; set; }

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PngColorType? ColorType { get; set; }

        /// <summary>
        /// Gets or sets the filter method.
        /// </summary>
        public PngFilterMethod? FilterMethod { get; set; }

        /// <summary>
        /// Gets or sets the compression level 1-9.
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Gets or sets the threshold of characters in text metadata, when compression should be used.
        /// Defaults to 1024.
        /// </summary>
        public int TextCompressionThreshold { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the gamma value, that will be written the image.
        /// </summary>
        public float? Gamma { get; set; }

        /// <summary>
        /// Gets or sets quantizer for reducing the color count.
        /// Defaults to the <see cref="WuQuantizer"/>.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = byte.MaxValue;

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write an Adam7 interlaced image.
        /// </summary>
        public PngInterlaceMode? InterlaceMethod { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var encoder = new PngEncoderCore(image.GetMemoryAllocator(), image.GetConfiguration(), new PngEncoderOptions(this)))
            {
                encoder.Encode(image, stream);
            }
        }
    }
}
