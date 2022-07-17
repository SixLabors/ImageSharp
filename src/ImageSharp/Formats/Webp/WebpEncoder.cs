// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the Webp format.
    /// </summary>
    public sealed class WebpEncoder : IImageEncoder, IWebpEncoderOptions
    {
        /// <inheritdoc/>
        public WebpFileFormatType? FileFormat { get; set; }

        /// <inheritdoc/>
        public int Quality { get; set; } = 75;

        /// <inheritdoc/>
        public WebpEncodingMethod Method { get; set; } = WebpEncodingMethod.Default;

        /// <inheritdoc/>
        public bool UseAlphaCompression { get; set; } = true;

        /// <inheritdoc/>
        public int EntropyPasses { get; set; } = 1;

        /// <inheritdoc/>
        public int SpatialNoiseShaping { get; set; } = 50;

        /// <inheritdoc/>
        public int FilterStrength { get; set; } = 60;

        /// <inheritdoc/>
        public WebpTransparentColorMode TransparentColorMode { get; set; } = WebpTransparentColorMode.Clear;

        /// <inheritdoc/>
        public bool NearLossless { get; set; }

        /// <inheritdoc/>
        public int NearLosslessQuality { get; set; } = 100;

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoderCore(this, image.GetMemoryAllocator());
            encoder.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebpEncoderCore(this, image.GetMemoryAllocator());
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }
}
