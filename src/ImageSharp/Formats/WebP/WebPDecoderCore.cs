// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

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
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

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
            this.IgnoreMetadata = options.IgnoreMetadata;
        }

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/> and sets the data to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.Metadata = new ImageMetadata();
            this.currentStream = stream;

            uint fileSize = this.ReadImageHeader();
            WebPImageInfo imageInfo = this.ReadVp8Info();
            if (imageInfo.Features != null && imageInfo.Features.Animation)
            {
                WebPThrowHelper.ThrowNotSupportedException("Animations are not supported");
            }

            var image = new Image<TPixel>(this.configuration, (int)imageInfo.Width, (int)imageInfo.Height, this.Metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            if (imageInfo.IsLossLess)
            {
                var losslessDecoder = new WebPLosslessDecoder(imageInfo.Vp8LBitReader, this.memoryAllocator, this.configuration);
                losslessDecoder.Decode(pixels, image.Width, image.Height);
            }
            else
            {
                var lossyDecoder = new WebPLossyDecoder(imageInfo.Vp8BitReader, this.memoryAllocator, this.configuration);
                lossyDecoder.Decode(pixels, image.Width, image.Height, imageInfo);
            }

            // There can be optional chunks after the image data, like EXIF and XMP.
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

            return new ImageInfo(new PixelTypeInfo((int)imageInfo.BitsPerPixel), (int)imageInfo.Width, (int)imageInfo.Height, this.Metadata);
        }

        /// <summary>
        /// Reads and skips over the image header.
        /// </summary>
        /// <returns>The chunk size in bytes.</returns>
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
            this.Metadata = new ImageMetadata();
            this.webpMetadata = this.Metadata.GetFormatMetadata(WebPFormat.Instance);

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

        /// <summary>
        /// Reads an the extended webp file header. An extended file header consists of:
        /// - A 'VP8X' chunk with information about features used in the file.
        /// - An optional 'ICCP' chunk with color profile.
        /// - An optional 'ANIM' chunk with animation control data.
        /// - An optional 'ALPH' chunk with alpha channel data.
        /// After the image header, image data will follow. After that optional image metadata chunks (EXIF and XMP) can follow.
        /// </summary>
        /// <returns>Information about this webp image.</returns>
        private WebPImageInfo ReadVp8XHeader()
        {
            uint chunkSize = this.ReadChunkSize();

            // The first byte contains information about the image features used.
            // The first two bit of it are reserved and should be 0. TODO: should an exception be thrown if its not the case, or just ignore it?
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
            uint width = (uint)BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            // 3 bytes for the height.
            this.currentStream.Read(this.buffer, 0, 3);
            this.buffer[3] = 0;
            uint height = (uint)BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            // Optional chunks ICCP, ALPH and ANIM can follow here.
            WebPChunkType chunkType;
            if (isIccPresent)
            {
                chunkType = this.ReadChunkType();
                if (chunkType is WebPChunkType.Iccp)
                {
                    uint iccpChunkSize = this.ReadChunkSize();
                    if (!this.IgnoreMetadata)
                    {
                        var iccpData = new byte[iccpChunkSize];
                        this.currentStream.Read(iccpData, 0, (int)iccpChunkSize);
                        var profile = new IccProfile(iccpData);
                        if (profile.CheckIsValid())
                        {
                            this.Metadata.IccProfile = profile;
                        }
                    }
                    else
                    {
                        this.currentStream.Skip((int)iccpChunkSize);
                    }
                }
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

            byte[] alphaData = null;
            byte alphaChunkHeader = 0;
            if (isAlphaPresent)
            {
                chunkType = this.ReadChunkType();
                if (chunkType != WebPChunkType.Alpha)
                {
                    WebPThrowHelper.ThrowImageFormatException($"unexpected chunk type {chunkType}, expected ALPH chunk is missing");
                }

                uint alphaChunkSize = this.ReadChunkSize();
                alphaChunkHeader = (byte)this.currentStream.ReadByte();
                alphaData = new byte[alphaChunkSize - 1];
                this.currentStream.Read(alphaData, 0, alphaData.Length);
            }

            var features = new WebPFeatures()
                                    {
                                        Animation = isAnimationPresent,
                                        Alpha = isAlphaPresent,
                                        AlphaData = alphaData,
                                        AlphaChunkHeader = alphaChunkHeader,
                                        ExifProfile = isExifPresent,
                                        IccProfile = isIccPresent,
                                        XmpMetaData = isXmpPresent
                                    };

            // A VP8 or VP8L chunk should follow here.
            chunkType = this.ReadChunkType();

            // TOOD: check if VP8 or VP8L info about the dimensions match VP8X info
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

        /// <summary>
        /// Reads the header of a lossy webp image.
        /// </summary>
        /// <param name="features">Webp features.</param>
        /// <returns>Information about this webp image.</returns>
        private WebPImageInfo ReadVp8Header(WebPFeatures features = null)
        {
            this.webpMetadata.Format = WebPFormatType.Lossy;

            // VP8 data size (not including this 4 bytes).
            this.currentStream.Read(this.buffer, 0, 4);
            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            // remaining counts the available image data payload.
            uint remaining = dataSize;

            // Paragraph 9.1 https://tools.ietf.org/html/rfc6386#page-30
            // Frame tag that contains four fields:
            // - A 1-bit frame type (0 for key frames, 1 for interframes).
            // - A 3-bit version number.
            // - A 1-bit show_frame flag.
            // - A 19-bit field containing the size of the first data partition in bytes.
            this.currentStream.Read(this.buffer, 0, 3);
            uint frameTag = (uint)(this.buffer[0] | (this.buffer[1] << 8) | (this.buffer[2] << 16));
            remaining -= 3;
            bool isKeyFrame = (frameTag & 0x1) is 0;
            if (!isKeyFrame)
            {
                WebPThrowHelper.ThrowImageFormatException("VP8 header indicates the image is not a key frame");
            }

            uint version = (frameTag >> 1) & 0x7;
            if (version > 3)
            {
                WebPThrowHelper.ThrowImageFormatException($"VP8 header indicates unknown profile {version}");
            }

            bool showFrame = ((frameTag >> 4) & 0x1) is 1;
            if (!showFrame)
            {
                WebPThrowHelper.ThrowImageFormatException("VP8 header indicates that the first frame is invisible");
            }

            uint partitionLength = frameTag >> 5;
            if (partitionLength > dataSize)
            {
                WebPThrowHelper.ThrowImageFormatException("VP8 header contains inconsistent size information");
            }

            // Check for VP8 magic bytes.
            this.currentStream.Read(this.buffer, 0, 3);
            if (!this.buffer.AsSpan().Slice(0, 3).SequenceEqual(WebPConstants.Vp8MagicBytes))
            {
                WebPThrowHelper.ThrowImageFormatException("VP8 magic bytes not found");
            }

            this.currentStream.Read(this.buffer, 0, 4);
            uint tmp = (uint)BinaryPrimitives.ReadInt16LittleEndian(this.buffer);
            uint width = tmp & 0x3fff;
            sbyte xScale = (sbyte)(tmp >> 6);
            tmp = (uint)BinaryPrimitives.ReadInt16LittleEndian(this.buffer.AsSpan(2));
            uint height = tmp & 0x3fff;
            sbyte yScale = (sbyte)(tmp >> 6);
            remaining -= 7;
            if (width is 0 || height is 0)
            {
                WebPThrowHelper.ThrowImageFormatException("width or height can not be zero");
            }

            if (partitionLength > remaining)
            {
                WebPThrowHelper.ThrowImageFormatException("bad partition length");
            }

            var vp8FrameHeader = new Vp8FrameHeader()
                                 {
                                     KeyFrame = true,
                                     Profile = (sbyte)version,
                                     PartitionLength = partitionLength
                                 };

            var bitReader = new Vp8BitReader(
                this.currentStream,
                remaining,
                this.memoryAllocator,
                partitionLength);
            bitReader.Remaining = remaining;

            return new WebPImageInfo()
                   {
                       Width = width,
                       Height = height,
                       XScale = xScale,
                       YScale = yScale,
                       BitsPerPixel = features?.Alpha is true ? WebPBitsPerPixel.Pixel32 : WebPBitsPerPixel.Pixel24,
                       IsLossLess = false,
                       Features = features,
                       Vp8Profile = (sbyte)version,
                       Vp8FrameHeader = vp8FrameHeader,
                       Vp8BitReader = bitReader
                   };
        }

        /// <summary>
        /// Reads the header of a lossless webp image.
        /// </summary>
        /// <param name="features">Webp image features.</param>
        /// <returns>Information about this image.</returns>
        private WebPImageInfo ReadVp8LHeader(WebPFeatures features = null)
        {
            this.webpMetadata.Format = WebPFormatType.Lossless;

            // VP8 data size.
            uint imageDataSize = this.ReadChunkSize();

            var bitReader = new Vp8LBitReader(this.currentStream, imageDataSize, this.memoryAllocator);

            // One byte signature, should be 0x2f.
            uint signature = bitReader.ReadValue(8);
            if (signature != WebPConstants.Vp8LMagicByte)
            {
                WebPThrowHelper.ThrowImageFormatException("Invalid VP8L signature");
            }

            // The first 28 bits of the bitstream specify the width and height of the image.
            uint width = bitReader.ReadValue(WebPConstants.Vp8LImageSizeBits) + 1;
            uint height = bitReader.ReadValue(WebPConstants.Vp8LImageSizeBits) + 1;
            if (width is 0 || height is 0)
            {
                WebPThrowHelper.ThrowImageFormatException("width or height can not be zero");
            }

            // The alphaIsUsed flag should be set to 0 when all alpha values are 255 in the picture, and 1 otherwise.
            // TODO: this flag value is not used yet
            bool alphaIsUsed = bitReader.ReadBit();

            // The next 3 bits are the version. The version number is a 3 bit code that must be set to 0.
            // Any other value should be treated as an error.
            uint version = bitReader.ReadValue(WebPConstants.Vp8LVersionBits);
            if (version != 0)
            {
                WebPThrowHelper.ThrowNotSupportedException($"Unexpected version number {version} found in VP8L header");
            }

            return new WebPImageInfo()
                   {
                       Width = width,
                       Height = height,
                       BitsPerPixel = WebPBitsPerPixel.Pixel32,
                       IsLossLess = true,
                       Features = features,
                       Vp8LBitReader = bitReader
                   };
        }

        /// <summary>
        /// Parses optional metadata chunks. There SHOULD be at most one chunk of each type ('EXIF' and 'XMP ').
        /// If there are more such chunks, readers MAY ignore all except the first one.
        /// Also, a file may possibly contain both 'EXIF' and 'XMP ' chunks.
        /// </summary>
        /// <param name="features">The webp features.</param>
        private void ParseOptionalChunks(WebPFeatures features)
        {
            if (this.IgnoreMetadata || (features.ExifProfile is false && features.XmpMetaData is false))
            {
                return;
            }

            while (this.currentStream.Position < this.currentStream.Length)
            {
                // Read chunk header.
                WebPChunkType chunkType = this.ReadChunkType();
                uint chunkLength = this.ReadChunkSize();

                if (chunkType is WebPChunkType.Exif)
                {
                    var exifData = new byte[chunkLength];
                    this.currentStream.Read(exifData, 0, (int)chunkLength);
                    this.Metadata.ExifProfile = new ExifProfile(exifData);
                }
                else
                {
                    // Skip XMP chunk data for now.
                    this.currentStream.Skip((int)chunkLength);
                }
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
            if (this.currentStream.Read(this.buffer, 0, 4) is 4)
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
            if (this.currentStream.Read(this.buffer, 0, 4) is 4)
            {
                uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);
                return (chunkSize % 2 is 0) ? chunkSize : chunkSize + 1;
            }

            throw new ImageFormatException("Invalid WebP data.");
        }
    }
}
