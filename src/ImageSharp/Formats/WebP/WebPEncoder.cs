// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the WebP format.
    /// </summary>
    public sealed class WebPEncoder : IImageEncoder, IWebPEncoderOptions
    {
        /// <inheritdoc/>
        public bool Lossy { get; set; }

        /// <inheritdoc/>
        public int Quality { get; set; }

        /// <inheritdoc/>
        public int Method { get; set; }

        /// <inheritdoc/>
        public bool AlphaCompression { get; set; }

        /// <inheritdoc/>
        public int EntropyPasses { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoderCore(this, image.GetMemoryAllocator());
            encoder.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoderCore(this, image.GetMemoryAllocator());
            return encoder.EncodeAsync(image, stream);
        }
    }
}
