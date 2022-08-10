// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
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
    internal sealed class WebpDecoderCore : IImageDecoderInternals, IDisposable
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// Used for allocating memory during the decoding operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream currentStream;

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
            this.DecodingMode = options.DecodingMode;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.IgnoreMetadata = options.IgnoreMetadata;
        }

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the decoding mode for multi-frame images.
        /// </summary>
        public FrameDecodingMode DecodingMode { get; }

        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the <see cref="ImageMetadata"/> decoded by this decoder instance.
        /// </summary>
        public ImageMetadata Metadata { get; private set; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new((int)this.webImageInfo.Width, (int)this.webImageInfo.Height);

        /// <summary>
        /// Gets or sets the alpha data, if an ALPH chunk is present.
        /// </summary>
        public IMemoryOwner<byte> AlphaData { get; set; }

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
                        using var animationDecoder = new WebpAnimationDecoder(this.memoryAllocator, this.Configuration, this.DecodingMode);
                        return animationDecoder.Decode<TPixel>(stream, this.webImageInfo.Features, this.webImageInfo.Width, this.webImageInfo.Height, fileSize);
                    }

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
                        lossyDecoder.Decode(pixels, image.Width, image.Height, this.webImageInfo, this.AlphaData);
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
            using (this.webImageInfo = this.ReadVp8Info(true))
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
            uint fileSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);

            // Skip 'WEBP' from the header.
            this.currentStream.Skip(4);

            return fileSize;
        }

        /// <summary>
        /// Reads information present in the image header, about the image content and how to decode the image.
        /// </summary>
        /// <param name="ignoreAlpha">For identify, the alpha data should not be read.</param>
        /// <returns>Information about the webp image.</returns>
        private WebpImageInfo ReadVp8Info(bool ignoreAlpha = false)
        {
            this.Metadata = new ImageMetadata();
            this.webpMetadata = this.Metadata.GetFormatMetadata(WebpFormat.Instance);

            WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(this.currentStream, this.buffer);

            var features = new WebpFeatures();
            switch (chunkType)
            {
                case WebpChunkType.Vp8:
                    this.webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                    return WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, this.currentStream, this.buffer, features);
                case WebpChunkType.Vp8L:
                    this.webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                    return WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, this.currentStream, this.buffer, features);
                case WebpChunkType.Vp8X:
                    WebpImageInfo webpInfos = WebpChunkParsingUtils.ReadVp8XHeader(this.currentStream, this.buffer, features);
                    while (this.currentStream.Position < this.currentStream.Length)
                    {
                        chunkType = WebpChunkParsingUtils.ReadChunkType(this.currentStream, this.buffer);
                        if (chunkType == WebpChunkType.Vp8)
                        {
                            this.webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                            webpInfos = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, this.currentStream, this.buffer, features);
                        }
                        else if (chunkType == WebpChunkType.Vp8L)
                        {
                            this.webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                            webpInfos = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, this.currentStream, this.buffer, features);
                        }
                        else if (WebpChunkParsingUtils.IsOptionalVp8XChunk(chunkType))
                        {
                            bool isAnimationChunk = this.ParseOptionalExtendedChunks(chunkType, features, ignoreAlpha);
                            if (isAnimationChunk)
                            {
                                return webpInfos;
                            }
                        }
                        else
                        {
                            WebpThrowHelper.ThrowImageFormatException("Unexpected chunk followed VP8X header");
                        }
                    }

                    return webpInfos;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");
                    return
                        new WebpImageInfo(); // this return will never be reached, because throw helper will throw an exception.
            }
        }

        /// <summary>
        /// Parses optional VP8X chunks, which can be ICCP, XMP, ANIM or ALPH chunks.
        /// </summary>
        /// <param name="chunkType">The chunk type.</param>
        /// <param name="features">The webp image features.</param>
        /// <param name="ignoreAlpha">For identify, the alpha data should not be read.</param>
        /// <returns>true, if its a alpha chunk.</returns>
        private bool ParseOptionalExtendedChunks(WebpChunkType chunkType, WebpFeatures features, bool ignoreAlpha)
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

                case WebpChunkType.AnimationParameter:
                    this.ReadAnimationParameters(features);
                    return true;

                case WebpChunkType.Alpha:
                    this.ReadAlphaData(features, ignoreAlpha);
                    break;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Unexpected chunk followed VP8X header");
                    break;
            }

            return false;
        }

        /// <summary>
        /// Reads the optional metadata EXIF of XMP profiles, which can follow the image data.
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
        /// Reads the animation parameters chunk from the stream.
        /// </summary>
        /// <param name="features">The webp features.</param>
        private void ReadAnimationParameters(WebpFeatures features)
        {
            features.Animation = true;
            uint animationChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
            byte blue = (byte)this.currentStream.ReadByte();
            byte green = (byte)this.currentStream.ReadByte();
            byte red = (byte)this.currentStream.ReadByte();
            byte alpha = (byte)this.currentStream.ReadByte();
            features.AnimationBackgroundColor = new Color(new Rgba32(red, green, blue, alpha));
            int bytesRead = this.currentStream.Read(this.buffer, 0, 2);
            if (bytesRead != 2)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the animation loop count");
            }

            features.AnimationLoopCount = BinaryPrimitives.ReadUInt16LittleEndian(this.buffer);
        }

        /// <summary>
        /// Reads the alpha data chunk data from the stream.
        /// </summary>
        /// <param name="features">The features.</param>
        /// <param name="ignoreAlpha">if set to true, skips the chunk data.</param>
        private void ReadAlphaData(WebpFeatures features, bool ignoreAlpha)
        {
            uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
            if (ignoreAlpha)
            {
                this.currentStream.Skip((int)alphaChunkSize);
                return;
            }

            features.AlphaChunkHeader = (byte)this.currentStream.ReadByte();
            int alphaDataSize = (int)(alphaChunkSize - 1);
            this.AlphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);
            Span<byte> alphaData = this.AlphaData.GetSpan();
            int bytesRead = this.currentStream.Read(alphaData, 0, alphaDataSize);
            if (bytesRead != alphaDataSize)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the alpha data from the stream");
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

        /// <inheritdoc/>
        public void Dispose() => this.AlphaData?.Dispose();
    }
}
