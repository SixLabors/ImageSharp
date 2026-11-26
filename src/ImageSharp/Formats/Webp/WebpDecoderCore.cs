// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Webp.ColorConverters;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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

    private readonly IccProfile? sourceIccProfile;

    /// <summary>
    /// The stream position where the RIFF payload ends. Chunk parsing stops here, so bytes after the payload are ignored.
    /// </summary>
    private long riffEnd;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public WebpDecoderCore(WebpDecoderOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpDecoderCore"/> class with the containing format's ICC profile.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="sourceIccProfile">The containing format's profile, or null to use the WebP profile.</param>
    public WebpDecoderCore(WebpDecoderOptions options, IccProfile? sourceIccProfile)
        : base(options.GeneralOptions)
    {
        this.backgroundColorHandling = options.BackgroundColorHandling;
        this.configuration = options.GeneralOptions.Configuration;
        this.skipMetadata = options.GeneralOptions.SkipMetadata;
        this.maxFrames = options.GeneralOptions.MaxFrames;
        this.memoryAllocator = this.configuration.MemoryAllocator;
        this.sourceIccProfile = sourceIccProfile;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        Image<TPixel>? image = null;
        try
        {
            // The containing format's profile takes precedence. ICCP parsing already skips a second profile while consuming its chunk.
            ImageMetadata metadata = new() { IccProfile = this.sourceIccProfile };
            Span<byte> buffer = stackalloc byte[4];

            this.ReadImageHeader(stream, buffer);

            using (this.webImageInfo = this.ReadVp8Info(stream, metadata))
            {
                ColorProfileConverter? colorProfileConverter = WebpColorConverterBase.CreateIccConverter(this.configuration, metadata.IccProfile);

                if (this.webImageInfo.Features is { Animation: true })
                {
                    using WebpAnimationDecoder animationDecoder = new(
                        this.memoryAllocator,
                        this.configuration,
                        this.maxFrames,
                        this.skipMetadata,
                        this.backgroundColorHandling,
                        this.Options.SegmentIntegrityHandling);

                    image = animationDecoder.Decode<TPixel>(
                        stream, metadata, this.webImageInfo.Features, this.webImageInfo.Width, this.webImageInfo.Height, this.riffEnd, colorProfileConverter);

                    return image;
                }

                // A VP8X header alone describes a canvas, not decodable image data.
                // Ignoring a truncated optional chunk must not bypass this requirement.
                if (this.webImageInfo.Vp8BitReader is null && this.webImageInfo.Vp8LBitReader is null)
                {
                    WebpThrowHelper.ThrowInvalidImageContentException("Missing WebP image data.");
                }

                // The image does not exist yet, so the method throws for damaged headers in each mode.
                _ = this.webImageInfo.TryReadImageHeaders(this.memoryAllocator, this.configuration, this.Options.SegmentIntegrityHandling, false);

                // ReadVp8Info read the metadata chunks after the image chunk too, so the metadata is complete here.
                image = new Image<TPixel>(this.configuration, (int)this.webImageInfo.Width, (int)this.webImageInfo.Height, metadata);
                this.webImageInfo.Decode(image.GetRootFramePixelBuffer(), this.alphaData, colorProfileConverter, this.Options.SegmentIntegrityHandling);
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
                    this.maxFrames,
                    this.skipMetadata,
                    this.backgroundColorHandling,
                    this.Options.SegmentIntegrityHandling);

                ImageInfo info = animationDecoder.Identify(
                    stream,
                    metadata,
                    this.webImageInfo.Features,
                    this.webImageInfo.Width,
                    this.webImageInfo.Height,
                    this.riffEnd);

                if (this.backgroundColorHandling != BackgroundColorHandling.Ignore)
                {
                    // Identify has no destination pixel format; RgbaVector preserves converted colors as unassociated floating-point values.
                    ColorProfileConverter? converter = WebpColorConverterBase.CreateIccConverter(this.configuration, metadata.IccProfile);
                    if (converter is not null)
                    {
                        WebpMetadata webpMetadata = metadata.GetWebpMetadata();
                        webpMetadata.BackgroundColor = converter.Convert<RgbaVector>(webpMetadata.BackgroundColor);
                    }
                }

                return info;
            }

            return new ImageInfo(
                new Size((int)this.webImageInfo.Width, (int)this.webImageInfo.Height),
                metadata);
        }
    }

    /// <summary>
    /// Reads the image header and records the end of the RIFF payload.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ReadImageHeader(BufferedReadStream stream, Span<byte> buffer)
    {
        long riffStart = stream.Position;

        // Skip FourCC header, we already know its a RIFF file at this point.
        stream.Skip(4);

        // Read file size.
        // The size of the file in bytes starting at offset 8.
        // The file size in the header is the total size of the chunks that follow plus 4 bytes for the ‘WEBP’ FourCC.
        if (stream.Read(buffer) != 4)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the RIFF size.");
        }

        uint fileSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

        // Bytes after the RIFF payload are not part of the image, so chunks are only read up to its end,
        // as libwebp does. Keep the declared end separate from EOF: IgnoreImageData can recover a compressed
        // payload cut short at EOF, but it must never borrow bytes from outside its declared parent.
        this.riffEnd = riffStart + WebpConstants.ChunkHeaderSize + fileSize;

        // Skip 'WEBP' from the header.
        stream.Skip(4);

        if (fileSize < 12 || Math.Min(this.riffEnd, stream.Length) - stream.Position < WebpConstants.ChunkHeaderSize)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the first WebP chunk.");
        }
    }

    /// <summary>
    /// Reads the information in the image header. The information describes the image content and how to decode the image.
    /// </summary>
    /// <remarks>
    /// For an extended file, the method also reads the metadata chunks after the image chunk, to the end of the RIFF payload. The image chunk copies its
    /// payload into its bit reader, so the stream is after it.
    /// </remarks>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreAlpha">If <see langword="true"/>, the method does not read the alpha data. Identify uses this value.</param>
    /// <returns>Information about the webp image.</returns>
    private WebpImageInfo ReadVp8Info(BufferedReadStream stream, ImageMetadata metadata, bool ignoreAlpha = false)
    {
        WebpMetadata webpMetadata = metadata.GetFormatMetadata(WebpFormat.Instance);

        Span<byte> buffer = stackalloc byte[4];
        WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
        WebpFeatures features = new();
        if (chunkType is WebpChunkType.Vp8 or WebpChunkType.Vp8L)
        {
            // The simple file format has one image chunk. As in libwebp, the decoder reads no chunk after it.
            // The image does not exist yet, so the method throws for a damaged header in each mode.
            _ = WebpChunkParsingUtils.TryReadImageChunk(
                chunkType,
                this.memoryAllocator,
                stream,
                buffer,
                features,
                this.riffEnd,
                this.Options.SegmentIntegrityHandling,
                false,
                out WebpImageInfo? simpleInfo);

            DebugGuard.NotNull(simpleInfo);
            webpMetadata.FileFormat = simpleInfo.IsLossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy;
            webpMetadata.ColorType = !simpleInfo.IsLossless ? WebpColorType.Yuv : features.Alpha ? WebpColorType.Rgba : WebpColorType.Rgb;
            return simpleInfo;
        }

        if (chunkType != WebpChunkType.Vp8X)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Unrecognized VP8 header");
        }

        WebpImageInfo info = WebpChunkParsingUtils.ReadVp8XHeader(stream, buffer, features, this.riffEnd);

        // Retain local ownership until return. Metadata parsing can fail after a copied bitstream has been allocated.
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        try
        {
            while (WebpChunkParsingUtils.TryReadNextChunkType(stream, buffer, metadata, this.skipMetadata, this.riffEnd, handling, out chunkType))
            {
                switch (chunkType)
                {
                    case WebpChunkType.Vp8:
                    case WebpChunkType.Vp8L:
                    {
                        // libwebp uses the first image chunk.
                        uint canvasWidth = info.Width;
                        uint canvasHeight = info.Height;

                        // The image does not exist yet, so the method throws for a damaged header in each mode.
                        _ = WebpChunkParsingUtils.TryReadImageChunk(
                            chunkType,
                            this.memoryAllocator,
                            stream,
                            buffer,
                            features,
                            this.riffEnd,
                            handling,
                            false,
                            out WebpImageInfo? imageInfo);

                        DebugGuard.NotNull(imageInfo);
                        info = imageInfo;

                        // libwebp rejects a still image whose size is not the canvas size.
                        if (info.Width != canvasWidth || info.Height != canvasHeight)
                        {
                            WebpThrowHelper.ThrowInvalidImageContentException("The WebP image size is not the VP8X canvas size.");
                        }

                        webpMetadata.FileFormat = info.IsLossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy;
                        webpMetadata.ColorType = features.Alpha ? WebpColorType.Rgba : WebpColorType.Rgb;

                        // The chunks after the image chunk hold only metadata, so the decoder skips each other chunk. As in libwebp, the decoder does not use
                        // an ALPH chunk after the image chunk.
                        while (WebpChunkParsingUtils.TryReadNextChunkType(stream, buffer, metadata, this.skipMetadata, this.riffEnd, handling, out _))
                        {
                            WebpChunkParsingUtils.SkipChunk(stream, buffer, this.riffEnd);
                        }

                        return info;
                    }

                    case WebpChunkType.Alpha:
                        this.ReadAlphaData(stream, features, ignoreAlpha, buffer);
                        break;

                    case WebpChunkType.AnimationParameter:
                        // The ANIM chunk comes before the frames. The animated decoder reads the chunks after it.
                        ReadAnimationParameters(stream, features, buffer, this.riffEnd);
                        return info;

                    default:
                        // Chunks that the decoder does not know do not change the pixels. SkipChunk adds the padding byte to the size. When the declared size
                        // is more than the bytes in the container, the skip stops at the end of the RIFF payload.
                        WebpChunkParsingUtils.SkipChunk(stream, buffer, this.riffEnd);
                        break;
                }
            }

            return info;
        }
        catch
        {
            info.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the animation parameters chunk from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    private static void ReadAnimationParameters(BufferedReadStream stream, WebpFeatures features, Span<byte> buffer, long containerEnd)
    {
        features.Animation = true;

        // The ANIM chunk is not image data, and the image does not exist yet. Thus, the method throws in each mode for a chunk that does not fit. The size
        // check keeps the 6-byte payload in the stream.
        _ = WebpChunkParsingUtils.TryReadChunkSize(stream, buffer, containerEnd, 6, SegmentIntegrityHandling.Strict, false, out uint animationChunkSize);
        long chunkEnd = stream.Position + animationChunkSize + (animationChunkSize & 1);
        byte blue = (byte)stream.ReadByte();
        byte green = (byte)stream.ReadByte();
        byte red = (byte)stream.ReadByte();
        byte alpha = (byte)stream.ReadByte();
        features.AnimationBackgroundColor = Color.FromPixel(new Rgba32(red, green, blue, alpha));
        stream.Read(buffer, 0, 2);
        features.AnimationLoopCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        stream.Position = chunkEnd;
    }

    /// <summary>
    /// Reads the data of the ALPH chunk from the stream. As in libwebp, the decoder uses the last ALPH chunk before the image chunk.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="features">The features.</param>
    /// <param name="ignoreAlpha">If <see langword="true"/>, the method skips the chunk data.</param>
    /// <param name="buffer">A temporary buffer.</param>
    private void ReadAlphaData(BufferedReadStream stream, WebpFeatures features, bool ignoreAlpha, Span<byte> buffer)
    {
        // The image does not exist yet. Thus each call throws in each mode for a chunk that does not fit.
        if (ignoreAlpha)
        {
            // Identify does not use the alpha data. It checks the extent of the chunk and skips the payload. Alpha data is image data, so the image data
            // integrity policy applies to an ALPH chunk that ends at EOF.
            _ = WebpChunkParsingUtils.TryReadChunkSize(stream, buffer, this.riffEnd, 1, this.Options.SegmentIntegrityHandling, false, out uint alphaChunkSize);
            stream.Position = Math.Min(stream.Position + alphaChunkSize + (alphaChunkSize & 1), stream.Length);
            return;
        }

        this.alphaData?.Dispose();
        this.alphaData = null;
        _ = WebpChunkParsingUtils.TryReadAlphaChunk(
            stream,
            buffer,
            this.memoryAllocator,
            this.riffEnd,
            this.Options.SegmentIntegrityHandling,
            false,
            out this.alphaData,
            out byte alphaChunkHeader);

        features.AlphaChunkHeader = alphaChunkHeader;
    }

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
