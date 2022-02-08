// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
    internal sealed class WebpDecoderCore : IImageDecoderInternals
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
            this.Metadata = new ImageMetadata();
            this.currentStream = stream;

            uint fileSize = this.ReadImageHeader();

            using (this.webImageInfo = this.ReadVp8Info())
            {
                if (this.webImageInfo.Features is { Animation: true })
                {
                    var animationDecoder = new WebpAnimationDecoder(this.memoryAllocator, this.Configuration);
                    return animationDecoder.Decode<TPixel>(stream, this.webImageInfo.Features, this.webImageInfo.Width, this.webImageInfo.Height, fileSize);
                }

                var image = new Image<TPixel>(this.Configuration, (int)this.webImageInfo.Width, (int)this.webImageInfo.Height, this.Metadata);
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
                this.ReadOptionalMetadata();

                return image;
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
            uint fileSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);

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
                            bool isAnimationChunk = this.ParseOptionalExtendedChunks(chunkType, features);
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
                    return new WebpImageInfo(); // This return will never be reached, because throw helper will throw an exception.
            }
        }

        /// <summary>
        /// Parses optional VP8X chunks, which can be ICCP, ANIM or ALPH chunks.
        /// </summary>
        /// <param name="chunkType">The chunk type.</param>
        /// <param name="features">The webp image features.</param>
        /// <returns>true, if animation chunk was found.</returns>
        private bool ParseOptionalExtendedChunks(WebpChunkType chunkType, WebpFeatures features)
        {
            int bytesRead;
            switch (chunkType)
            {
                case WebpChunkType.Iccp:
                    uint iccpChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
                    if (this.IgnoreMetadata)
                    {
                        this.currentStream.Skip((int)iccpChunkSize);
                    }
                    else
                    {
                        byte[] iccpData = new byte[iccpChunkSize];
                        bytesRead = this.currentStream.Read(iccpData, 0, (int)iccpChunkSize);
                        if (bytesRead != iccpChunkSize)
                        {
                            WebpThrowHelper.ThrowImageFormatException("Could not read enough data for ICCP profile");
                        }

                        var profile = new IccProfile(iccpData);
                        if (profile.CheckIsValid())
                        {
                            this.Metadata.IccProfile = profile;
                        }
                    }

                    break;

                case WebpChunkType.Exif:
                    uint exifChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
                    if (this.IgnoreMetadata)
                    {
                        this.currentStream.Skip((int)exifChunkSize);
                    }
                    else
                    {
                        byte[] exifData = new byte[exifChunkSize];
                        bytesRead = this.currentStream.Read(exifData, 0, (int)exifChunkSize);
                        if (bytesRead != exifChunkSize)
                        {
                            WebpThrowHelper.ThrowImageFormatException("Could not read enough data for the EXIF profile");
                        }

                        var profile = new ExifProfile(exifData);
                        this.Metadata.ExifProfile = profile;
                    }

                    break;

                case WebpChunkType.Xmp:
                    uint xmpChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
                    if (this.IgnoreMetadata)
                    {
                        this.currentStream.Skip((int)xmpChunkSize);
                    }
                    else
                    {
                        byte[] xmpData = new byte[xmpChunkSize];
                        bytesRead = this.currentStream.Read(xmpData, 0, (int)xmpChunkSize);
                        if (bytesRead != xmpChunkSize)
                        {
                            WebpThrowHelper.ThrowImageFormatException("Could not read enough data for the XMP profile");
                        }

                        var profile = new XmpProfile(xmpData);
                        this.Metadata.XmpProfile = profile;
                    }

                    break;

                case WebpChunkType.AnimationParameter:
                    features.Animation = true;
                    uint animationChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
                    byte blue = (byte)this.currentStream.ReadByte();
                    byte green = (byte)this.currentStream.ReadByte();
                    byte red = (byte)this.currentStream.ReadByte();
                    byte alpha = (byte)this.currentStream.ReadByte();
                    features.AnimationBackgroundColor = new Color(new Rgba32(red, green, blue, alpha));
                    this.currentStream.Read(this.buffer, 0, 2);
                    features.AnimationLoopCount = BinaryPrimitives.ReadUInt16LittleEndian(this.buffer);
                    return true;

                case WebpChunkType.Alpha:
                    uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(this.currentStream, this.buffer);
                    features.AlphaChunkHeader = (byte)this.currentStream.ReadByte();
                    int alphaDataSize = (int)(alphaChunkSize - 1);
                    features.AlphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);
                    this.currentStream.Read(features.AlphaData.Memory.Span, 0, alphaDataSize);
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
        private void ReadOptionalMetadata()
        {
            if (!this.IgnoreMetadata && this.webImageInfo.Features != null && (this.webImageInfo.Features.ExifProfile || this.webImageInfo.Features.XmpMetaData))
            {
                // The spec states, that the EXIF and XMP should come after the image data, but it seems some encoders store them
                // in the VP8X chunk before the image data. Make sure there is still data to read here.
                if (this.currentStream.Position == this.currentStream.Length)
                {
                    return;
                }

                WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(this.currentStream, this.buffer);
                WebpChunkParsingUtils.ParseOptionalChunks(this.currentStream, chunkType, this.Metadata, this.IgnoreMetadata, this.buffer);
            }
        }
    }
}
