// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Performs the webp decoding operation.
/// </summary>
internal sealed class WebpDecoderCore : ImageDecoderCore, IDisposable
{
    /// <summary>
    /// General configuration options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// A value indicating whether the metadata should be ignored when the image is being decoded.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// The maximum number of frames to decode. Inclusive.
    /// </summary>
    private readonly uint maxFrames;

    /// <summary>
    /// Gets or sets the alpha data, if an ALPH chunk is present.
    /// </summary>
    private IMemoryOwner<byte>? alphaData;

    /// <summary>
    /// Used for allocating memory during the decoding operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// Information about the webp image.
    /// </summary>
    private WebpImageInfo? webImageInfo;

    /// <summary>
    /// The flag to decide how to handle the background color in the Animation Chunk.
    /// </summary>
    private readonly BackgroundColorHandling backgroundColorHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public WebpDecoderCore(WebpDecoderOptions options)
        : base(options.GeneralOptions)
    {
        this.backgroundColorHandling = options.BackgroundColorHandling;
        this.configuration = options.GeneralOptions.Configuration;
        this.skipMetadata = options.GeneralOptions.SkipMetadata;
        this.maxFrames = options.GeneralOptions.MaxFrames;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        Image<TPixel>? image = null;
        try
        {
            ImageMetadata metadata = new();
            Span<byte> buffer = stackalloc byte[4];

            uint fileSize = ReadImageHeader(stream, buffer);

            using (this.webImageInfo = this.ReadVp8Info(stream, metadata))
            {
                if (this.webImageInfo.Features is { Animation: true })
                {
                    using WebpAnimationDecoder animationDecoder = new(
                        this.memoryAllocator,
                        this.configuration,
                        this.maxFrames,
                        this.skipMetadata,
                        this.backgroundColorHandling);

                    return animationDecoder.Decode<TPixel>(stream, this.webImageInfo.Features, this.webImageInfo.Width, this.webImageInfo.Height, fileSize);
                }

                image = new Image<TPixel>(this.configuration, (int)this.webImageInfo.Width, (int)this.webImageInfo.Height, metadata);
                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
                if (this.webImageInfo.IsLossless)
                {
                    WebpLosslessDecoder losslessDecoder = new(
                        this.webImageInfo.Vp8LBitReader,
                        this.memoryAllocator,
                        this.configuration);

                    losslessDecoder.Decode(pixels, image.Width, image.Height);
                }
                else
                {
                    WebpLossyDecoder lossyDecoder = new(
                        this.webImageInfo.Vp8BitReader,
                        this.memoryAllocator,
                        this.configuration);

                    lossyDecoder.Decode(pixels, image.Width, image.Height, this.webImageInfo, this.alphaData);
                }

                // There can be optional chunks after the image data, like EXIF and XMP.
                if (this.webImageInfo.Features != null)
                {
                    this.ParseOptionalChunks(stream, metadata, this.webImageInfo.Features, buffer);
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
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        uint fileSize = ReadImageHeader(stream, stackalloc byte[4]);
        ImageMetadata metadata = new();

        using (this.webImageInfo = this.ReadVp8Info(stream, metadata, true))
        {
            if (this.webImageInfo.Features is { Animation: true })
            {
                using WebpAnimationDecoder animationDecoder = new(
                    this.memoryAllocator,
                    this.configuration,
                    this.maxFrames,
                    this.skipMetadata,
                    this.backgroundColorHandling);

                return animationDecoder.Identify(
                    stream,
                    (int)this.webImageInfo.BitsPerPixel,
                    this.webImageInfo.Features,
                    this.webImageInfo.Width,
                    this.webImageInfo.Height,
                    fileSize);
            }

            return new ImageInfo(
                new PixelTypeInfo((int)this.webImageInfo.BitsPerPixel),
                new Size((int)this.webImageInfo.Width, (int)this.webImageInfo.Height),
                metadata);
        }
    }

    /// <summary>
    /// Reads and skips over the image header.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <returns>The file size in bytes.</returns>
    private static uint ReadImageHeader(BufferedReadStream stream, Span<byte> buffer)
    {
        // Skip FourCC header, we already know its a RIFF file at this point.
        stream.Skip(4);

        // Read file size.
        // The size of the file in bytes starting at offset 8.
        // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
        uint fileSize = WebpChunkParsingUtils.ReadChunkSize(stream, buffer);

        // Skip 'WEBP' from the header.
        stream.Skip(4);

        return fileSize;
    }

    /// <summary>
    /// Reads information present in the image header, about the image content and how to decode the image.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreAlpha">For identify, the alpha data should not be read.</param>
    /// <returns>Information about the webp image.</returns>
    private WebpImageInfo ReadVp8Info(BufferedReadStream stream, ImageMetadata metadata, bool ignoreAlpha = false)
    {
        WebpMetadata webpMetadata = metadata.GetFormatMetadata(WebpFormat.Instance);

        Span<byte> buffer = stackalloc byte[4];
        WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);

        WebpImageInfo webpImageInfo;
        WebpFeatures features = new();
        switch (chunkType)
        {
            case WebpChunkType.Vp8:
                webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                webpImageInfo = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, buffer, features);
                break;
            case WebpChunkType.Vp8L:
                webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                webpImageInfo = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, buffer, features);
                break;
            case WebpChunkType.Vp8X:
                webpImageInfo = WebpChunkParsingUtils.ReadVp8XHeader(stream, buffer, features);
                while (stream.Position < stream.Length)
                {
                    chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
                    if (chunkType == WebpChunkType.Vp8)
                    {
                        webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                        webpImageInfo = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, buffer, features);
                    }
                    else if (chunkType == WebpChunkType.Vp8L)
                    {
                        webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                        webpImageInfo = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, buffer, features);
                    }
                    else if (WebpChunkParsingUtils.IsOptionalVp8XChunk(chunkType))
                    {
                        // ANIM chunks appear before EXIF and XMP chunks.
                        // Return after parsing an ANIM chunk - The animated decoder will handle the rest.
                        bool isAnimationChunk = this.ParseOptionalExtendedChunks(stream, metadata, chunkType, features, ignoreAlpha, buffer);
                        if (isAnimationChunk)
                        {
                            break;
                        }
                    }
                    else
                    {
                        // Ignore unknown chunks.
                        uint chunkSize = ReadChunkSize(stream, buffer, false);
                        stream.Skip((int)chunkSize);
                    }
                }

                break;
            default:
                WebpThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");

                // This return will never be reached, because throw helper will throw an exception.
                webpImageInfo = new();
                break;
        }

        this.Dimensions = new Size((int)webpImageInfo.Width, (int)webpImageInfo.Height);
        return webpImageInfo;
    }

    /// <summary>
    /// Parses optional VP8X chunks, which can be ICCP, XMP, ANIM or ALPH chunks.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="chunkType">The chunk type.</param>
    /// <param name="features">The webp image features.</param>
    /// <param name="ignoreAlpha">For identify, the alpha data should not be read.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <returns>true, if its a alpha chunk.</returns>
    private bool ParseOptionalExtendedChunks(
        BufferedReadStream stream,
        ImageMetadata metadata,
        WebpChunkType chunkType,
        WebpFeatures features,
        bool ignoreAlpha,
        Span<byte> buffer)
    {
        switch (chunkType)
        {
            case WebpChunkType.Iccp:
                this.ReadIccProfile(stream, metadata, buffer);
                break;

            case WebpChunkType.Exif:
                this.ReadExifProfile(stream, metadata, buffer);
                break;

            case WebpChunkType.Xmp:
                this.ReadXmpProfile(stream, metadata, buffer);
                break;

            case WebpChunkType.AnimationParameter:
                ReadAnimationParameters(stream, features, buffer);
                return true;

            case WebpChunkType.Alpha:
                this.ReadAlphaData(stream, features, ignoreAlpha, buffer);
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
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ParseOptionalChunks(BufferedReadStream stream, ImageMetadata metadata, WebpFeatures features, Span<byte> buffer)
    {
        if (this.skipMetadata || (!features.ExifProfile && !features.XmpMetaData))
        {
            return;
        }

        long streamLength = stream.Length;
        while (stream.Position < streamLength)
        {
            // Read chunk header.
            WebpChunkType chunkType = ReadChunkType(stream, buffer);
            if (chunkType == WebpChunkType.Exif && metadata.ExifProfile == null)
            {
                this.ReadExifProfile(stream, metadata, buffer);
            }
            else if (chunkType == WebpChunkType.Xmp && metadata.XmpProfile == null)
            {
                this.ReadXmpProfile(stream, metadata, buffer);
            }
            else
            {
                // Skip duplicate XMP or EXIF chunk.
                uint chunkLength = ReadChunkSize(stream, buffer);
                stream.Skip((int)chunkLength);
            }
        }
    }

    /// <summary>
    /// Reads the EXIF profile from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ReadExifProfile(BufferedReadStream stream, ImageMetadata metadata, Span<byte> buffer)
    {
        uint exifChunkSize = ReadChunkSize(stream, buffer);
        if (this.skipMetadata)
        {
            stream.Skip((int)exifChunkSize);
        }
        else
        {
            byte[] exifData = new byte[exifChunkSize];
            int bytesRead = stream.Read(exifData, 0, (int)exifChunkSize);
            if (bytesRead != exifChunkSize)
            {
                // Ignore invalid chunk.
                return;
            }

            ExifProfile exifProfile = new(exifData);

            // Set the resolution from the metadata.
            double horizontalValue = GetExifResolutionValue(exifProfile, ExifTag.XResolution);
            double verticalValue = GetExifResolutionValue(exifProfile, ExifTag.YResolution);

            if (horizontalValue > 0 && verticalValue > 0)
            {
                metadata.HorizontalResolution = horizontalValue;
                metadata.VerticalResolution = verticalValue;
                metadata.ResolutionUnits = UnitConverter.ExifProfileToResolutionUnit(exifProfile);
            }

            metadata.ExifProfile = exifProfile;
        }
    }

    private static double GetExifResolutionValue(ExifProfile exifProfile, ExifTag<Rational> tag)
    {
        if (exifProfile.TryGetValue(tag, out IExifValue<Rational>? resolution))
        {
            return resolution.Value.ToDouble();
        }

        return 0;
    }

    /// <summary>
    /// Reads the XMP profile the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ReadXmpProfile(BufferedReadStream stream, ImageMetadata metadata, Span<byte> buffer)
    {
        uint xmpChunkSize = ReadChunkSize(stream, buffer);
        if (this.skipMetadata)
        {
            stream.Skip((int)xmpChunkSize);
        }
        else
        {
            byte[] xmpData = new byte[xmpChunkSize];
            int bytesRead = stream.Read(xmpData, 0, (int)xmpChunkSize);
            if (bytesRead != xmpChunkSize)
            {
                // Ignore invalid chunk.
                return;
            }

            metadata.XmpProfile = new XmpProfile(xmpData);
        }
    }

    /// <summary>
    /// Reads the ICCP chunk from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ReadIccProfile(BufferedReadStream stream, ImageMetadata metadata, Span<byte> buffer)
    {
        uint iccpChunkSize = ReadChunkSize(stream, buffer);
        if (this.skipMetadata)
        {
            stream.Skip((int)iccpChunkSize);
        }
        else
        {
            byte[] iccpData = new byte[iccpChunkSize];
            int bytesRead = stream.Read(iccpData, 0, (int)iccpChunkSize);
            if (bytesRead != iccpChunkSize)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the iccp chunk");
            }

            IccProfile profile = new(iccpData);
            if (profile.CheckIsValid())
            {
                metadata.IccProfile = profile;
            }
        }
    }

    /// <summary>
    /// Reads the animation parameters chunk from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private static void ReadAnimationParameters(BufferedReadStream stream, WebpFeatures features, Span<byte> buffer)
    {
        features.Animation = true;
        uint animationChunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, buffer);
        byte blue = (byte)stream.ReadByte();
        byte green = (byte)stream.ReadByte();
        byte red = (byte)stream.ReadByte();
        byte alpha = (byte)stream.ReadByte();
        features.AnimationBackgroundColor = new Color(new Rgba32(red, green, blue, alpha));
        int bytesRead = stream.Read(buffer, 0, 2);
        if (bytesRead != 2)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the animation loop count");
        }

        features.AnimationLoopCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    /// <summary>
    /// Reads the alpha data chunk data from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="features">The features.</param>
    /// <param name="ignoreAlpha">if set to true, skips the chunk data.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ReadAlphaData(BufferedReadStream stream, WebpFeatures features, bool ignoreAlpha, Span<byte> buffer)
    {
        uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, buffer);
        if (ignoreAlpha)
        {
            stream.Skip((int)alphaChunkSize);
            return;
        }

        features.AlphaChunkHeader = (byte)stream.ReadByte();
        int alphaDataSize = (int)(alphaChunkSize - 1);
        this.alphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);
        Span<byte> alphaData = this.alphaData.GetSpan();
        int bytesRead = stream.Read(alphaData, 0, alphaDataSize);
        if (bytesRead != alphaDataSize)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the alpha data from the stream");
        }
    }

    /// <summary>
    /// Identifies the chunk type from the chunk.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if the input stream is not valid.
    /// </exception>
    private static WebpChunkType ReadChunkType(BufferedReadStream stream, Span<byte> buffer)
    {
        if (stream.Read(buffer, 0, 4) == 4)
        {
            return (WebpChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        throw new ImageFormatException("Invalid Webp data.");
    }

    /// <summary>
    /// Reads the chunk size. If Chunk Size is odd, a single padding byte will be added to the payload,
    /// so the chunk size will be increased by 1 in those cases.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="required">If true, the chunk size is required to be read, otherwise it can be skipped.</param>
    /// <returns>The chunk size in bytes.</returns>
    /// <exception cref="ImageFormatException">Invalid data.</exception>
    private static uint ReadChunkSize(BufferedReadStream stream, Span<byte> buffer, bool required = true)
    {
        if (stream.Read(buffer, 0, 4) == 4)
        {
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            return (chunkSize % 2 == 0) ? chunkSize : chunkSize + 1;
        }

        if (required)
        {
            throw new ImageFormatException("Invalid Webp data.");
        }

        // Return the size of the remaining data in the stream.
        return (uint)(stream.Length - stream.Position);
    }

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
