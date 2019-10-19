// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Performs the bitmap decoding operation.
    /// </summary>
    internal sealed class WebPDecoderCore
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly IWebPDecoderOptions options;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public WebPDecoderCore(Configuration configuration, IWebPDecoderOptions options)
        {
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var metadata = new ImageMetadata();
            WebPMetadata webpMetadata = metadata.GetFormatMetadata(WebPFormat.Instance);
            this.currentStream = stream;

            uint chunkSize = this.ReadImageHeader();
            WebPImageInfo imageInfo = this.ReadVp8Info();

            // TODO: there can be optional chunks after that, like EXIF.

            var image = new Image<TPixel>(this.configuration, imageInfo.Width, imageInfo.Height, this.metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            if (imageInfo.IsLossLess)
            {
                ReadSimpleLossless(pixels, image.Width, image.Height);
            }
            else
            {
                ReadSimpleLossy(pixels, image.Width, image.Height);
            }

            return image;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            var metadata = new ImageMetadata();
            WebPMetadata webpMetadata = metadata.GetFormatMetadata(WebPFormat.Instance);
            this.currentStream = stream;

            this.ReadImageHeader();
            WebPImageInfo imageInfo = this.ReadVp8Info();

            // TODO: not sure yet where to get this info. Assuming 24 bits for now.
            int bitsPerPixel = 24;
            return new ImageInfo(new PixelTypeInfo(bitsPerPixel), imageInfo.Width, imageInfo.Height, this.metadata);
        }

        private uint ReadImageHeader()
        {
            // Skip FourCC header, we already know its a RIFF file at this point.
            this.currentStream.Skip(4);

            // Read Chunk size.
            // The size of the file in bytes starting at offset 8.
            // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
            this.currentStream.Read(this.buffer, 0, 4);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            // Skip 'WEBP' from the header.
            this.currentStream.Skip(4);

            return chunkSize;
        }

        private WebPImageInfo ReadVp8Info()
        {
            // Read VP8 chunk header.
            this.currentStream.Read(this.buffer, 0, 4);

            if (this.buffer.AsSpan().SequenceEqual(WebPConstants.Vp8Header))
            {
                return this.ReadVp8Header();
            }

            if (this.buffer.AsSpan().SequenceEqual(WebPConstants.Vp8LHeader))
            {
                return this.ReadVp8LHeader();
            }

            if (this.buffer.SequenceEqual(WebPConstants.Vp8XHeader))
            {
                return this.ReadVp8XHeader();
            }

            WebPThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");

            return new WebPImageInfo();
        }

        private WebPImageInfo ReadVp8XHeader()
        {
            this.currentStream.Read(this.buffer, 0, 4);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            byte imageFeatures = (byte)this.currentStream.ReadByte();

            // 3 reserved bytes should follow which are supposed to be zero.
            this.currentStream.Read(this.buffer, 0, 3);

            // 3 bytes for the width.
            this.currentStream.Read(this.buffer, 0, 3);
            this.buffer[3] = 0;
            int width = BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            // 3 bytes for the height.
            this.currentStream.Read(this.buffer, 0, 3);
            this.buffer[3] = 0;
            int height = BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            return new WebPImageInfo()
                   {
                       Width = width,
                       Height = height,
                       IsLossLess = false, // note: this is maybe incorrect here
                       DataSize = chunkSize
                   };
        }

        private WebPImageInfo ReadVp8Header()
        {
            // VP8 data size.
            this.currentStream.Read(this.buffer, 0, 3);
            this.buffer[3] = 0;
            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            // https://tools.ietf.org/html/rfc6386#page-30
            // Frame tag that contains four fields:
            // - A 1-bit frame type (0 for key frames, 1 for interframes).
            // - A 3-bit version number.
            // - A 1-bit show_frame flag.
            // - A 19-bit field containing the size of the first data partition in bytes.
            this.currentStream.Read(this.buffer, 0, 3);
            int tmp = (this.buffer[2] << 16) | (this.buffer[1] << 8) | this.buffer[0];
            int isKeyFrame = tmp & 0x1;
            int version = (tmp >> 1) & 0x7;
            int showFrame = (tmp >> 4) & 0x1;

            // Check for VP8 magic bytes.
            this.currentStream.Read(this.buffer, 0, 4);
            if (!this.buffer.AsSpan(1).SequenceEqual(WebPConstants.Vp8MagicBytes))
            {
                WebPThrowHelper.ThrowImageFormatException("VP8 magic bytes not found");
            }

            this.currentStream.Read(this.buffer, 0, 4);

            // TODO: Get horizontal and vertical scale
            int width = BinaryPrimitives.ReadInt16LittleEndian(this.buffer) & 0x3fff;
            int height = BinaryPrimitives.ReadInt16LittleEndian(this.buffer.AsSpan(2)) & 0x3fff;

            return new WebPImageInfo()
                   {
                       Width = width,
                       Height = height,
                       IsLossLess = false,
                       DataSize = dataSize
                   };
        }

        private WebPImageInfo ReadVp8LHeader()
        {
            // VP8 data size.
            this.currentStream.Read(this.buffer, 0, 4);
            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            // One byte signature, should be 0x2f.
            byte signature = (byte)this.currentStream.ReadByte();
            if (signature != WebPConstants.Vp8LMagicByte)
            {
                WebPThrowHelper.ThrowImageFormatException("Invalid VP8L signature");
            }

            // The first 28 bits of the bitstream specify the width and height of the image.
            var bitReader = new Vp8LBitreader(this.currentStream);
            uint height = bitReader.Read(WebPConstants.Vp8LImageSizeBits) + 1;
            uint width = bitReader.Read(WebPConstants.Vp8LImageSizeBits) + 1;

            return new WebPImageInfo()
                   {
                       Width = (int)width,
                       Height = (int)height,
                       IsLossLess = true,
                       DataSize = dataSize
                   };
        }

        private void ReadSimpleLossy<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: implement decoding
        }

        private void ReadSimpleLossless<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: implement decoding
        }

        private void ReadExtended<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: implement decoding
        }
    }
}
