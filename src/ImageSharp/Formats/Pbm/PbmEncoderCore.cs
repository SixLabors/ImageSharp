// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a PGM, PBM, PPM or PAM bitmap.
    /// </summary>
    internal sealed class PbmEncoderCore : IImageEncoderInternals
    {
        private const char NewLine = '\n';

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The encoder options.
        /// </summary>
        private readonly IPbmEncoderOptions options;

        /// <summary>
        /// The encoding for the pixels.
        /// </summary>
        private PbmEncoding encoding;

        /// <summary>
        /// Gets the Color type of the resulting image.
        /// </summary>
        private PbmColorType colorType;

        /// <summary>
        /// Gets the maximum pixel value, per component.
        /// </summary>
        private int maxPixelValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="PbmEncoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The encoder options.</param>
        public PbmEncoderCore(Configuration configuration, IPbmEncoderOptions options)
        {
            this.configuration = configuration;
            this.options = options;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to request cancellation.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.DeduceOptions(image);

            string signature = this.DeduceSignature();
            this.WriteHeader(stream, signature, image.Size());

            this.WritePixels(stream, image.Frames.RootFrame);

            stream.Flush();
        }

        private void DeduceOptions<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.configuration = image.GetConfiguration();
            PbmMetadata metadata = image.Metadata.GetPbmMetadata();
            this.encoding = this.options.Encoding ?? metadata.Encoding;
            this.colorType = this.options.ColorType ?? metadata.ColorType;
            if (this.colorType != PbmColorType.BlackAndWhite)
            {
                this.maxPixelValue = this.options.MaxPixelValue ?? metadata.MaxPixelValue;
            }
        }

        private string DeduceSignature()
        {
            string signature;
            if (this.colorType == PbmColorType.BlackAndWhite)
            {
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = "P1";
                }
                else
                {
                    signature = "P4";
                }
            }
            else if (this.colorType == PbmColorType.Grayscale)
            {
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = "P2";
                }
                else
                {
                    signature = "P5";
                }
            }
            else
            {
                // RGB ColorType
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = "P3";
                }
                else
                {
                    signature = "P6";
                }
            }

            return signature;
        }

        private void WriteHeader(Stream stream, string signature, Size pixelSize)
        {
            var builder = new StringBuilder(20);
            builder.Append(signature);
            builder.Append(NewLine);
            builder.Append(pixelSize.Width.ToString());
            builder.Append(NewLine);
            builder.Append(pixelSize.Height.ToString());
            builder.Append(NewLine);
            if (this.colorType != PbmColorType.BlackAndWhite)
            {
                builder.Append(this.maxPixelValue.ToString());
                builder.Append(NewLine);
            }

            string headerStr = builder.ToString();
            byte[] headerBytes = Encoding.ASCII.GetBytes(headerStr);
            stream.Write(headerBytes, 0, headerBytes.Length);
        }

        /// <summary>
        /// Writes the pixel data to the binary stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image">
        /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.
        /// </param>
        private void WritePixels<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.encoding == PbmEncoding.Plain)
            {
                PlainEncoder.WritePixels(this.configuration, stream, image, this.colorType, this.maxPixelValue);
            }
            else
            {
                BinaryEncoder.WritePixels(this.configuration, stream, image, this.colorType, this.maxPixelValue);
            }
        }
    }
}
