// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    public sealed class JpegEncoder : IImageEncoder, IJpegEncoderOptions
    {
        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// Defaults to <value>75</value>.
        /// </summary>
        public int? Quality
        {
            [Obsolete("This accessor will soon be deprecated. Use LuminanceQuality and ChrominanceQuality getters instead.", error: false)]
            get
            {
                const int defaultQuality = 75;

                int lumaQuality = this.LuminanceQuality ?? defaultQuality;
                int chromaQuality = this.LuminanceQuality ?? lumaQuality;
                return (int)Math.Round((lumaQuality + chromaQuality) / 2f);
            }

            set
            {
                this.LuminanceQuality = value;
                this.ChrominanceQuality = value;
            }
        }

        /// <summary>
        /// Gets or sets the quality, that will be used to encode luminance image data.
        /// Quality index must be between 0 and 100 (compression from max to min).
        /// Defaults to <value>75</value>.
        /// </summary>
        public int? LuminanceQuality { get; set; }

        /// <summary>
        /// Gets or sets the quality, that will be used to encode chrominance image data.
        /// Quality index must be between 0 and 100 (compression from max to min).
        /// Defaults to <value>75</value>.
        /// </summary>
        public int? ChrominanceQuality { get; set; }

        /// <summary>
        /// Gets or sets the subsample ration, that will be used to encode the image.
        /// </summary>
        public JpegSubsample? Subsample { get; set; }

        /// <summary>
        /// Gets or sets the color type, that will be used to encode the image.
        /// </summary>
        public JpegColorType? ColorType { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new JpegEncoderCore(this);
            this.InitializeColorType<TPixel>(image);
            encoder.Encode(image, stream);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new JpegEncoderCore(this);
            this.InitializeColorType<TPixel>(image);
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }

        /// <summary>
        /// If ColorType was not set, set it based on the given image.
        /// </summary>
        private void InitializeColorType<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // First inspect the image metadata.
            if (this.ColorType == null)
            {
                JpegMetadata metadata = image.Metadata.GetJpegMetadata();
                this.ColorType = metadata.ColorType;
            }

            // Secondly, inspect the pixel type.
            if (this.ColorType == null)
            {
                bool isGrayscale =
                    typeof(TPixel) == typeof(L8) || typeof(TPixel) == typeof(L16) ||
                    typeof(TPixel) == typeof(La16) || typeof(TPixel) == typeof(La32);
                this.ColorType = isGrayscale ? JpegColorType.Luminance : JpegColorType.YCbCr;
            }
        }
    }
}
