// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
        /// <inheritdoc/>
        public PngBitDepth? BitDepth { get; set; }

        /// <inheritdoc/>
        public PngColorType? ColorType { get; set; }

        /// <inheritdoc/>
        public PngFilterMethod? FilterMethod { get; set; }

        /// <inheritdoc/>
        public PngCompressionLevel CompressionLevel { get; set; } = PngCompressionLevel.DefaultCompression;

        /// <inheritdoc/>
        public int TextCompressionThreshold { get; set; } = 1024;

        /// <inheritdoc/>
        public float? Gamma { get; set; }

        /// <inheritdoc/>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = byte.MaxValue;

        /// <inheritdoc/>
        public PngInterlaceMode? InterlaceMethod { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var encoder = new PngEncoderCore(image.GetMemoryAllocator(), image.GetConfiguration(), new PngEncoderOptions(this)))
            {
                encoder.Encode(image, stream);
            }
        }
    }
}
