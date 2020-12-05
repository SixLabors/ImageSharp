// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        /// <inheritdoc/>
        public byte Threshold { get; set; } = byte.MaxValue;

        /// <inheritdoc/>
        public PngInterlaceMode? InterlaceMethod { get; set; }

        /// <inheritdoc/>
        public PngChunkFilter? ChunkFilter { get; set; }

        /// <inheritdoc/>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public PngTransparentColorMode TransparentColorMode { get; set; }

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

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // The introduction of a local variable that refers to an object the implements
            // IDisposable means you must use async/await, where the compiler generates the
            // state machine and a continuation.
            using (var encoder = new PngEncoderCore(image.GetMemoryAllocator(), image.GetConfiguration(), new PngEncoderOptions(this)))
            {
                await encoder.EncodeAsync(image, stream, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
