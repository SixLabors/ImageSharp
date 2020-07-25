// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.WebP.Lossless;
using SixLabors.ImageSharp.Formats.WebP.Lossy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the WebP format.
    /// </summary>
    internal sealed class WebPEncoderCore
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// Indicating whether the alpha plane should be compressed with WebP lossless format.
        /// </summary>
        private bool alphaCompression;

        /// <summary>
        /// Indicating whether lossless compression should be used. If false, lossy compression will be used.
        /// </summary>
        private bool lossless;

        /// <summary>
        /// Compression quality. Between 0 and 100.
        /// </summary>
        private float quality;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public WebPEncoderCore(IWebPEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.alphaCompression = options.AlphaCompression;
            this.lossless = options.Lossless;
            this.quality = options.Quality;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;

            if (this.lossless)
            {
                var enc = new Vp8LEncoder(this.memoryAllocator, image.Width, image.Height);
                enc.Encode(image, stream);
            }
            else
            {
                var enc = new Vp8Encoder(this.memoryAllocator, image.Width, image.Height);
                enc.Encode(image, stream);
            }
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public async Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (stream.CanSeek)
            {
                this.Encode(image, stream);
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    this.Encode(image, ms);
                    ms.Position = 0;
                    await ms.CopyToAsync(stream).ConfigureAwait(false);
                }
            }
        }
    }
}
