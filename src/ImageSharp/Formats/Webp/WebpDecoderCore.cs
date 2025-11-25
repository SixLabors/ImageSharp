// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Performs the webp decoding operation.
    /// </summary>
    internal sealed class WebpDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

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
        private WebpMetadata webpMetadata;

        /// <summary>
        /// Information about the webp image.
        /// </summary>
        private WebpImageInfo webImageInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebpDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public WebpDecoderCore(Configuration configuration, IWebpDecoderOptions options)
        {
            this.Configuration = configuration;
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

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new((int)this.webImageInfo.Width, (int)this.webImageInfo.Height);

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> image = null;
            try
            {
                this.Metadata = new ImageMetadata();
                this.currentStream = stream;

                uint fileSize = this.ReadImageHeader();

                using (this.webImageInfo = this.ReadVp8Info())
                {
                    if (this.webImageInfo.Features is { Animation: true })
                    {
                        WebpThrowHelper.ThrowNotSupportedException("Animations are not supported");
                    }

                    image = new Image<TPixel>(this.Configuration, (int)this.webImageInfo.Width, (int)this.webImageInfo.Height, this.Metadata);
                    Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
                    if (this.webImageInfo.IsLossless)
                    {
                        var losslessDecoder = new WebpLosslessDecoder(this.webImageInfo.Vp8LBitReader, this.memoryAllocator, this.Configuration);
                        losslessDecoder.Decode(pixels, image.Width, image.Height);
                    }
                    else
                    {
                        var lossyDecoder = new WebpLossyDecoder(this.webImageInfo.Vp8BitReader, this.memoryAllocator, this.Configuration);
                        lossyDecoder.Decode(pixels, image.Width, image.Height, this.webImageInfo);
                    }

                    // There can be optional chunks after the image data, like EXIF and XMP.
                    if (this.webImageInfo.Features != null)
                    {
                        this.ParseOptionalChunks(this.webImageInfo.Features);
                    }

                    return image;
                }
            }
            catch
            {
                image?.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.currentStream = stream;

            this.ReadImageHeader();
            using (this.webImageInfo = this.ReadVp8Info())
            {
                return new ImageInfo(new PixelTypeInfo((int)this.webImageInfo.BitsPerPixel), (int)this.webImageInfo.Width, (int)this.webImageInfo.Height, this.Metadata);
            }
        }

        /// <summary>
        /// Reads and skips over the image header.
        /// </summary>
        /// <returns>The file size in bytes.</returns>
        private uint ReadImageHeader()
        {
            // Skip FourCC header, we already know its a RIFF file at this point.
            this.currentStream.Skip(4);

            // Read file size.
            // The size of the file in bytes starting at offset 8.
            // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
            uint fileSize = this.ReadChunkSize();

            // Skip 'WEBP' from the header.
            this.currentStream.Skip(4);

            return fileSize;
        }

        /// <summary>
        /// Reads information present in the image header, about the image content and how to decode the image.
        /// </summary>
        /// <returns>Information about the webp image.</returns>
        private WebpImageInfo ReadVp8Info()
        {
            this.Metadata = new ImageMetadata();
            this.webpMetadata = this.Metadata.GetFormatMetadata(WebpFormat.Instance);

            WebpChunkType chunkType = this.ReadChunkType();

            switch (chunkType)
            {
                case WebpChunkType.Vp8:
                    return this.ReadVp8Header();
                case WebpChunkType.Vp8L:
                    return this.ReadVp8LHeader();
                case WebpChunkType.Vp8X:
                    return this.ReadVp8XHeader();
                default:
                    WebpThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");
                    return new WebpImageInfo(); // this return will never be reached, because throw helper will throw an exception.
            }
        }

        /// <summary>
        /// Reads an the extended webp file header. An extended file header consists of:
        /// - A 'VP8X' chunk with information about features used in the file.
        /// - An optional 'ICCP' chunk with color profile.
        /// - An optional 'XMP' chunk with metadata.
        /// - An optional 'ANIM' chunk with animation control data.
        /// - An optional 'ALPH' chunk with alpha channel data.
        /// After the image header, image data will follow. After that optional image metadata chunks (EXIF and XMP) can follow.
        /// </summary>
        /// <returns>Information about this webp image.</returns>
        private WebpImageInfo ReadVp8XHeader()
        {
            var features = new WebpFeatures();
            uint fileSize = this.ReadChunkSize();

            // The first byte contains information about the image features used.
            int imageFeatures = this.currentStream.ReadByte();
            if (imageFeatures == -1)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("VP8X header doe not contain enough data");
            }

            // The first two bit of it are reserved and should be 0.
            if (imageFeatures >> 6 != 0)
            {
                WebpThrowHelper.ThrowImageFormatException("first two bits of the VP8X header are expected to be zero");
            }

            // If bit 3 is set, a ICC Profile Chunk should be present.
            features.IccProfile = (imageFeatures & (1 << 5)) != 0;

            // If bit 4 is set, any of the frames of the image contain transparency information ("alpha" chunk).
            features.Alpha = (imageFeatures & (1 << 4)) != 0;

            // If bit 5 is set, a EXIF metadata should be present.
            features.ExifProfile = (imageFeatures & (1 << 3)) != 0;

            // If bit 6 is set, XMP metadata should be present.
            features.XmpMetaData = (imageFeatures & (1 << 2)) != 0;

            // If bit 7 is set, animation should be present.
            features.Animation = (imageFeatures & (1 << 1)) != 0;

            // 3 reserved bytes should follow which are supposed to be zero.
            int bytesRead = this.currentStream.Read(this.buffer, 0, 3);
            if (bytesRead != 3)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("VP8X header does not contain enough data");
            }

            if (this.buffer[0] != 0 || this.buffer[1] != 0 || this.buffer[2] != 0)
            {
                WebpThrowHelper.ThrowImageFormatException("reserved bytes should be zero");
            }

            // 3 bytes for the width.
            bytesRead = this.currentStream.Read(this.buffer, 0, 3);
            if (bytesRead != 3)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("VP8 header does not contain enough data to read the width");
            }

            this.buffer[3] = 0;
            uint width = (uint)BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            // 3 bytes for the height.
            bytesRead = this.currentStream.Read(this.buffer, 0, 3);
            if (bytesRead != 3)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("VP8 header does not contain enough data to read the height");
            }

            this.buffer[3] = 0;
            uint height = (uint)BinaryPrimitives.ReadInt32LittleEndian(this.buffer) + 1;

            // Read all the chunks in the order they occur.
            var info = new WebpImageInfo();
            while (this.currentStream.Position < this.currentStream.Length)
            {
                WebpChunkType chunkType = this.ReadChunkType();
                if (chunkType == WebpChunkType.Vp8)
                {
                    info = this.ReadVp8Header(features);
                }
                else if (chunkType == WebpChunkType.Vp8L)
                {
                    info = this.ReadVp8LHeader(features);
                }
                else if (IsOptionalVp8XChunk(chunkType))
                {
                    this.ParseOptionalExtendedChunks(chunkType, features);
                }
                else
                {
                    // Ignore unknown chunks.
                    uint chunkSize = this.ReadChunkSize();
                    this.currentStream.Skip((int)chunkSize);
                }
            }

            if (features.Animation)
            {
                // TODO: Animations are not yet supported.
                return new WebpImageInfo() { Width = width, Height = height, Features = features };
            }

            return info;
        }

        /// <summary>
        /// Reads the header of a lossy webp image.
        /// </summary>
        /// <param name="features">Webp features.</param>
        /// <returns>Information about this webp image.</returns>
        private WebpImageInfo ReadVp8Header(WebpFeatures features = null)
        {
            this.webpMetadata.FileFormat = WebpFileFormatType.Lossy;

            // VP8 data size (not including this 4 bytes).
            int bytesRead = this.currentStream.Read(this.buffer, 0, 4);
            if (bytesRead != 4)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 data size");
            }

            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

            // remaining counts the available image data payload.
            uint remaining = dataSize;

            // Paragraph 9.1 https://tools.ietf.org/html/rfc6386#page-30
            // Frame tag that contains four fields:
            // - A 1-bit frame type (0 for key frames, 1 for interframes).
            // - A 3-bit version number.
            // - A 1-bit show_frame flag.
            // - A 19-bit field containing the size of the first data partition in bytes.
            bytesRead = this.currentStream.Read(this.buffer, 0, 3);
            if (bytesRead != 3)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 frame tag");
            }

            uint frameTag = (uint)(this.buffer[0] | (this.buffer[1] << 8) | (this.buffer[2] << 16));
            remaining -= 3;
            bool isNoKeyFrame = (frameTag & 0x1) == 1;
            if (isNoKeyFrame)
            {
                WebpThrowHelper.ThrowImageFormatException("VP8 header indicates the image is not a key frame");
            }

            uint version = (frameTag >> 1) & 0x7;
            if (version > 3)
            {
                WebpThrowHelper.ThrowImageFormatException($"VP8 header indicates unknown profile {version}");
            }

            bool invisibleFrame = ((frameTag >> 4) & 0x1) == 0;
            if (invisibleFrame)
            {
                WebpThrowHelper.ThrowImageFormatException("VP8 header indicates that the first frame is invisible");
            }

            uint partitionLength = frameTag >> 5;
            if (partitionLength > dataSize)
            {
                WebpThrowHelper.ThrowImageFormatException("VP8 header contains inconsistent size information");
            }

            // Check for VP8 magic bytes.
            bytesRead = this.currentStream.Read(this.buffer, 0, 3);
            if (bytesRead != 3)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 magic bytes");
            }

            if (!this.buffer.AsSpan(0, 3).SequenceEqual(WebpConstants.Vp8HeaderMagicBytes))
            {
                WebpThrowHelper.ThrowImageFormatException("VP8 magic bytes not found");
            }

            bytesRead = this.currentStream.Read(this.buffer, 0, 4);
            if (bytesRead != 4)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("VP8 header does not contain enough data to read the image width and height");
            }

            uint tmp = (uint)BinaryPrimitives.ReadInt16LittleEndian(this.buffer);
            uint width = tmp & 0x3fff;
            sbyte xScale = (sbyte)(tmp >> 6);
            tmp = (uint)BinaryPrimitives.ReadInt16LittleEndian(this.buffer.AsSpan(2));
            uint height = tmp & 0x3fff;
            sbyte yScale = (sbyte)(tmp >> 6);
            remaining -= 7;
            if (width == 0 || height == 0)
            {
                WebpThrowHelper.ThrowImageFormatException("width or height can not be zero");
            }

            if (partitionLength > remaining)
            {
                WebpThrowHelper.ThrowImageFormatException("bad partition length");
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
                partitionLength)
            {
                Remaining = remaining
            };

            return new WebpImageInfo()
            {
                Width = width,
                Height = height,
                XScale = xScale,
                YScale = yScale,
                BitsPerPixel = features?.Alpha == true ? WebpBitsPerPixel.Pixel32 : WebpBitsPerPixel.Pixel24,
                IsLossless = false,
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
        private WebpImageInfo ReadVp8LHeader(WebpFeatures features = null)
        {
            this.webpMetadata.FileFormat = WebpFileFormatType.Lossless;

            // VP8 data size.
            uint imageDataSize = this.ReadChunkSize();

            var bitReader = new Vp8LBitReader(this.currentStream, imageDataSize, this.memoryAllocator);

            // One byte signature, should be 0x2f.
            uint signature = bitReader.ReadValue(8);
            if (signature != WebpConstants.Vp8LHeaderMagicByte)
            {
                WebpThrowHelper.ThrowImageFormatException("Invalid VP8L signature");
            }

            // The first 28 bits of the bitstream specify the width and height of the image.
            uint width = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;
            uint height = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;
            if (width == 0 || height == 0)
            {
                WebpThrowHelper.ThrowImageFormatException("invalid width or height read");
            }

            // The alphaIsUsed flag should be set to 0 when all alpha values are 255 in the picture, and 1 otherwise.
            // TODO: this flag value is not used yet
            bool alphaIsUsed = bitReader.ReadBit();

            // The next 3 bits are the version. The version number is a 3 bit code that must be set to 0.
            // Any other value should be treated as an error.
            uint version = bitReader.ReadValue(WebpConstants.Vp8LVersionBits);
            if (version != 0)
            {
                WebpThrowHelper.ThrowNotSupportedException($"Unexpected version number {version} found in VP8L header");
            }

            return new WebpImageInfo()
            {
                Width = width,
                Height = height,
                BitsPerPixel = WebpBitsPerPixel.Pixel32,
                IsLossless = true,
                Features = features,
                Vp8LBitReader = bitReader
            };
        }

        /// <summary>
        /// Parses optional VP8X chunks, which can be ICCP, XMP, ANIM or ALPH chunks.
        /// </summary>
        /// <param name="chunkType">The chunk type.</param>
        /// <param name="features">The webp image features.</param>
        private void ParseOptionalExtendedChunks(WebpChunkType chunkType, WebpFeatures features)
        {
            switch (chunkType)
            {
                case WebpChunkType.Iccp:
                    this.ReadIccProfile();
                    break;

                case WebpChunkType.Exif:
                    this.ReadExifProfile();
                    break;

                case WebpChunkType.Xmp:
                    this.ReadXmpProfile();
                    break;

                case WebpChunkType.Animation:
                    // TODO: Decoding animation is not implemented yet.
                    break;

                case WebpChunkType.Alpha:
                    uint alphaChunkSize = this.ReadChunkSize();
                    features.AlphaChunkHeader = (byte)this.currentStream.ReadByte();
                    int alphaDataSize = (int)(alphaChunkSize - 1);
                    features.AlphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);
                    int bytesRead = this.currentStream.Read(features.AlphaData.Memory.Span, 0, alphaDataSize);
                    if (bytesRead != alphaDataSize)
                    {
                        WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the alpha chunk");
                    }

                    break;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Unexpected chunk followed VP8X header");
                    break;
            }
        }

        /// <summary>
        /// Parses optional metadata chunks. There SHOULD be at most one chunk of each type ('EXIF' and 'XMP ').
        /// If there are more such chunks, readers MAY ignore all except the first one.
        /// Also, a file may possibly contain both 'EXIF' and 'XMP ' chunks.
        /// </summary>
        /// <param name="features">The webp features.</param>
        private void ParseOptionalChunks(WebpFeatures features)
        {
            if (this.IgnoreMetadata || (features.ExifProfile == false && features.XmpMetaData == false))
            {
                return;
            }

            long streamLength = this.currentStream.Length;
            while (this.currentStream.Position < streamLength)
            {
                // Read chunk header.
                WebpChunkType chunkType = this.ReadChunkType();
                if (chunkType == WebpChunkType.Exif && this.Metadata.ExifProfile == null)
                {
                    this.ReadExifProfile();
                }
                else if (chunkType == WebpChunkType.Xmp && this.Metadata.XmpProfile == null)
                {
                    this.ReadXmpProfile();
                }
                else
                {
                    // Skip duplicate XMP or EXIF chunk.
                    uint chunkLength = this.ReadChunkSize();
                    this.currentStream.Skip((int)chunkLength);
                }
            }
        }

        /// <summary>
        /// Reads the EXIF profile from the stream.
        /// </summary>
        private void ReadExifProfile()
        {
            uint exifChunkSize = this.ReadChunkSize();
            if (this.IgnoreMetadata)
            {
                this.currentStream.Skip((int)exifChunkSize);
            }
            else
            {
                byte[] exifData = new byte[exifChunkSize];
                int bytesRead = this.currentStream.Read(exifData, 0, (int)exifChunkSize);
                if (bytesRead != exifChunkSize)
                {
                    // Ignore invalid chunk.
                    return;
                }

                var profile = new ExifProfile(exifData);
                this.Metadata.ExifProfile = profile;
            }
        }

        /// <summary>
        /// Reads the XMP profile the stream.
        /// </summary>
        private void ReadXmpProfile()
        {
            uint xmpChunkSize = this.ReadChunkSize();
            if (this.IgnoreMetadata)
            {
                this.currentStream.Skip((int)xmpChunkSize);
            }
            else
            {
                byte[] xmpData = new byte[xmpChunkSize];
                int bytesRead = this.currentStream.Read(xmpData, 0, (int)xmpChunkSize);
                if (bytesRead != xmpChunkSize)
                {
                    // Ignore invalid chunk.
                    return;
                }

                var profile = new XmpProfile(xmpData);
                this.Metadata.XmpProfile = profile;
            }
        }

        /// <summary>
        /// Reads the ICCP chunk from the stream.
        /// </summary>
        private void ReadIccProfile()
        {
            uint iccpChunkSize = this.ReadChunkSize();
            if (this.IgnoreMetadata)
            {
                this.currentStream.Skip((int)iccpChunkSize);
            }
            else
            {
                byte[] iccpData = new byte[iccpChunkSize];
                int bytesRead = this.currentStream.Read(iccpData, 0, (int)iccpChunkSize);
                if (bytesRead != iccpChunkSize)
                {
                    WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the iccp chunk");
                }

                var profile = new IccProfile(iccpData);
                if (profile.CheckIsValid())
                {
                    this.Metadata.IccProfile = profile;
                }
            }
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private WebpChunkType ReadChunkType()
        {
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                var chunkType = (WebpChunkType)BinaryPrimitives.ReadUInt32BigEndian(this.buffer);
                return chunkType;
            }

            throw new ImageFormatException("Invalid Webp data.");
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

            throw new ImageFormatException("Invalid Webp data.");
        }

        /// <summary>
        /// Determines if the chunk type is an optional VP8X chunk.
        /// </summary>
        /// <param name="chunkType">The chunk type.</param>
        /// <returns>True, if its an optional chunk type.</returns>
        private static bool IsOptionalVp8XChunk(WebpChunkType chunkType)
        {
            switch (chunkType)
            {
                case WebpChunkType.Alpha:
                case WebpChunkType.Iccp:
                case WebpChunkType.Exif:
                case WebpChunkType.Xmp:
                    return true;
                case WebpChunkType.AnimationParameter:
                case WebpChunkType.Animation:
                    WebpThrowHelper.ThrowNotSupportedException("Animated webp are not yet supported.");
                    return false;
                default:
                    return false;
            }
        }
    }
}
