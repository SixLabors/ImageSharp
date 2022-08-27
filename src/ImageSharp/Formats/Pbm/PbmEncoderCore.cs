// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Text;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a PGM, PBM, PPM or PAM bitmap.
    /// </summary>
    internal sealed class PbmEncoderCore : IImageEncoderInternals
    {
        private const byte NewLine = (byte)'\n';
        private const byte Space = (byte)' ';
        private const byte P = (byte)'P';

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
        private PbmComponentType componentType;

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

            byte signature = this.DeduceSignature();
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
                this.componentType = this.options.ComponentType ?? metadata.ComponentType;
            }
            else
            {
                this.componentType = PbmComponentType.Bit;
            }
        }

        private byte DeduceSignature()
        {
            byte signature;
            if (this.colorType == PbmColorType.BlackAndWhite)
            {
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = (byte)'1';
                }
                else
                {
                    signature = (byte)'4';
                }
            }
            else if (this.colorType == PbmColorType.Grayscale)
            {
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = (byte)'2';
                }
                else
                {
                    signature = (byte)'5';
                }
            }
            else
            {
                // RGB ColorType
                if (this.encoding == PbmEncoding.Plain)
                {
                    signature = (byte)'3';
                }
                else
                {
                    signature = (byte)'6';
                }
            }

            return signature;
        }

        private void WriteHeader(Stream stream, byte signature, Size pixelSize)
        {
            Span<byte> buffer = stackalloc byte[128];

            int written = 3;
            buffer[0] = P;
            buffer[1] = signature;
            buffer[2] = NewLine;

            Utf8Formatter.TryFormat(pixelSize.Width, buffer.Slice(written), out int bytesWritten);
            written += bytesWritten;
            buffer[written++] = Space;
            Utf8Formatter.TryFormat(pixelSize.Height, buffer.Slice(written), out bytesWritten);
            written += bytesWritten;
            buffer[written++] = NewLine;

            if (this.colorType != PbmColorType.BlackAndWhite)
            {
                int maxPixelValue = this.componentType == PbmComponentType.Short ? 65535 : 255;
                Utf8Formatter.TryFormat(maxPixelValue, buffer.Slice(written), out bytesWritten);
                written += bytesWritten;
                buffer[written++] = NewLine;
            }

            stream.Write(buffer, 0, written);
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
                PlainEncoder.WritePixels(this.configuration, stream, image, this.colorType, this.componentType);
            }
            else
            {
                BinaryEncoder.WritePixels(this.configuration, stream, image, this.colorType, this.componentType);
            }
        }
    }
}
