// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the Webp format.
    /// </summary>
    internal sealed class WebpEncoderCore : IImageEncoderInternals
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// TODO: not used at the moment.
        /// Indicating whether the alpha plane should be compressed with Webp lossless format.
        /// </summary>
        private readonly bool alphaCompression;

        /// <summary>
        /// Indicating whether lossy compression should be used. If false, lossless compression will be used.
        /// </summary>
        private readonly bool lossy;

        /// <summary>
        /// Compression quality. Between 0 and 100.
        /// </summary>
        private readonly int quality;

        /// <summary>
        /// Quality/speed trade-off (0=fast, 6=slower-better).
        /// </summary>
        private readonly WebpEncodingMethod method;

        /// <summary>
        /// The number of entropy-analysis passes (in [1..10]).
        /// </summary>
        private readonly int entropyPasses;

        /// <summary>
        /// Spatial Noise Shaping. 0=off, 100=maximum.
        /// </summary>
        private readonly int spatialNoiseShaping;

        /// <summary>
        /// The filter the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering).
        /// </summary>
        private readonly int filterStrength;

        /// <summary>
        /// Flag indicating whether to preserve the exact RGB values under transparent area. Otherwise, discard this invisible
        /// RGB information for better compression.
        /// </summary>
        private readonly WebpTransparentColorMode transparentColorMode;

        /// <summary>
        /// Indicating whether near lossless mode should be used.
        /// </summary>
        private readonly bool nearLossless;

        /// <summary>
        /// The near lossless quality. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).
        /// </summary>
        private readonly int nearLosslessQuality;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebpEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public WebpEncoderCore(IWebpEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.alphaCompression = options.UseAlphaCompression;
            this.lossy = options.Lossy;
            this.quality = options.Quality;
            this.method = options.Method;
            this.entropyPasses = options.EntropyPasses;
            this.spatialNoiseShaping = options.SpatialNoiseShaping;
            this.filterStrength = options.FilterStrength;
            this.transparentColorMode = options.TransparentColorMode;
            this.nearLossless = options.NearLossless;
            this.nearLosslessQuality = options.NearLosslessQuality;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();

            if (this.lossy)
            {
                using var enc = new Vp8Encoder(
                    this.memoryAllocator,
                    this.configuration,
                    image.Width,
                    image.Height,
                    this.quality,
                    this.method,
                    this.entropyPasses,
                    this.filterStrength,
                    this.spatialNoiseShaping);
                enc.Encode(image, stream);
            }
            else
            {
                using var enc = new Vp8LEncoder(
                    this.memoryAllocator,
                    this.configuration,
                    image.Width,
                    image.Height,
                    this.quality,
                    this.method,
                    this.transparentColorMode,
                    this.nearLossless,
                    this.nearLosslessQuality);
                enc.Encode(image, stream);
            }
        }
    }
}
