// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a targa truevision image.
    /// </summary>
    public sealed class TgaEncoder : IImageEncoder, ITgaEncoderOptions
    {
        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public TgaBitsPerPixel? BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether no compression or run length compression should be used.
        /// </summary>
        public TgaCompression Compression { get; set; } = TgaCompression.RunLength;

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new TgaEncoderCore(this, image.GetMemoryAllocator());
            encoder.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new TgaEncoderCore(this, image.GetMemoryAllocator());
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }
}
