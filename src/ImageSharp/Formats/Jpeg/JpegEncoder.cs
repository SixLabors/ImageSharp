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
        /// <inheritdoc/>
        public int? Quality { get; set; }

        /// <inheritdoc/>
        public JpegColorType? ColorType { get; set; }

        public JpegFrameConfig JpegFrameConfig { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new JpegEncoderCore(this, this.JpegFrameConfig);
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
            var encoder = new JpegEncoderCore(this, this.JpegFrameConfig);
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }

    public class JpegFrameConfig
    {
        public JpegFrameConfig(JpegColorType colorType, int precision)
        {
            this.ColorType = colorType;
            this.Precision = precision;

            int componentCount = GetComponentCountFromColorType(colorType);
            this.Components = new JpegComponentConfig[componentCount];

            static int GetComponentCountFromColorType(JpegColorType colorType)
            {
                switch (colorType)
                {
                    case JpegColorType.Luminance:
                        return 1;
                    case JpegColorType.YCbCrRatio444:
                    case JpegColorType.YCbCrRatio422:
                    case JpegColorType.YCbCrRatio420:
                    case JpegColorType.YCbCrRatio411:
                    case JpegColorType.YCbCrRatio410:
                    case JpegColorType.Rgb:
                        return 3;
                    case JpegColorType.Cmyk:
                    case JpegColorType.YccK:
                        return 4;
                    default:
                        throw new ArgumentException($"Unknown jpeg color space: {colorType}");
                }
            }
        }

        public JpegColorType ColorType { get; }

        public int Precision { get; }

        public JpegComponentConfig[] Components { get; }

        public JpegFrameConfig PopulateComponent(int index, int id, int hsf, int vsf, int quantIndex, int dcIndex, int acIndex)
        {
            this.Components[index] = new JpegComponentConfig
            {
                Id = id,
                HorizontalSampleFactor = hsf,
                VerticalSampleFactor = vsf,
                QuantizatioTableIndex = quantIndex,
                dcTableSelector = dcIndex,
                acTableSelector = acIndex,
            };

            return this;
        }
    }

    public class JpegComponentConfig
    {
        public int Id { get; set; }

        public int HorizontalSampleFactor { get; set; }

        public int VerticalSampleFactor { get; set; }

        public int QuantizatioTableIndex { get; set; }

        public int dcTableSelector { get; set; }

        public int acTableSelector { get; set; }
    }
}
