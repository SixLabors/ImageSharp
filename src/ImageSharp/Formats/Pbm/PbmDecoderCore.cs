// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Performs the PBM decoding operation.
    /// </summary>
    internal sealed class PbmDecoderCore : IImageDecoderInternals
    {
        private int maxPixelValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="PbmDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public PbmDecoderCore(Configuration configuration) => this.Configuration = configuration ?? Configuration.Default;

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the colortype to use
        /// </summary>
        public PbmColorType ColorType { get; private set; }

        /// <summary>
        /// Gets the size of the pixel array
        /// </summary>
        public Size PixelSize { get; private set; }

        /// <summary>
        /// Gets the component data type
        /// </summary>
        public PbmComponentType ComponentType { get; private set; }

        /// <summary>
        /// Gets the Encoding of pixels
        /// </summary>
        public PbmEncoding Encoding { get; private set; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        /// <inheritdoc/>
        Size IImageDecoderInternals.Dimensions => this.PixelSize;

        private bool NeedsUpscaling => this.ColorType != PbmColorType.BlackAndWhite && this.maxPixelValue is not 255 and not 65535;

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.ProcessHeader(stream);

            var image = new Image<TPixel>(this.Configuration, this.PixelSize.Width, this.PixelSize.Height, this.Metadata);

            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            this.ProcessPixels(stream, pixels);
            if (this.NeedsUpscaling)
            {
                this.ProcessUpscaling(image);
            }

            return image;
        }

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.ProcessHeader(stream);

            // BlackAndWhite pixels are encoded into a byte.
            int bitsPerPixel = this.ComponentType == PbmComponentType.Short ? 16 : 8;
            return new ImageInfo(new PixelTypeInfo(bitsPerPixel), this.PixelSize.Width, this.PixelSize.Height, this.Metadata);
        }

        /// <summary>
        /// Processes the ppm header.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        private void ProcessHeader(BufferedReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[2];

            int bytesRead = stream.Read(buffer);
            if (bytesRead != 2 || buffer[0] != 'P')
            {
                throw new InvalidImageContentException("Empty or not an PPM image.");
            }

            switch ((char)buffer[1])
            {
                case '1':
                    // Plain PBM format: 1 component per pixel, boolean value ('0' or '1').
                    this.ColorType = PbmColorType.BlackAndWhite;
                    this.Encoding = PbmEncoding.Plain;
                    break;
                case '2':
                    // Plain PGM format: 1 component per pixel, in decimal text.
                    this.ColorType = PbmColorType.Grayscale;
                    this.Encoding = PbmEncoding.Plain;
                    break;
                case '3':
                    // Plain PPM format: 3 components per pixel, in decimal text.
                    this.ColorType = PbmColorType.Rgb;
                    this.Encoding = PbmEncoding.Plain;
                    break;
                case '4':
                    // Binary PBM format: 1 component per pixel, 8 pixels per byte.
                    this.ColorType = PbmColorType.BlackAndWhite;
                    this.Encoding = PbmEncoding.Binary;
                    break;
                case '5':
                    // Binary PGM format: 1 components per pixel, in binary integers.
                    this.ColorType = PbmColorType.Grayscale;
                    this.Encoding = PbmEncoding.Binary;
                    break;
                case '6':
                    // Binary PPM format: 3 components per pixel, in binary integers.
                    this.ColorType = PbmColorType.Rgb;
                    this.Encoding = PbmEncoding.Binary;
                    break;
                case '7':
                // PAM image: sequence of images.
                // Not implemented yet
                default:
                    throw new InvalidImageContentException("Unknown of not implemented image type encountered.");
            }

            stream.SkipWhitespaceAndComments();
            int width = stream.ReadDecimal();
            stream.SkipWhitespaceAndComments();
            int height = stream.ReadDecimal();
            stream.SkipWhitespaceAndComments();
            if (this.ColorType != PbmColorType.BlackAndWhite)
            {
                this.maxPixelValue = stream.ReadDecimal();
                if (this.maxPixelValue > 255)
                {
                    this.ComponentType = PbmComponentType.Short;
                }
                else
                {
                    this.ComponentType = PbmComponentType.Byte;
                }

                stream.SkipWhitespaceAndComments();
            }
            else
            {
                this.ComponentType = PbmComponentType.Bit;
            }

            this.PixelSize = new Size(width, height);
            this.Metadata = new ImageMetadata();
            PbmMetadata meta = this.Metadata.GetPbmMetadata();
            meta.Encoding = this.Encoding;
            meta.ColorType = this.ColorType;
            meta.ComponentType = this.ComponentType;
        }

        private void ProcessPixels<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.Encoding == PbmEncoding.Binary)
            {
                BinaryDecoder.Process(this.Configuration, pixels, stream, this.ColorType, this.ComponentType);
            }
            else
            {
                PlainDecoder.Process(this.Configuration, pixels, stream, this.ColorType, this.ComponentType);
            }
        }

        private void ProcessUpscaling<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int maxAllocationValue = this.ComponentType == PbmComponentType.Short ? 65535 : 255;
            float factor = maxAllocationValue / this.maxPixelValue;
            image.Mutate(x => x.Brightness(factor));
        }
    }
}
