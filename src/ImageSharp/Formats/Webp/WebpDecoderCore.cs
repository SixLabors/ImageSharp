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

    private long riffEnd;

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

            this.ReadImageHeader(stream, buffer);

            using (this.webImageInfo = this.ReadVp8Info(stream, metadata))
            {
                if (this.webImageInfo.Features is { Animation: true })
                {
                    using WebpAnimationDecoder animationDecoder = new(
                        this.memoryAllocator,
                        this.configuration,
                        metadata,
                        this.maxFrames,
                        this.skipMetadata,
                        this.backgroundColorHandling);

                    return animationDecoder.Decode<TPixel>(stream, this.webImageInfo.Features, this.webImageInfo.Width, this.webImageInfo.Height, this.riffEnd);
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
        this.ReadImageHeader(stream, stackalloc byte[4]);
        ImageMetadata metadata = new();

        using (this.webImageInfo = this.ReadVp8Info(stream, metadata, true))
        {
            if (this.webImageInfo.Features is { Animation: true })
            {
                using WebpAnimationDecoder animationDecoder = new(
                    this.memoryAllocator,
                    this.configuration,
                    metadata,
                    this.maxFrames,
                    this.skipMetadata,
                    this.backgroundColorHandling);

                return animationDecoder.Identify(
                    stream,
                    (int)this.webImageInfo.BitsPerPixel,
                    this.webImageInfo.Features,
                    this.webImageInfo.Width,
                    this.webImageInfo.Height,
                    this.riffEnd);
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
    private void ReadImageHeader(BufferedReadStream stream, Span<byte> buffer)
    {
        // Skip FourCC header, we already know its a RIFF file at this point.
        stream.Skip(4);

        // Read file size.
        // The size of the file in bytes starting at offset 8.
        // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
        uint fileSize = WebpChunkParsingUtils.ReadChunkPayloadSize(stream, buffer);

        // The RIFF size includes the four-byte WEBP identifier.
        if (fileSize < 4)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Invalid WebP RIFF size.");
        }

        this.riffEnd = stream.Position + Math.Min((long)fileSize, stream.Length - stream.Position);
        stream.Skip(4);
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
                while (this.riffEnd - stream.Position >= 8)
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
                        WebpChunkParsingUtils.ReadMetadataChunk(stream, chunkType, metadata, true, buffer, this.riffEnd);
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
    /// <returns>Whether the chunk starts an animation.</returns>
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
            case WebpChunkType.Exif:
            case WebpChunkType.Xmp:
                WebpChunkParsingUtils.ReadMetadataChunk(stream, chunkType, metadata, this.skipMetadata, buffer, this.riffEnd);
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
    /// Reads optional metadata chunks that follow the image data.
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

        while (this.riffEnd - stream.Position >= 8)
        {
            WebpChunkType chunkType = ReadChunkType(stream, buffer);
            WebpChunkParsingUtils.ReadMetadataChunk(stream, chunkType, metadata, false, buffer, this.riffEnd);
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
            if (chunkSize == uint.MaxValue)
            {
                // The mandatory padding byte cannot fit in the returned 32-bit extent.
                WebpThrowHelper.ThrowInvalidImageContentException("Invalid WebP chunk size.");
            }

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
