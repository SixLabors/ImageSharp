// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

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
        /// The webp specific metadata.
        /// </summary>
        private WebPMetadata webpMetadata;

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
            this.currentStream = stream;

            uint fileSize = this.ReadImageHeader();
            WebPImageInfo imageInfo = this.ReadVp8Info();
            if (imageInfo.Features != null && imageInfo.Features.Animation)
            {
                WebPThrowHelper.ThrowNotSupportedException("Animations are not supported");
            }

            var image = new Image<TPixel>(this.configuration, imageInfo.Width, imageInfo.Height, this.metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            if (imageInfo.IsLossLess)
            {
                ReadSimpleLossless(pixels, image.Width, image.Height, (int)imageInfo.ImageDataSize);
            }
            else
            {
                ReadSimpleLossy(pixels, image.Width, image.Height, (int)imageInfo.ImageDataSize);
            }

            // There can be optional chunks after the image data, like EXIF, XMP etc.
            if (imageInfo.Features != null)
            {
                this.ParseOptionalChunks(imageInfo.Features);
            }

            return image;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
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

            // Read file size.
            // The size of the file in bytes starting at offset 8.
            // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
            uint chunkSize = this.ReadChunkSize();

            // Skip 'WEBP' from the header.
            this.currentStream.Skip(4);

            return chunkSize;
        }

        private WebPImageInfo ReadVp8Info()
        {
            this.metadata = new ImageMetadata();
            this.webpMetadata = metadata.GetFormatMetadata(WebPFormat.Instance);

            WebPChunkType chunkType = this.ReadChunkType();

            switch (chunkType)
            {
                case WebPChunkType.Vp8:
                    return this.ReadVp8Header();
                case WebPChunkType.Vp8L:
                    return this.ReadVp8LHeader();
                case WebPChunkType.Vp8X:
                    return this.ReadVp8XHeader();
            }

            WebPThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");

            return new WebPImageInfo();
        }

        private WebPImageInfo ReadVp8XHeader()
        {
            uint chunkSize = this.ReadChunkSize();

            // This byte contains information about the image features used.
            // The first two bit should and the last bit should be 0.
            // TODO: should an exception be thrown if its not the case, or just ignore it?
            byte imageFeatures = (byte)this.currentStream.ReadByte();

            // If bit 3 is set, a ICC Profile Chunk should be present.
            bool isIccPresent = (imageFeatures & (1 << 5)) != 0;

            // If bit 4 is set, any of the frames of the image contain transparency information ("alpha" chunk).
            bool isAlphaPresent = (imageFeatures & (1 << 4)) != 0;

            // If bit 5 is set, a EXIF metadata should be present.
            bool isExifPresent = (imageFeatures & (1 << 3)) != 0;

            // If bit 6 is set, XMP metadata should be present.
            bool isXmpPresent = (imageFeatures & (1 << 2)) != 0;

            // If bit 7 is set, animation should be present.
            bool isAnimationPresent = (imageFeatures & (1 << 1)) != 0;

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

            // Optional chunks ALPH, ICCP and ANIM can follow here. Ignoring them for now.
            WebPChunkType chunkType;
            if (isIccPresent)
            {
                chunkType = this.ReadChunkType();
                uint iccpChunkSize = this.ReadChunkSize();
                this.currentStream.Skip((int)iccpChunkSize);
            }

            if (isAnimationPresent)
            {
                this.webpMetadata.Animated = true;

                return new WebPImageInfo()
                       {
                           Width = width,
                           Height = height,
                           Features = new WebPFeatures()
                           {
                               Animation = true
                           }
                       };
            }

            if (isAlphaPresent)
            {
                chunkType = this.ReadChunkType();
                uint alphaChunkSize = this.ReadChunkSize();
                this.currentStream.Skip((int)alphaChunkSize);
            }

            var features = new WebPFeatures()
                                    {
                                        Animation = isAnimationPresent,
                                        Alpha = isAlphaPresent,
                                        ExifProfile = isExifPresent,
                                        IccProfile = isIccPresent,
                                        XmpMetaData = isXmpPresent
                                    };

            // A VP8 or VP8L chunk should follow here.
            chunkType = this.ReadChunkType();

            // TOOD: image width and height from VP8X should overrule VP8 or VP8L info, because its 3 bytes instead of just 14 bit.
            switch (chunkType)
            {
                case WebPChunkType.Vp8:
                    return this.ReadVp8Header(features);
                case WebPChunkType.Vp8L:
                    return this.ReadVp8LHeader(features);
            }

            WebPThrowHelper.ThrowImageFormatException("Unexpected chunk followed VP8X header");

            return new WebPImageInfo();
        }

        private WebPImageInfo ReadVp8Header(WebPFeatures features = null)
        {
            this.webpMetadata.Format = WebPFormatType.Lossy;

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
                       ImageDataSize = dataSize,
                       Features = features
                   };
        }

        private WebPImageInfo ReadVp8LHeader(WebPFeatures features = null)
        {
            this.webpMetadata.Format = WebPFormatType.Lossless;

            // VP8 data size.
            uint dataSize = this.ReadChunkSize();

            // One byte signature, should be 0x2f.
            byte signature = (byte)this.currentStream.ReadByte();
            if (signature != WebPConstants.Vp8LMagicByte)
            {
                WebPThrowHelper.ThrowImageFormatException("Invalid VP8L signature");
            }

            // The first 28 bits of the bitstream specify the width and height of the image.
            var bitReader = new Vp8LBitReader(this.currentStream);
            uint width = bitReader.Read(WebPConstants.Vp8LImageSizeBits) + 1;
            uint height = bitReader.Read(WebPConstants.Vp8LImageSizeBits) + 1;

            // The alpha_is_used flag should be set to 0 when all alpha values are 255 in the picture, and 1 otherwise.
            bool alphaIsUsed = bitReader.ReadBit();

            // The next 3 bytes are the version. The version_number is a 3 bit code that must be set to 0.
            // Any other value should be treated as an error.
            // TODO: should we throw here when version number is != 0?
            uint version = bitReader.Read(3);

            return new WebPImageInfo()
                   {
                       Width = (int)width,
                       Height = (int)height,
                       IsLossLess = true,
                       ImageDataSize = dataSize,
                       Features = features
                   };
        }

        private void ReadSimpleLossy<TPixel>(Buffer2D<TPixel> pixels, int width, int height, int imageDataSize)
            where TPixel : struct, IPixel<TPixel>
        {
            var lossyDecoder = new WebPLossyDecoder(this.configuration, this.currentStream);
            lossyDecoder.Decode(pixels, width, height, imageDataSize);
        }

        private void ReadSimpleLossless<TPixel>(Buffer2D<TPixel> pixels, int width, int height, int imageDataSize)
            where TPixel : struct, IPixel<TPixel>
        {
            var losslessDecoder = new WebPLosslessDecoder(this.currentStream);
            losslessDecoder.Decode(pixels, width, height, imageDataSize);

            // TODO: implement decoding. For simulating the decoding: skipping the chunk size bytes.
            this.currentStream.Skip(imageDataSize + 34); // TODO: Not sure why the additional data starts at offset +34 at the moment.
        }

        private void ReadExtended<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: implement decoding
        }

        private void ParseOptionalChunks(WebPFeatures features)
        {
            if (features.ExifProfile == false && features.XmpMetaData == false)
            {
                return;
            }

            while (this.currentStream.Position < this.currentStream.Length)
            {
                // Read chunk header.
                WebPChunkType chunkType = this.ReadChunkType();
                uint chunkLength = this.ReadChunkSize();

                // Skip chunk data for now.
                this.currentStream.Skip((int)chunkLength);
            }
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private WebPChunkType ReadChunkType()
        {
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                var chunkType = (WebPChunkType)BinaryPrimitives.ReadUInt32BigEndian(this.buffer);
                this.webpMetadata.ChunkTypes.Enqueue(chunkType);
                return chunkType;
            }

            throw new ImageFormatException("Invalid WebP data.");
        }

        /// <summary>
        /// Reads the chunk size. If Chunk Size is odd, a single padding byte will be added to the payload,
        /// so the chunk size will be increased by 1 in those cases.
        /// </summary>
        /// <returns>The chunk size in bytes.</returns>
        private uint ReadChunkSize()
        {
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);
                return (chunkSize % 2 == 0) ? chunkSize : chunkSize + 1;
            }

            throw new ImageFormatException("Invalid WebP data.");
        }
    }
}
