// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
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

    private readonly SegmentIntegrityHandling segmentIntegrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public WebpDecoderCore(WebpDecoderOptions options)
        : base(options.GeneralOptions)
    {
        this.backgroundColorHandling = options.BackgroundColorHandling;
        this.segmentIntegrityHandling = options.GeneralOptions.SegmentIntegrityHandling;
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
                        this.backgroundColorHandling,
                        this.segmentIntegrityHandling);

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
                    this.backgroundColorHandling,
                    this.segmentIntegrityHandling);

                return animationDecoder.Identify(
                    stream,
                    this.webImageInfo.Features,
                    this.webImageInfo.Width,
                    this.webImageInfo.Height,
                    fileSize);
            }

            return new ImageInfo(
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

        WebpImageInfo? info = null;
        WebpFeatures features = new();
        switch (chunkType)
        {
            case WebpChunkType.Vp8:
                info = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, buffer, features);
                webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                webpMetadata.ColorType = WebpColorType.Yuv;
                return info;
            case WebpChunkType.Vp8L:
                info = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, buffer, features);
                webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                webpMetadata.ColorType = info.Features?.Alpha == true ? WebpColorType.Rgba : WebpColorType.Rgb;
                return info;
            case WebpChunkType.Vp8X:
                info = WebpChunkParsingUtils.ReadVp8XHeader(stream, buffer, features);
                while (stream.Position < stream.Length)
                {
                    chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
                    if (chunkType == WebpChunkType.Vp8)
                    {
                        info = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, buffer, features);
                        webpMetadata.FileFormat = WebpFileFormatType.Lossy;
                        webpMetadata.ColorType = info.Features?.Alpha == true ? WebpColorType.Rgba : WebpColorType.Rgb;
                    }
                    else if (chunkType == WebpChunkType.Vp8L)
                    {
                        info = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, buffer, features);
                        webpMetadata.FileFormat = WebpFileFormatType.Lossless;
                        webpMetadata.ColorType = info.Features?.Alpha == true ? WebpColorType.Rgba : WebpColorType.Rgb;
                    }
                    else if (WebpChunkParsingUtils.IsOptionalVp8XChunk(chunkType))
                    {
                        // ANIM chunks appear before EXIF and XMP chunks.
                        // Return after parsing an ANIM chunk - The animated decoder will handle the rest.
                        bool isAnimationChunk = this.ParseOptionalExtendedChunks(stream, metadata, chunkType, features, ignoreAlpha, buffer);
                        if (isAnimationChunk)
                        {
                            return info;
                        }
                    }
                    else
                    {
                        // Ignore unknown chunks.
                        // These must always fall after the image data so we are safe to always skip them.
                        uint chunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, buffer, false);
                        stream.Skip((int)chunkSize);
                    }
                }

                return info;
            default:
                WebpThrowHelper.ThrowImageFormatException("Unrecognized VP8 header");
                return
                    new WebpImageInfo(); // this return will never be reached, because throw helper will throw an exception.
        }
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
        bool ignoreMetadata = this.skipMetadata;
        SegmentIntegrityHandling integrityHandling = this.segmentIntegrityHandling;
        switch (chunkType)
        {
            case WebpChunkType.Iccp:
                WebpChunkParsingUtils.ReadIccProfile(stream, metadata, ignoreMetadata, integrityHandling, buffer);
                break;

            case WebpChunkType.Exif:
                WebpChunkParsingUtils.ReadExifProfile(stream, metadata, ignoreMetadata, integrityHandling, buffer);
                break;

            case WebpChunkType.Xmp:
                WebpChunkParsingUtils.ReadXmpProfile(stream, metadata, ignoreMetadata, integrityHandling, buffer);
                break;

            case WebpChunkType.AnimationParameter:
                ReadAnimationParameters(stream, features, buffer);
                return true;

            case WebpChunkType.Alpha:
                this.ReadAlphaData(stream, features, ignoreAlpha, buffer);
                break;
            default:

                // Specification explicitly states to ignore unknown chunks.
                // We do not support writing these chunks at present.
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
        bool ignoreMetadata = this.skipMetadata;
        SegmentIntegrityHandling integrityHandling = this.segmentIntegrityHandling;

        if (ignoreMetadata || (!features.ExifProfile && !features.XmpMetaData))
        {
            return;
        }

        long streamLength = stream.Length;
        while (stream.Position < streamLength)
        {
            // Read chunk header.
            WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
            if (chunkType == WebpChunkType.Exif && metadata.ExifProfile == null)
            {
                WebpChunkParsingUtils.ReadExifProfile(stream, metadata, ignoreMetadata, integrityHandling, buffer);
            }
            else if (chunkType == WebpChunkType.Xmp && metadata.XmpProfile == null)
            {
                WebpChunkParsingUtils.ReadXmpProfile(stream, metadata, ignoreMetadata, integrityHandling, buffer);
            }
            else
            {
                // Skip duplicate XMP or EXIF chunk.
                uint chunkLength = WebpChunkParsingUtils.ReadChunkSize(stream, buffer, false);
                stream.Skip((int)chunkLength);
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
        features.AnimationBackgroundColor = Color.FromPixel(new Rgba32(red, green, blue, alpha));
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

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
