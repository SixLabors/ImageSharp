// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Performs the png decoding operation.
/// </summary>
internal sealed class PngDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// The general decoder options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Whether the metadata should be ignored when the image is being decoded.
    /// </summary>
    private readonly uint maxFrames;

    /// <summary>
    /// Whether the metadata should be ignored when the image is being decoded.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// Whether to read the IHDR and tRNS chunks only.
    /// </summary>
    private readonly bool colorMetadataOnly;

    /// <summary>
    /// Used the manage memory allocations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The stream to decode from.
    /// </summary>
    private BufferedReadStream currentStream = null!;

    /// <summary>
    /// The png header.
    /// </summary>
    private PngHeader header;

    /// <summary>
    /// A value that tells if the decoder read the IHDR chunk.
    /// </summary>
    private bool hasHeader;

    /// <summary>
    /// The png animation control.
    /// </summary>
    private AnimationControl animationControl;

    /// <summary>
    /// The number of bytes per pixel.
    /// </summary>
    private int bytesPerPixel;

    /// <summary>
    /// The number of bytes per scanline.
    /// </summary>
    private int bytesPerScanline;

    /// <summary>
    /// The palette containing color information for indexed png's.
    /// </summary>
    private byte[] palette = null!;

    /// <summary>
    /// The palette containing alpha channel color information for indexed png's.
    /// </summary>
    private byte[] paletteAlpha = null!;

    /// <summary>
    /// Previous scanline processed.
    /// </summary>
    private IMemoryOwner<byte> previousScanline = null!;

    /// <summary>
    /// The current scanline that is being processed.
    /// </summary>
    private IMemoryOwner<byte> scanline = null!;

    /// <summary>
    /// Gets or sets the png color type.
    /// </summary>
    private PngColorType pngColorType;

    /// <summary>
    /// The next chunk of data to return.
    /// </summary>
    private PngChunk? nextChunk;

    /// <summary>
    /// How to handle CRC errors.
    /// </summary>
    private readonly SegmentIntegrityHandling segmentIntegrityHandling;

    /// <summary>
    /// A reusable Crc32 hashing instance.
    /// </summary>
    private readonly Crc32 crc32 = new();

    /// <summary>
    /// The buffer that the CRC check of image data chunks reads the chunk payload through. Otherwise, <see langword="null"/>.
    /// </summary>
    private IMemoryOwner<byte>? dataCrcBuffer;

    /// <summary>
    /// The stream position after the payload of the image data chunk that the inflater reads, or -1 when the inflater reads no chunk.
    /// </summary>
    private long imageDataChunkEnd = -1;

    /// <summary>
    /// The type of the chunks of the compressed stream that the inflater reads: IDAT or fdAT.
    /// </summary>
    private PngChunkType imageDataChunkType;

    /// <summary>
    /// The delegate that gives the inflater the next chunk of the compressed stream. One instance serves each frame.
    /// </summary>
    private readonly Func<int> readNextDataChunk;

    /// <summary>
    /// The maximum memory in bytes that a zTXt, sPLT, iTXt, iCCP, or unknown chunk can occupy when decompressed.
    /// </summary>
    private readonly int maxUncompressedLength;

    /// <summary>
    /// Whether this is an Apple CgBI PNG. CgBI files store IDATs as raw DEFLATE
    /// (no zlib header/Adler-32) and pixels as premultiplied BGRA, so they need
    /// extra inversion steps to round-trip back to standard PNG semantics.
    /// </summary>
    private bool isCgbi;

    /// <summary>
    /// Initializes a new instance of the <see cref="PngDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public PngDecoderCore(PngDecoderOptions options)
        : base(options.GeneralOptions)
    {
        this.configuration = options.GeneralOptions.Configuration;
        this.maxFrames = options.GeneralOptions.MaxFrames;
        this.skipMetadata = options.GeneralOptions.SkipMetadata;
        this.memoryAllocator = this.configuration.MemoryAllocator;
        this.segmentIntegrityHandling = options.GeneralOptions.SegmentIntegrityHandling;
        this.maxUncompressedLength = options.MaxUncompressedAncillaryChunkSizeBytes;
        this.readNextDataChunk = this.ReadNextDataChunk;
    }

    internal PngDecoderCore(PngDecoderOptions options, bool colorMetadataOnly)
        : base(options.GeneralOptions)
    {
        this.colorMetadataOnly = colorMetadataOnly;
        this.maxFrames = options.GeneralOptions.MaxFrames;
        this.skipMetadata = true;
        this.configuration = options.GeneralOptions.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
        this.segmentIntegrityHandling = options.GeneralOptions.SegmentIntegrityHandling;
        this.maxUncompressedLength = options.MaxUncompressedAncillaryChunkSizeBytes;
        this.readNextDataChunk = this.ReadNextDataChunk;
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        uint frameCount = 0;
        ImageMetadata metadata = new();
        PngMetadata pngMetadata = metadata.GetPngMetadata();
        this.currentStream = stream;
        this.currentStream.Skip(8);
        Image<TPixel>? image = null;
        FrameControl? previousFrameControl = null;
        FrameControl? currentFrameControl = null;
        ImageFrame<TPixel>? previousFrame = null;
        ImageFrame<TPixel>? currentFrame = null;
        Span<byte> buffer = stackalloc byte[20];

        try
        {
            while (this.TryReadChunk(buffer, out PngChunk chunk))
            {
                try
                {
                    switch (chunk.Type)
                    {
                        case PngChunkType.Header:
                            this.ReadHeaderChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.AnimationControl:
                            this.ReadAnimationControlChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Physical:
                            ReadPhysicalChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Gamma:
                            ReadGammaChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Cicp:
                            ReadCicpChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameControl:
                        {
                            if (!this.TryReadFrameControlChunk(chunk.Data.GetSpan(), image is not null, ++frameCount, out FrameControl frameControl))
                            {
                                // The frame limit or the mode ends the decode. The decoder keeps the frames that it decoded.
                                goto EOF;
                            }

                            currentFrameControl = frameControl;
                            currentFrame = null;
                            break;
                        }

                        case PngChunkType.FrameData:
                        {
                            // The default image comes before each animation frame.
                            if (image is null)
                            {
                                PngThrowHelper.ThrowMissingDefaultData();
                            }

                            if (!this.TryStartFrameData(chunk, currentFrame is not null))
                            {
                                break;
                            }

                            // The IDAT chunk sets the frame control of the default image, so a frame control exists here.
                            Debug.Assert(currentFrameControl.HasValue, "The default image has a frame control.");
                            this.InitializeFrame(previousFrameControl, currentFrameControl.Value, image, previousFrame, out currentFrame);

                            this.currentStream.Position += 4;
                            this.ReadScanlines(
                                chunk.Length - 4,
                                currentFrame,
                                pngMetadata,
                                PngChunkType.FrameData,
                                currentFrameControl.Value,
                                cancellationToken);

                            // if current frame dispose is restore to previous, then from future frame's perspective, it never happened
                            if (currentFrameControl.Value.DisposalMode != FrameDisposalMode.RestoreToPrevious)
                            {
                                previousFrame = currentFrame;
                                previousFrameControl = currentFrameControl;
                            }

                            break;
                        }

                        case PngChunkType.Data:
                        {
                            if (image is not null)
                            {
                                // The IDAT chunks are consecutive and hold one zlib stream. See PNG Third Edition,
                                // section 11.2.3 "IDAT Image data". The first IDAT chunk creates the image, and the
                                // decoder reads the image from that stream. A later IDAT chunk holds only data after
                                // the end of that stream, for example the Adler-32 checksum.
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            pngMetadata.AnimateRootFrame = currentFrameControl.HasValue;
                            currentFrameControl = this.GetDefaultImageFrameControl(currentFrameControl);

                            // PLTE and tRNS come before IDAT. The decoder checks the palette of an indexed image before it allocates the pixels.
                            this.AssignColorPalette<TPixel>(this.palette, this.paletteAlpha, pngMetadata, metadata.IccProfile);
                            this.InitializeImage(metadata, currentFrameControl.Value, out image);

                            this.ReadScanlines(
                                chunk.Length,
                                image.Frames.RootFrame,
                                pngMetadata,
                                PngChunkType.Data,
                                currentFrameControl.Value,
                                cancellationToken);
                            if (pngMetadata.AnimateRootFrame)
                            {
                                previousFrame = currentFrame;
                                previousFrameControl = currentFrameControl;
                            }

                            if (frameCount > this.maxFrames)
                            {
                                goto EOF;
                            }

                            break;
                        }

                        case PngChunkType.Palette:
                            this.palette = [.. chunk.Data.GetSpan()];
                            break;
                        case PngChunkType.Transparency:
                            this.paletteAlpha = [.. chunk.Data.GetSpan()];
                            this.AssignTransparentMarkers(this.paletteAlpha, pngMetadata);
                            break;
                        case PngChunkType.Text:
                            this.ReadTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.CompressedText:
                            this.ReadCompressedTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.InternationalText:
                            this.ReadInternationalTextChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Exif:
                            this.ReadExifChunk(metadata, chunk);

                            break;
                        case PngChunkType.EmbeddedColorProfile:
                            this.ReadColorProfileChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.End:
                            goto EOF;
                        case PngChunkType.ProprietaryApple:
                            // GetDefaultImageFrameControl checks the header of a CgBI image at the first IDAT chunk.
                            this.isCgbi = true;
                            break;
                    }
                }
                finally
                {
                    chunk.Data?.Dispose(); // Data is rented in ReadChunkData()
                }
            }

            EOF:
            if (image is null)
            {
                PngThrowHelper.ThrowNoData();
            }

            if (this.pngColorType != PngColorType.Palette)
            {
                _ = TryConvertIccProfile(image);
            }

            return image;
        }
        catch
        {
            image?.Dispose();
            throw;
        }
        finally
        {
            this.scanline?.Dispose();
            this.previousScanline?.Dispose();
            this.dataCrcBuffer?.Dispose();
            this.nextChunk?.Data?.Dispose();
        }
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        uint frameCount = 0;
        ImageMetadata metadata = new();
        List<ImageFrameMetadata> framesMetadata = [];
        PngMetadata pngMetadata = metadata.GetPngMetadata();
        this.currentStream = stream;
        FrameControl? currentFrameControl = null;
        bool frameStarted = false;
        Span<byte> buffer = stackalloc byte[20];

        this.currentStream.Skip(8);

        try
        {
            while (this.TryReadChunk(buffer, out PngChunk chunk))
            {
                try
                {
                    switch (chunk.Type)
                    {
                        case PngChunkType.Header:
                            this.ReadHeaderChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.AnimationControl:
                            this.ReadAnimationControlChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Physical:
                            ReadPhysicalChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Gamma:
                            ReadGammaChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Cicp:
                            ReadCicpChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameControl:
                        {
                            if (!this.TryReadFrameControlChunk(chunk.Data.GetSpan(), framesMetadata.Count > 0, ++frameCount, out FrameControl frameControl))
                            {
                                // The frame limit or the mode ends the read. Identify keeps the frames that it read.
                                goto EOF;
                            }

                            currentFrameControl = frameControl;
                            frameStarted = false;
                            break;
                        }

                        case PngChunkType.FrameData:
                            // The default image comes before each animation frame.
                            if (framesMetadata.Count == 0)
                            {
                                PngThrowHelper.ThrowMissingDefaultData();
                            }

                            if (!this.TryStartFrameData(chunk, frameStarted))
                            {
                                break;
                            }

                            // The IDAT chunk sets the frame control of the default image, so a frame control exists here.
                            Debug.Assert(currentFrameControl.HasValue, "The default image has a frame control.");
                            InitializeFrameMetadata(framesMetadata, currentFrameControl.Value);
                            frameStarted = true;

                            // Identify skips the data of the frame. Decode reads the same chunks through ReadScanlines.
                            this.SkipFrameData(chunk);
                            break;
                        case PngChunkType.Data:
                            if (framesMetadata.Count > 0)
                            {
                                // The first IDAT chunk starts the zlib stream of the default image. Identify skips the other IDAT chunks.
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            pngMetadata.AnimateRootFrame = currentFrameControl.HasValue;
                            currentFrameControl = this.GetDefaultImageFrameControl(currentFrameControl);

                            // Spec says tRNS must be before IDAT so safe to exit.
                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            InitializeFrameMetadata(framesMetadata, currentFrameControl.Value);

                            // The specification puts the PLTE and tRNS chunks before IDAT, so the decoder read them before this point. Identify has no
                            // destination pixel format. RgbaVector keeps the converted colors as unassociated floating-point values.
                            this.AssignColorPalette<RgbaVector>(this.palette, this.paletteAlpha, pngMetadata, metadata.IccProfile);

                            this.SkipChunkDataAndCrc(chunk);
                            if (frameCount > this.maxFrames)
                            {
                                goto EOF;
                            }

                            break;
                        case PngChunkType.Palette:
                            this.palette = [.. chunk.Data.GetSpan()];
                            break;

                        case PngChunkType.Transparency:
                            this.paletteAlpha = [.. chunk.Data.GetSpan()];
                            this.AssignTransparentMarkers(this.paletteAlpha, pngMetadata);

                            // Spec says tRNS must be after PLTE so safe to exit.
                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            break;
                        case PngChunkType.Text:
                            this.ReadTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.CompressedText:
                            this.ReadCompressedTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.InternationalText:
                            this.ReadInternationalTextChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Exif:
                            this.ReadExifChunk(metadata, chunk);

                            break;
                        case PngChunkType.EmbeddedColorProfile:
                            this.ReadColorProfileChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.End:
                            goto EOF;

                        case PngChunkType.ProprietaryApple:
                            // GetDefaultImageFrameControl checks the header of a CgBI image at the first IDAT chunk.
                            this.isCgbi = true;
                            break;
                    }
                }
                finally
                {
                    chunk.Data?.Dispose(); // Data is rented in ReadChunkData()
                }
            }

            EOF:
            if (!this.hasHeader)
            {
                PngThrowHelper.ThrowInvalidHeader();
            }

            return new ImageInfo(new Size(this.header.Width, this.header.Height), metadata, framesMetadata);
        }
        finally
        {
            this.scanline?.Dispose();
            this.previousScanline?.Dispose();
            this.dataCrcBuffer?.Dispose();
            this.nextChunk?.Data?.Dispose();
        }
    }

    /// <summary>
    /// Reads the least significant bits from the byte pair with the others set to 0.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">THe offset.</param>
    /// <returns>The <see cref="int"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ReadByteLittleEndian(ReadOnlySpan<byte> buffer, int offset)
        => (byte)(((buffer[offset] & 0xFF) << 16) | (buffer[offset + 1] & 0xFF));

    /// <summary>
    /// Reads the data chunk containing physical dimension data.
    /// </summary>
    /// <param name="metadata">The metadata to read to.</param>
    /// <param name="data">The data containing physical data.</param>
    private static void ReadPhysicalChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        PngPhysical physicalChunk = PngPhysical.Parse(data);

        metadata.ResolutionUnits = physicalChunk.UnitSpecifier == byte.MinValue
            ? PixelResolutionUnit.AspectRatio
            : PixelResolutionUnit.PixelsPerMeter;

        metadata.HorizontalResolution = physicalChunk.XAxisPixelsPerUnit;
        metadata.VerticalResolution = physicalChunk.YAxisPixelsPerUnit;
    }

    /// <summary>
    /// Reads the data chunk that contains gamma data.
    /// </summary>
    /// <param name="pngMetadata">The metadata to read to.</param>
    /// <param name="data">The gamma data. It has 4 bytes.</param>
    private static void ReadGammaChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
    {
        // For example, a gamma of 1/2.2 would be stored as 45455.
        // The value is encoded as a 4-byte unsigned integer, representing gamma times 100000.
        pngMetadata.Gamma = BinaryPrimitives.ReadUInt32BigEndian(data) * 1e-5F;
    }

    /// <summary>
    /// Initializes the image and various buffers needed for processing
    /// </summary>
    /// <typeparam name="TPixel">The type the pixels will be</typeparam>
    /// <param name="metadata">The metadata information for the image</param>
    /// <param name="frameControl">The frame control information for the frame</param>
    /// <param name="image">The image that we will populate</param>
    private void InitializeImage<TPixel>(ImageMetadata metadata, FrameControl frameControl, out Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        image = new Image<TPixel>(this.configuration, this.header.Width, this.header.Height, metadata);

        PngFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetPngMetadata();
        frameMetadata.FromChunk(in frameControl);

        // PngHeader.Validate makes sure that the scanline and its filter type byte fit in an int.
        this.bytesPerPixel = PngHeader.CalculateBytesPerPixel(this.header.ColorType, this.header.BitDepth);
        this.bytesPerScanline = (int)PngHeader.CalculateScanlineLength(this.header.Width, this.header.BitDepth, this.bytesPerPixel) + 1;

        this.previousScanline?.Dispose();
        this.scanline?.Dispose();
        this.previousScanline = this.memoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
        this.scanline = this.configuration.MemoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
    }

    /// <summary>
    /// Initializes the image and various buffers needed for processing
    /// </summary>
    /// <typeparam name="TPixel">The type the pixels will be</typeparam>
    /// <param name="previousFrameControl">The frame control information for the previous frame.</param>
    /// <param name="currentFrameControl">The frame control information for the current frame.</param>
    /// <param name="image">The image that we will populate</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="frame">The created frame</param>
    private void InitializeFrame<TPixel>(
        FrameControl? previousFrameControl,
        FrameControl currentFrameControl,
        Image<TPixel> image,
        ImageFrame<TPixel>? previousFrame,
        out ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // We create a clone of the previous frame and add it.
        // We will overpaint the difference of pixels on the current frame to create a complete image.
        // This ensures that we have enough pixel data to process without distortion. #2450
        frame = image.Frames.AddFrame(previousFrame ?? image.Frames.RootFrame);

        // If the first `fcTL` chunk uses a `dispose_op` of APNG_DISPOSE_OP_PREVIOUS it should be treated as APNG_DISPOSE_OP_BACKGROUND.
        // So, if restoring to before first frame, clear entire area. Same if first frame (previousFrameControl null).
        if (previousFrameControl == null || (previousFrame is null && previousFrameControl.Value.DisposalMode == FrameDisposalMode.RestoreToPrevious))
        {
            Buffer2DRegion<TPixel> pixelRegion = frame.PixelBuffer.GetRegion();
            pixelRegion.Clear();
        }
        else if (previousFrameControl.Value.DisposalMode == FrameDisposalMode.RestoreToBackground)
        {
            Rectangle restoreArea = previousFrameControl.Value.Bounds;
            Buffer2DRegion<TPixel> pixelRegion = frame.PixelBuffer.GetRegion(restoreArea);
            pixelRegion.Clear();
        }

        PngFrameMetadata frameMetadata = frame.Metadata.GetPngMetadata();
        frameMetadata.FromChunk(currentFrameControl);

        this.previousScanline?.Dispose();
        this.scanline?.Dispose();
        this.previousScanline = this.memoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
        this.scanline = this.configuration.MemoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
    }

    private static void InitializeFrameMetadata(List<ImageFrameMetadata> imageFrameMetadata, FrameControl currentFrameControl)
    {
        ImageFrameMetadata meta = new();
        PngFrameMetadata frameMetadata = meta.GetPngMetadata();
        frameMetadata.FromChunk(currentFrameControl);
        imageFrameMetadata.Add(meta);
    }

    /// <summary>
    /// Reads the scanlines within the image.
    /// </summary>
    /// <remarks>
    /// The compressed data starts at the stream position and continues in the chunks of the same type that follow. After the decode, the stream is at the start
    /// of the next chunk header, or <see cref="nextChunk"/> holds the next chunk.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="chunkLength">The length of the chunk that contains the compressed scanline data.</param>
    /// <param name="image">The pixel data.</param>
    /// <param name="pngMetadata">The PNG metadata.</param>
    /// <param name="chunkType">The type of the chunks that hold the compressed data: IDAT or fdAT.</param>
    /// <param name="frameControl">The frame control.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void ReadScanlines<TPixel>(
        int chunkLength,
        ImageFrame<TPixel> image,
        PngMetadata pngMetadata,
        PngChunkType chunkType,
        in FrameControl frameControl,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The inflater reads the payload of the first chunk from the stream position. Then it gets each later payload from the next chunk of the same type.
        this.imageDataChunkEnd = this.currentStream.Position + chunkLength;
        this.imageDataChunkType = chunkType;

        if (this.isCgbi)
        {
            // CgBI IDAT chunks hold a raw DEFLATE stream with no zlib CMF and FLG header and no Adler-32 trailer. Thus the decoder does not use the zlib header
            // parser.
            using ChunkedReadStream segmentStream = new(this.currentStream, this.readNextDataChunk);
            segmentStream.SetCurrentSegmentLength(chunkLength);
            using DeflateStream cgbiDataStream = new(segmentStream, CompressionMode.Decompress, leaveOpen: true);
            this.DecodeFromDeflate(cgbiDataStream, image, pngMetadata, frameControl, cancellationToken);
        }
        else
        {
            using ZlibInflateReader inflateStream = new(this.currentStream, this.readNextDataChunk);
            if (inflateStream.AllocateNewBytes(chunkLength))
            {
                this.DecodeFromDeflate(inflateStream.CompressedStream, image, pngMetadata, frameControl, cancellationToken);
            }
            else
            {
                // The zlib header is part of the image data. A missing header, or a header that is not valid, is damaged image data. No row decodes. The frame
                // region gets the same pixels as for data that ends before the first row.
                this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data does not start with a valid zlib header.");
                ClearUndecodedPixels(frameControl, image, -1, (int)frameControl.YOffset);
            }
        }

        // The inflater can stop inside the last chunk that it reads. After the last row, it does not read the Adler-32 checksum and the data after it. Damaged
        // data also stops it early. The decoder skips the rest of that chunk and its CRC. The next chunk header comes after the CRC.
        if (this.imageDataChunkEnd >= 0)
        {
            this.currentStream.Position = this.imageDataChunkEnd;
            this.currentStream.Skip(4);
            this.imageDataChunkEnd = -1;
        }
    }

    private void DecodeFromDeflate<TPixel>(
        DeflateStream dataStream,
        ImageFrame<TPixel> image,
        PngMetadata pngMetadata,
        in FrameControl frameControl,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.header.InterlaceMethod is PngInterlaceMode.Adam7)
        {
            this.DecodeInterlacedPixelData(frameControl, dataStream, image, pngMetadata, cancellationToken);
        }
        else
        {
            this.DecodePixelData(frameControl, dataStream, image, pngMetadata, cancellationToken);
        }
    }

    /// <summary>
    /// Decodes the raw pixel data row by row
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="imageFrame">The image frame to decode to.</param>
    /// <param name="pngMetadata">The png metadata</param>
    /// <param name="cancellationToken">The CancellationToken</param>
    private void DecodePixelData<TPixel>(
        FrameControl frameControl,
        DeflateStream compressedStream,
        ImageFrame<TPixel> imageFrame,
        PngMetadata pngMetadata,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<TPixel>? blendMemory = frameControl.BlendMode == FrameBlendMode.Over
            ? this.memoryAllocator.Allocate<TPixel>(imageFrame.Width, AllocationOptions.Clean)
            : null;

        using PngScanlineProcessor<TPixel> processor = this.CreateScanlineProcessor<TPixel>(frameControl, pngMetadata);

        this.DecodePixelDataCore(
            frameControl,
            compressedStream,
            imageFrame,
            blendMemory,
            in processor,
            cancellationToken);
    }

    /// <summary>
    /// Decodes the raw pixel data row by row.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control.</param>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="imageFrame">The image frame to decode to.</param>
    /// <param name="blendMemory">The optional row blending buffer.</param>
    /// <param name="processor">The scanline processor of the frame.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodePixelDataCore<TPixel>(
        FrameControl frameControl,
        DeflateStream compressedStream,
        ImageFrame<TPixel> imageFrame,
        IMemoryOwner<TPixel>? blendMemory,
        in PngScanlineProcessor<TPixel> processor,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int currentRow = (int)frameControl.YOffset;
        int height = (int)frameControl.YMax;

        // Each row of the frame has the same length: one filter type byte, then the row data.
        int bytesPerFrameScanline = (int)PngHeader.CalculateScanlineLength((int)frameControl.Width, this.header.BitDepth, this.bytesPerPixel) + 1;

        Span<TPixel> blendRowBuffer = blendMemory is null ? [] : blendMemory.Memory.Span;

        while (currentRow < height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<byte> scanSpan = this.scanline.GetSpan()[..bytesPerFrameScanline];
            Span<byte> prevSpan = this.previousScanline.GetSpan()[..bytesPerFrameScanline];

            // The rows before this row stay decoded. This row and the rows after it keep the fill value.
            if (!this.TryReadScanline(compressedStream, scanSpan))
            {
                ClearUndecodedPixels(frameControl, imageFrame, -1, currentRow);
                return;
            }

            switch ((FilterType)scanSpan[0])
            {
                case FilterType.None:
                    break;

                case FilterType.Sub:
                    PngFilterDecoder.Decode<SubFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                case FilterType.Up:
                    PngFilterDecoder.Decode<UpFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                case FilterType.Average:
                    PngFilterDecoder.Decode<AverageFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                case FilterType.Paeth:
                    PngFilterDecoder.Decode<PaethFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                default:
                    // A filter type that is not valid is damaged image data. Stop at this row.
                    this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("Unknown filter type.");
                    ClearUndecodedPixels(frameControl, imageFrame, -1, currentRow);
                    return;
            }

            if (this.isCgbi)
            {
                PngCgbiProcessor.ApplyTransform(this.configuration, scanSpan[1..], this.pngColorType);
            }

            this.ProcessDefilteredScanline(frameControl, currentRow, scanSpan, imageFrame, blendRowBuffer, in processor);
            this.SwapScanlineBuffers();
            currentRow++;
        }
    }

    /// <summary>
    /// Decodes the raw interlaced pixel data row by row
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="imageFrame">The current image frame.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodeInterlacedPixelData<TPixel>(
        in FrameControl frameControl,
        DeflateStream compressedStream,
        ImageFrame<TPixel> imageFrame,
        PngMetadata pngMetadata,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<TPixel>? blendMemory = frameControl.BlendMode == FrameBlendMode.Over
            ? this.memoryAllocator.Allocate<TPixel>(imageFrame.Width, AllocationOptions.Clean)
            : null;

        // The bulk conversion writes contiguous pixels. This buffer holds the pixels of one pass scanline before
        // they go to the columns of the pass. Each pass scanline writes the pixels before it reads them.
        using IMemoryOwner<TPixel> passMemory = this.memoryAllocator.Allocate<TPixel>((int)frameControl.Width);

        using PngScanlineProcessor<TPixel> processor = this.CreateScanlineProcessor<TPixel>(frameControl, pngMetadata);

        this.DecodeInterlacedPixelDataCore(
            frameControl,
            compressedStream,
            imageFrame,
            blendMemory,
            passMemory,
            in processor,
            cancellationToken);
    }

    /// <summary>
    /// Decodes the raw interlaced pixel data row by row.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control.</param>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="imageFrame">The current image frame.</param>
    /// <param name="blendMemory">The optional row blending buffer.</param>
    /// <param name="passMemory">The buffer for the pixels of one pass scanline.</param>
    /// <param name="processor">The scanline processor of the frame.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodeInterlacedPixelDataCore<TPixel>(
        FrameControl frameControl,
        DeflateStream compressedStream,
        ImageFrame<TPixel> imageFrame,
        IMemoryOwner<TPixel>? blendMemory,
        IMemoryOwner<TPixel> passMemory,
        in PngScanlineProcessor<TPixel> processor,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int currentRow = Adam7.FirstRow[0] + (int)frameControl.YOffset;
        int pass = 0;
        int width = (int)frameControl.Width;
        int endRow = (int)frameControl.YMax;

        Buffer2D<TPixel> imageBuffer = imageFrame.PixelBuffer;

        Span<TPixel> blendRowBuffer = blendMemory is null ? [] : blendMemory.Memory.Span;
        Span<TPixel> passRowBuffer = passMemory.Memory.Span;

        while (true)
        {
            int numColumns = Adam7.ComputeBlockWidth(width, pass);

            if (numColumns == 0)
            {
                // This pass has no scanlines. The next pass starts at its first row in the frame region.
                // Pass 6 always has columns, so the pass index stays less than 7.
                pass++;
                currentRow = Adam7.FirstRow[pass] + (int)frameControl.YOffset;
                continue;
            }

            int bytesPerInterlaceScanline = (int)PngHeader.CalculateScanlineLength(numColumns, this.header.BitDepth, this.bytesPerPixel) + 1;

            while (currentRow < endRow)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Span<byte> scanSpan = this.scanline.Slice(0, bytesPerInterlaceScanline);
                Span<byte> prevSpan = this.previousScanline.Slice(0, bytesPerInterlaceScanline);

                // The pixels of the earlier rows and passes stay decoded. The other pixels keep the fill value.
                if (!this.TryReadScanline(compressedStream, scanSpan))
                {
                    ClearUndecodedPixels(frameControl, imageFrame, pass, currentRow);
                    return;
                }

                switch ((FilterType)scanSpan[0])
                {
                    case FilterType.None:
                        break;

                    case FilterType.Sub:
                        PngFilterDecoder.Decode<SubFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Up:
                        PngFilterDecoder.Decode<UpFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Average:
                        PngFilterDecoder.Decode<AverageFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Paeth:
                        PngFilterDecoder.Decode<PaethFilterOperator>(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    default:
                        // A filter type that is not valid is damaged image data. Stop at this row.
                        this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("Unknown filter type.");
                        ClearUndecodedPixels(frameControl, imageFrame, pass, currentRow);
                        return;
                }

                if (this.isCgbi)
                {
                    PngCgbiProcessor.ApplyTransform(this.configuration, scanSpan[1..], this.pngColorType);
                }

                // Each Adam7 pass is a reduced image with its own scanline length, so pass only this scanline.
                // See PNG Third Edition, section 8.1 "Interlace methods": https://www.w3.org/TR/png-3/#8InterlaceMethods
                Span<TPixel> rowSpan = imageBuffer.DangerousGetRowSpan(currentRow);
                this.ProcessInterlacedDefilteredScanline(
                    frameControl,
                    scanSpan,
                    numColumns,
                    passRowBuffer,
                    rowSpan,
                    blendRowBuffer,
                    in processor,
                    Adam7.FirstColumn[pass],
                    Adam7.ColumnIncrement[pass]);

                blendRowBuffer.Clear();
                this.SwapScanlineBuffers();

                currentRow += Adam7.RowIncrement[pass];
            }

            pass++;
            this.previousScanline.Clear();

            if (pass < 7)
            {
                // The rows of each pass are relative to the frame region, as the rows of the first pass are.
                currentRow = Adam7.FirstRow[pass] + (int)frameControl.YOffset;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Clears the frame region pixels that damaged or truncated image data does not decode.
    /// </summary>
    /// <remarks>
    /// The frame buffer of an APNG frame starts as the canvas of the earlier frame. Source blending replaces the frame
    /// region with the frame pixels. Thus a pixel that the data does not decode gets the fill value, transparent black.
    /// A frame without an earlier canvas gets the same value. Over blending of a transparent pixel keeps the canvas
    /// pixel, so the method changes nothing for it. See PNG Third Edition, section 11.3.6.2 "fcTL Frame Control Chunk",
    /// blend_op: https://www.w3.org/TR/png-3/#fcTL-chunk.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control of the frame.</param>
    /// <param name="imageFrame">The frame that receives the decoded rows.</param>
    /// <param name="pass">The Adam7 pass of <paramref name="row"/>, or -1 for a frame without interlace.</param>
    /// <param name="row">The first row that the data does not decode, in image coordinates.</param>
    private static void ClearUndecodedPixels<TPixel>(in FrameControl frameControl, ImageFrame<TPixel> imageFrame, int pass, int row)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (frameControl.BlendMode != FrameBlendMode.Source)
        {
            return;
        }

        int left = (int)frameControl.XOffset;
        int right = (int)frameControl.XMax;
        int bottom = (int)frameControl.YMax;
        Buffer2D<TPixel> pixels = imageFrame.PixelBuffer;
        if (pass < 0)
        {
            for (int y = row; y < bottom; y++)
            {
                pixels.DangerousGetRowSpan(y)[left..right].Clear();
            }

            return;
        }

        // The data ends in this pass. The decoder does not decode this row, the later rows of this pass, or the
        // later passes. The pixels of the earlier rows and passes stay.
        for (int p = pass; p < 7; p++)
        {
            int firstRow = p == pass ? row : Adam7.FirstRow[p] + (int)frameControl.YOffset;
            for (int y = firstRow; y < bottom; y += Adam7.RowIncrement[p])
            {
                Span<TPixel> rowSpan = pixels.DangerousGetRowSpan(y);
                for (int x = left + Adam7.FirstColumn[p]; x < right; x += Adam7.ColumnIncrement[p])
                {
                    rowSpan[x] = default;
                }
            }
        }
    }

    /// <summary>
    /// Reads one complete filtered scanline from the compressed image data.
    /// </summary>
    /// <remarks>
    /// The compressed data must hold each scanline of the frame. Data that ends before the last scanline, and
    /// a deflate stream that is not valid, are damaged image data. The method reports them through
    /// <see cref="SegmentIntegrityHandlingExtensions.ThrowOrIgnoreImageDataError(SegmentIntegrityHandling, string)"/>.
    /// It throws in the Strict and IgnoreAncillary modes. It returns <see langword="false"/> in the IgnoreImageData mode.
    /// </remarks>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="scanline">The destination for the filter type byte and the filtered scanline bytes.</param>
    /// <returns><see langword="true"/> if the scanline is complete. Otherwise, <see langword="false"/>.</returns>
    private bool TryReadScanline(DeflateStream compressedStream, Span<byte> scanline)
    {
        int bytesRead = 0;
        try
        {
            // A deflate stream can return fewer bytes than requested at a block or chunk boundary.
            // Only a return value of 0 shows the end of the compressed data.
            while (bytesRead < scanline.Length)
            {
                int count = compressedStream.Read(scanline[bytesRead..]);
                if (count <= 0)
                {
                    this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data ends before the last scanline.");
                    return false;
                }

                bytesRead += count;
            }
        }
        catch (InvalidDataException ex)
        {
            // The inflater found a deflate block that is not valid.
            this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data is damaged.", ex);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Processes the de-filtered scanline filling the image pixel data
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="currentRow">The index of the current scanline being processed.</param>
    /// <param name="scanline">The de-filtered scanline</param>
    /// <param name="pixels">The image</param>
    /// <param name="blendRowBuffer">A span used to temporarily hold the decoded row pixel data for alpha blending.</param>
    /// <param name="processor">The scanline processor of the frame.</param>
    private void ProcessDefilteredScanline<TPixel>(
        in FrameControl frameControl,
        int currentRow,
        ReadOnlySpan<byte> scanline,
        ImageFrame<TPixel> pixels,
        Span<TPixel> blendRowBuffer,
        in PngScanlineProcessor<TPixel> processor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<TPixel> destination = pixels.PixelBuffer.DangerousGetRowSpan(currentRow);

        bool blend = frameControl.BlendMode == FrameBlendMode.Over;
        Span<TPixel> rowSpan = blend
            ? blendRowBuffer
            : destination;

        // The first byte is the filter type. The frame pixels start at the frame x offset.
        processor.ProcessScanline(scanline[1..], rowSpan.Slice((int)frameControl.XOffset, (int)frameControl.Width));

        if (blend)
        {
            PixelBlender<TPixel> blender = PixelBlenders<TPixel>.NormalSrcOver;

            blender.Blend<TPixel>(this.configuration, destination, destination, rowSpan, 1F);
        }
    }

    /// <summary>
    /// Processes the interlaced de-filtered scanline filling the image pixel data
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="scanline">The de-filtered scanline</param>
    /// <param name="columns">The pixel count of the pass scanline.</param>
    /// <param name="passRowBuffer">The buffer for the pixels of the pass scanline.</param>
    /// <param name="destination">The current image row.</param>
    /// <param name="blendRowBuffer">A span used to temporarily hold the decoded row pixel data for alpha blending.</param>
    /// <param name="processor">The scanline processor of the frame.</param>
    /// <param name="firstColumn">The first column of the pass.</param>
    /// <param name="columnIncrement">The column step of the pass.</param>
    private void ProcessInterlacedDefilteredScanline<TPixel>(
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanline,
        int columns,
        Span<TPixel> passRowBuffer,
        Span<TPixel> destination,
        Span<TPixel> blendRowBuffer,
        in PngScanlineProcessor<TPixel> processor,
        int firstColumn,
        int columnIncrement)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool blend = frameControl.BlendMode == FrameBlendMode.Over;
        Span<TPixel> rowSpan = blend
            ? blendRowBuffer
            : destination;

        // The first byte is the filter type. The pass columns count from the frame x offset.
        processor.ProcessInterlacedScanline(
            scanline[1..],
            columns,
            passRowBuffer,
            rowSpan.Slice((int)frameControl.XOffset, (int)frameControl.Width),
            firstColumn,
            columnIncrement);

        if (blend)
        {
            PixelBlender<TPixel> blender = PixelBlenders<TPixel>.NormalSrcOver;

            blender.Blend<TPixel>(this.configuration, destination, destination, rowSpan, 1F);
        }
    }

    /// <summary>
    /// Creates the scanline processor of one frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control.</param>
    /// <param name="pngMetadata">The png metadata with the palette and the tRNS color.</param>
    /// <returns>The processor. The caller disposes it.</returns>
    private PngScanlineProcessor<TPixel> CreateScanlineProcessor<TPixel>(in FrameControl frameControl, PngMetadata pngMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
        => new(
            this.configuration,
            this.pngColorType,
            this.header.BitDepth,
            (int)frameControl.Width,
            pngMetadata.ColorTable,
            pngMetadata.TransparentColor);

    /// <summary>
    /// Assigns the palette to metadata and rejects indexed images without a complete palette entry.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format for converted palette entries.</typeparam>
    /// <param name="palette">The palette buffer.</param>
    /// <param name="alpha">The alpha palette buffer.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="profile">The embedded ICC profile.</param>
    private void AssignColorPalette<TPixel>(ReadOnlySpan<byte> palette, ReadOnlySpan<byte> alpha, PngMetadata pngMetadata, IccProfile? profile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Indexed samples require at least one complete RGB entry. Reject a missing table before scanline
        // recovery can suppress the error in IgnoreImageData mode. Other color types have an optional palette.
        if (palette.Length < Unsafe.SizeOf<Rgb24>())
        {
            if (this.pngColorType is PngColorType.Palette)
            {
                PngThrowHelper.ThrowMissingPalette();
            }

            return;
        }

        Color[] colorTable = new Color[palette.Length / Unsafe.SizeOf<Rgb24>()];
        ReadOnlySpan<Rgb24> rgbTable = MemoryMarshal.Cast<byte, Rgb24>(palette);

        // The tRNS chunk must not contain more alpha values than there are palette entries.
        if (alpha.Length > colorTable.Length)
        {
            alpha = alpha.Slice(0, colorTable.Length);
        }

        Color.FromPixel(rgbTable, colorTable);

        // The alpha chunk may contain as many transparency entries as there are palette entries
        // (more than that would not make any sense) or as few as one.
        for (int i = 0; i < alpha.Length; i++)
        {
            ref Color color = ref colorTable[i];
            color = color.WithAlpha(alpha[i] / 255F);
        }

        if (this.pngColorType == PngColorType.Palette && IccProfile.TryGetIccProfileForColorConversion(profile, out IccProfile? sourceProfile))
        {
            ColorConversionOptions options = new()
            {
                SourceIccProfile = sourceProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = this.memoryAllocator
            };

            ColorProfileConverter? converter = ColorProfileConverter.CreateIccForDecoding(options);

            // tRNS alpha is present before the destination pixel format packs the converted RGB.
            converter?.Convert<TPixel>(colorTable);
        }

        pngMetadata.ColorTable = colorTable;
    }

    /// <summary>
    /// Decodes and assigns marker colors that identify transparent pixels in non indexed images.
    /// </summary>
    /// <param name="alpha">The alpha tRNS buffer.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    private void AssignTransparentMarkers(ReadOnlySpan<byte> alpha, PngMetadata pngMetadata)
    {
        if (this.pngColorType == PngColorType.Rgb)
        {
            if (alpha.Length >= 6)
            {
                if (this.header.BitDepth == 16)
                {
                    // tRNS samples are stored big-endian like all other PNG multi-byte values,
                    // matching the byte order used for the 16-bit samples in the image data.
                    ushort rc = BinaryPrimitives.ReadUInt16BigEndian(alpha[..2]);
                    ushort gc = BinaryPrimitives.ReadUInt16BigEndian(alpha.Slice(2, 2));
                    ushort bc = BinaryPrimitives.ReadUInt16BigEndian(alpha.Slice(4, 2));

                    pngMetadata.TransparentColor = Color.FromPixel(new Rgb48(rc, gc, bc));
                    return;
                }

                byte r = ReadByteLittleEndian(alpha, 0);
                byte g = ReadByteLittleEndian(alpha, 2);
                byte b = ReadByteLittleEndian(alpha, 4);
                pngMetadata.TransparentColor = Color.FromPixel(new Rgb24(r, g, b));
            }
        }
        else if (this.pngColorType == PngColorType.Grayscale)
        {
            if (alpha.Length >= 2)
            {
                if (this.header.BitDepth == 16)
                {
                    pngMetadata.TransparentColor = Color.FromPixel(new L16(BinaryPrimitives.ReadUInt16BigEndian(alpha[..2])));
                }
                else
                {
                    pngMetadata.TransparentColor = Color.FromPixel(new L8(ReadByteLittleEndian(alpha, 0)));
                }
            }
        }
    }

    /// <summary>
    /// Reads a animation control chunk from the data.
    /// </summary>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
    private void ReadAnimationControlChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
    {
        this.animationControl = AnimationControl.Parse(data);

        pngMetadata.RepeatCount = this.animationControl.NumberPlays;
    }

    /// <summary>
    /// Reads the fcTL chunk that starts an animation frame, and applies the frame limit of the decode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each fcTL chunk starts one frame. When a frame exists, the fcTL chunk of the frame after <see cref="maxFrames"/> ends the read of the chunks. Before a
    /// frame exists, the decoder reads each fcTL chunk, so the default image always decodes.
    /// </para>
    /// <para>
    /// Before a frame exists, the frame control can define the pixel buffer of the image. Thus a damaged chunk at that point is a structural error in each
    /// mode. After a frame exists, a damaged chunk is damaged image data.
    /// </para>
    /// </remarks>
    /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> that contains the data.</param>
    /// <param name="imageExists">A value that tells if a frame exists.</param>
    /// <param name="frameCount">The number of fcTL chunks to this chunk, this chunk included.</param>
    /// <param name="frameControl">The frame control. If the return value is <see langword="false"/>, the default value.</param>
    /// <returns>
    /// <see langword="true"/> if the chunk starts a frame. <see langword="false"/> if the frame is past the frame limit, or if the chunk is damaged and the
    /// mode ignores image data errors. The decoder then reads no more chunks.
    /// </returns>
    private bool TryReadFrameControlChunk(ReadOnlySpan<byte> data, bool imageExists, uint frameCount, out FrameControl frameControl)
    {
        frameControl = default;
        if (frameCount > this.maxFrames && imageExists)
        {
            return false;
        }

        string message = "The frame control chunk does not contain enough data!";
        if (data.Length >= FrameControl.Size)
        {
            FrameControl parsed = FrameControl.Parse(data);
            if (parsed.IsValid(this.header))
            {
                frameControl = parsed;
                return true;
            }

            message = "The frame control chunk has a frame region outside the image, or a dispose or blend operation that is not valid.";
        }

        this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, message);
        return false;
    }

    /// <summary>
    /// Reads a header chunk from the data.
    /// </summary>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
    private void ReadHeaderChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
    {
        if (this.hasHeader)
        {
            PngThrowHelper.ThrowInvalidHeader();
        }

        this.header = PngHeader.Parse(data);

        this.header.Validate();
        this.hasHeader = true;

        pngMetadata.BitDepth = (PngBitDepth)this.header.BitDepth;
        pngMetadata.ColorType = this.header.ColorType;
        pngMetadata.InterlaceMethod = this.header.InterlaceMethod;

        this.pngColorType = this.header.ColorType;
        this.Dimensions = new Size(this.header.Width, this.header.Height);
    }

    /// <summary>
    /// Checks the chunks before the first IDAT chunk, and gets the frame control of the default image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apple's pngcrush writes the CgBI chunk before IHDR, and other tools can write it after IHDR. Both chunks come before the first IDAT chunk, so the
    /// decoder checks the CgBI header there, before it allocates the image.
    /// </para>
    /// <para>
    /// An fcTL chunk before the first IDAT chunk makes the default image the first frame. Its frame region is the whole image: offset 0, and the width and
    /// height of IHDR. See PNG Third Edition, section 11.3.6.2 "fcTL Frame Control Chunk": https://www.w3.org/TR/png-3/#fcTL-chunk.
    /// </para>
    /// </remarks>
    /// <param name="frameControl">The fcTL chunk before the first IDAT chunk, or <see langword="null"/>.</param>
    /// <returns>The frame control of the default image.</returns>
    private FrameControl GetDefaultImageFrameControl(FrameControl? frameControl)
    {
        if (this.isCgbi && (this.header.BitDepth != 8 || (this.header.ColorType is not PngColorType.Rgb and not PngColorType.RgbWithAlpha)))
        {
            PngThrowHelper.ThrowInvalidImageContentException(
                $"CgBI is only supported for 8-bit truecolor images. Was bit depth '{this.header.BitDepth}', color type '{this.header.ColorType}'.");
        }

        if (frameControl is null)
        {
            return new FrameControl((uint)this.header.Width, (uint)this.header.Height);
        }

        FrameControl value = frameControl.Value;
        if (value.XOffset != 0 || value.YOffset != 0 || value.Width != (uint)this.header.Width || value.Height != (uint)this.header.Height)
        {
            PngThrowHelper.ThrowInvalidImageContentException("The frame control chunk of the default image must have offset 0 and the image size.");
        }

        return value;
    }

    /// <summary>
    /// Reads a text chunk containing image properties from the data.
    /// </summary>
    /// <param name="baseMetadata">The <see cref="ImageMetadata"/> object.</param>
    /// <param name="metadata">The metadata to decode to.</param>
    /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
    private void ReadTextChunk(ImageMetadata baseMetadata, PngMetadata metadata, ReadOnlySpan<byte> data)
    {
        if (this.skipMetadata)
        {
            return;
        }

        int zeroIndex = data.IndexOf((byte)0);

        // Keywords are restricted to 1 to 79 bytes in length.
        if (zeroIndex is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
            return;
        }

        ReadOnlySpan<byte> keywordBytes = data[..zeroIndex];
        if (!TryReadTextKeyword(keywordBytes, out string name))
        {
            return;
        }

        string value = PngConstants.Encoding.GetString(data[(zeroIndex + 1)..]);

        if (!TryReadTextChunkMetadata(baseMetadata, name, value))
        {
            metadata.TextData.Add(new PngTextData(name, value, string.Empty, string.Empty));
        }
    }

    /// <summary>
    /// Reads the compressed text chunk. Contains a uncompressed keyword and a compressed text string.
    /// </summary>
    /// <param name="baseMetadata">The <see cref="ImageMetadata"/> object.</param>
    /// <param name="metadata">The metadata to decode to.</param>
    /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
    private void ReadCompressedTextChunk(ImageMetadata baseMetadata, PngMetadata metadata, ReadOnlySpan<byte> data)
    {
        if (this.skipMetadata)
        {
            return;
        }

        int keywordEnd = data.IndexOf((byte)0);
        if (keywordEnd is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
            return;
        }

        if (keywordEnd + 2 > data.Length)
        {
            return; // Not enough data for keyword + null + compression method.
        }

        byte compressionMethod = data[keywordEnd + 1];
        if (compressionMethod != 0)
        {
            // Only compression method 0 is supported (zlib datastream with deflate compression).
            return;
        }

        ReadOnlySpan<byte> keywordBytes = data[..keywordEnd];
        if (!TryReadTextKeyword(keywordBytes, out string name))
        {
            return;
        }

        ReadOnlySpan<byte> compressedData = data[(keywordEnd + 2)..];

        if (this.TryDecompressTextData(compressedData, PngConstants.Encoding, out string? uncompressed)
            && !TryReadTextChunkMetadata(baseMetadata, name, uncompressed))
        {
            metadata.TextData.Add(new PngTextData(name, uncompressed, string.Empty, string.Empty));
        }
    }

    /// <summary>
    /// Checks if the given text chunk is actually storing parsable metadata.
    /// </summary>
    /// <param name="baseMetadata">The <see cref="ImageMetadata"/> object to store the parsed metadata in.</param>
    /// <param name="chunkName">The name of the text chunk.</param>
    /// <param name="chunkText">The contents of the text chunk.</param>
    /// <returns>True if the text chunk is a legacy profile chunk. The decoder stores a valid profile and discards an
    /// invalid one, and never keeps the chunk as text. False if the text chunk is not a legacy profile chunk, and should
    /// be stored in the metadata object unmodified.</returns>
    private static bool TryReadTextChunkMetadata(ImageMetadata baseMetadata, string chunkName, string chunkText)
    {
        // A legacy profile chunk holds metadata, not text. A valid profile becomes the image profile.
        // An invalid profile is damaged metadata, so the decoder discards it.
        if (chunkName.Equals(PngConstants.ExifRawProfileKeyword, StringComparison.OrdinalIgnoreCase))
        {
            if (ExifReader.ReadLegacyText(chunkText) is ExifProfile exifProfile)
            {
                MergeOrSetExifProfile(baseMetadata, exifProfile, replaceExistingKeys: false);
            }

            return true;
        }

        if (chunkName.Equals(PngConstants.IptcRawProfileKeyword, StringComparison.OrdinalIgnoreCase))
        {
            // Keep the first IPTC profile.
            if (baseMetadata.IptcProfile == null)
            {
                baseMetadata.TrySetIptcProfile(IptcReader.ReadLegacyText(chunkText));
            }

            return true;
        }

        // No special chunk data identified
        return false;
    }

    /// <summary>
    /// Reads the CICP color profile chunk.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="data">The profile data. It has 4 bytes.</param>
    private static void ReadCicpChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        byte colorPrimaries = data[0];
        byte transferFunction = data[1];
        byte matrixCoefficients = data[2];
        bool? fullRange;
        if (data[3] == 1)
        {
            fullRange = true;
        }
        else if (data[3] == 0)
        {
            fullRange = false;
        }
        else
        {
            fullRange = null;
        }

        metadata.CicpProfile = new CicpProfile(colorPrimaries, transferFunction, matrixCoefficients, fullRange);
    }

    /// <summary>
    /// Reads an eXIf chunk and gives its tags precedence over earlier EXIF metadata.
    /// </summary>
    /// <param name="metadata">The image metadata to update.</param>
    /// <param name="chunk">The eXIf chunk containing the profile bytes.</param>
    private void ReadExifChunk(ImageMetadata metadata, PngChunk chunk)
    {
        if (!this.skipMetadata)
        {
            ExifProfile profile = ExifReader.ReadProfile(chunk.Data.GetSpan()[..chunk.Length]);
            MergeOrSetExifProfile(metadata, profile, replaceExistingKeys: true);
        }
    }

    /// <summary>
    /// Reads the color profile chunk. The data is stored similar to the zTXt chunk.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="data">The bytes containing the profile.</param>
    private unsafe void ReadColorProfileChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        int zeroIndex = data.IndexOf((byte)0);
        if (zeroIndex is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
            return;
        }

        if (zeroIndex + 1 >= data.Length)
        {
            this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read PNG ICC compression method.");
            return;
        }

        byte compressionMethod = data[zeroIndex + 1];
        if (compressionMethod != 0)
        {
            // Only compression method 0 is supported (zlib datastream with deflate compression).
            return;
        }

        ReadOnlySpan<byte> keywordBytes = data[..zeroIndex];
        if (!TryReadTextKeyword(keywordBytes, out string name))
        {
            return;
        }

        ReadOnlySpan<byte> compressedData = data[(zeroIndex + 2)..];

        fixed (byte* compressedDataBase = compressedData)
        {
            using UnmanagedMemoryStream memoryStreamInput = new(compressedDataBase, compressedData.Length);
            using BufferedReadStream bufferedStream = new(this.configuration, memoryStreamInput);
            using ZlibInflateReader inflateStream = new(bufferedStream);

            if (!inflateStream.AllocateNewBytes(compressedData.Length))
            {
                this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG ICC profile.");
                return;
            }

            try
            {
                IccReader reader = new(this.maxUncompressedLength);
                int profileLength = Math.Min(this.maxUncompressedLength, IccProfile.MaxProfileSize - 1);
                int bytesRead = reader.Read(inflateStream.CompressedStream, profileLength);

                // Probe once only at the limit to distinguish exact output from excess decompressed data.
                if (bytesRead == profileLength && inflateStream.CompressedStream.ReadByte() != -1)
                {
                    this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid PNG ICC profile.");
                    return;
                }

                metadata.IccProfile = reader.ToProfile(this.segmentIntegrityHandling.IgnoresAncillaryErrors());
            }
            catch (InvalidDataException ex)
            {
                this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG ICC profile.", ex);
            }
            catch (InvalidIccProfileException ex)
            {
                // The ICC reader rejects a profile that is too large or not valid. The profile is ancillary data.
                this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid PNG ICC profile.", ex);
            }
        }
    }

    /// <summary>
    /// Tries to decompress zlib compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data.</param>
    /// <param name="maxLength">The maximum uncompressed length.</param>
    /// <param name="uncompressedBytesArray">The uncompressed bytes array.</param>
    /// <returns>True, if de-compressing was successful.</returns>
    private unsafe bool TryDecompressZlibData(ReadOnlySpan<byte> compressedData, int maxLength, out byte[] uncompressedBytesArray)
    {
        fixed (byte* compressedDataBase = compressedData)
        {
            using IMemoryOwner<byte> destBuffer = this.memoryAllocator.Allocate<byte>(this.configuration.StreamProcessingBufferSize);
            using ChunkedMemoryStream memoryStreamOutput = new(this.memoryAllocator);
            using UnmanagedMemoryStream memoryStreamInput = new(compressedDataBase, compressedData.Length);
            using BufferedReadStream bufferedStream = new(this.configuration, memoryStreamInput);
            using ZlibInflateReader inflateStream = new(bufferedStream);

            Span<byte> destUncompressedData = destBuffer.GetSpan();
            if (!inflateStream.AllocateNewBytes(compressedData.Length))
            {
                uncompressedBytesArray = [];
                return false;
            }

            try
            {
                int bytesRead = inflateStream.CompressedStream.Read(destUncompressedData);
                while (bytesRead != 0)
                {
                    if (memoryStreamOutput.Length + bytesRead > maxLength)
                    {
                        uncompressedBytesArray = [];
                        return false;
                    }

                    memoryStreamOutput.Write(destUncompressedData[..bytesRead]);
                    bytesRead = inflateStream.CompressedStream.Read(destUncompressedData);
                }

                uncompressedBytesArray = memoryStreamOutput.ToArray();
                return true;
            }
            catch (InvalidDataException ex)
            {
                // ICC and text chunks are already bounded in memory, so rejecting their compressed contents
                // does not lose the next chunk boundary. Apply the ancillary policy without keeping partial output.
                this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG metadata.", ex);
                uncompressedBytesArray = [];
                return false;
            }
        }
    }

    /// <summary>
    /// Sets the <see cref="ExifProfile"/> in <paramref name="metadata"/> to <paramref name="newProfile"/>,
    /// or copies exif tags if <paramref name="metadata"/> already contains an <see cref="ExifProfile"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="ImageMetadata"/> to store the exif data in.</param>
    /// <param name="newProfile">The <see cref="ExifProfile"/> to copy exif tags from.</param>
    /// <param name="replaceExistingKeys">If <paramref name="metadata"/> already contains an <see cref="ExifProfile"/>,
    /// controls whether existing exif tags in <paramref name="metadata"/> will be overwritten with any conflicting
    /// tags from <paramref name="newProfile"/>.</param>
    private static void MergeOrSetExifProfile(ImageMetadata metadata, ExifProfile newProfile, bool replaceExistingKeys)
    {
        if (metadata.ExifProfile is null)
        {
            // No exif metadata was loaded yet, so just assign it
            metadata.TrySetExifProfile(newProfile);
        }
        else
        {
            // Try to merge existing keys with the ones from the new profile
            foreach (IExifValue newKey in newProfile.Values)
            {
                if (replaceExistingKeys || metadata.ExifProfile.GetValueInternal(newKey.Tag) is null)
                {
                    metadata.ExifProfile.SetValueInternal(newKey.Tag, newKey.GetValue());
                }
            }
        }
    }

    /// <summary>
    /// Reads a iTXt chunk, which contains international text data. It contains:
    /// - A uncompressed keyword.
    /// - Compression flag, indicating if a compression is used.
    /// - Compression method.
    /// - Language tag (optional).
    /// - A translated keyword (optional).
    /// - Text data, which is either compressed or uncompressed.
    /// </summary>
    /// <param name="metadata">The metadata to decode to.</param>
    /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
    private void ReadInternationalTextChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        if (this.skipMetadata)
        {
            return;
        }

        PngMetadata pngMetadata = metadata.GetPngMetadata();
        int zeroIndexKeyword = data.IndexOf((byte)0);
        if (zeroIndexKeyword is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
            return;
        }

        if (zeroIndexKeyword + 4 > data.Length)
        {
            return; // Not enough data for keyword + null + flag + method + language.
        }

        byte compressionFlag = data[zeroIndexKeyword + 1];
        if (compressionFlag is not (0 or 1))
        {
            return;
        }

        byte compressionMethod = data[zeroIndexKeyword + 2];
        if (compressionMethod != 0)
        {
            // Only compression method 0 is supported (zlib datastream with deflate compression).
            return;
        }

        int langStartIdx = zeroIndexKeyword + 3;
        int languageLength = data[langStartIdx..].IndexOf((byte)0);
        if (languageLength < 0)
        {
            return;
        }

        string language = PngConstants.LanguageEncoding.GetString(data.Slice(langStartIdx, languageLength));

        int translatedKeywordStartIdx = langStartIdx + languageLength + 1;
        int translatedKeywordLength = data[translatedKeywordStartIdx..].IndexOf((byte)0);
        if (translatedKeywordLength < 0)
        {
            return;
        }

        string translatedKeyword = PngConstants.TranslatedEncoding.GetString(data.Slice(translatedKeywordStartIdx, translatedKeywordLength));

        ReadOnlySpan<byte> keywordBytes = data[..zeroIndexKeyword];
        if (!TryReadTextKeyword(keywordBytes, out string keyword))
        {
            return;
        }

        int dataStartIdx = translatedKeywordStartIdx + translatedKeywordLength + 1;
        if (compressionFlag == 1)
        {
            ReadOnlySpan<byte> compressedData = data[dataStartIdx..];

            if (this.TryDecompressTextData(compressedData, PngConstants.TranslatedEncoding, out string? uncompressed))
            {
                pngMetadata.TextData.Add(new PngTextData(keyword, uncompressed, language, translatedKeyword));
            }
        }
        else if (IsXmpTextData(keywordBytes))
        {
            metadata.XmpProfile = XmpReader.ReadProfile(data[dataStartIdx..]) ?? metadata.XmpProfile;
        }
        else
        {
            string value = PngConstants.TranslatedEncoding.GetString(data[dataStartIdx..]);
            pngMetadata.TextData.Add(new PngTextData(keyword, value, language, translatedKeyword));
        }
    }

    /// <summary>
    /// Decompresses a byte array with zlib compressed text data.
    /// </summary>
    /// <param name="compressedData">Compressed text data bytes.</param>
    /// <param name="encoding">The string encoding to use.</param>
    /// <param name="value">The uncompressed value.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    private bool TryDecompressTextData(ReadOnlySpan<byte> compressedData, Encoding encoding, [NotNullWhen(true)] out string? value)
    {
        if (this.TryDecompressZlibData(compressedData, this.maxUncompressedLength, out byte[] uncompressedData))
        {
            value = encoding.GetString(uncompressedData);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Reads the header of the next chunk of a compressed stream, and moves the stream to the compressed data in it.
    /// </summary>
    /// <remarks>
    /// The compressed stream of the default image is the data of the consecutive IDAT chunks. The compressed stream of an animation frame is the data of the
    /// consecutive fdAT chunks, after the 4-byte sequence number of each chunk. See PNG Third Edition, section 11.2.3 "IDAT Image data" and section 11.3.6.3
    /// "fdAT Frame Data Chunk". A chunk can have no compressed data. The method skips such a chunk, because a return value of 0 tells the inflater that the
    /// compressed stream ended. <see cref="imageDataChunkType"/> gives the chunk type of the stream. A chunk of another type, or a damaged fdAT chunk, ends the
    /// compressed stream. <see cref="nextChunk"/> then keeps that chunk for the chunk loop.
    /// </remarks>
    /// <returns>The number of compressed bytes in the next chunk, or 0 if the compressed stream has no more chunks.</returns>
    private int ReadNextDataChunk()
    {
        if (this.nextChunk != null)
        {
            return 0;
        }

        Span<byte> buffer = stackalloc byte[20];
        PngChunkType type = this.imageDataChunkType;
        int sequenceLength = type is PngChunkType.FrameData ? sizeof(uint) : 0;
        while (true)
        {
            // The stream is at the end of the data of the previous chunk. The method skips its CRC.
            this.imageDataChunkEnd = -1;
            if (this.currentStream.Read(buffer, 0, 4) == 0 || !this.TryReadChunk(buffer, out PngChunk chunk))
            {
                return 0;
            }

            if (chunk.Type != type || (sequenceLength > 0 && !this.TryValidateFrameDataChunk(chunk)))
            {
                this.nextChunk = chunk;
                return 0;
            }

            this.currentStream.Skip(sequenceLength);
            int dataLength = chunk.Length - sequenceLength;
            if (dataLength > 0)
            {
                this.imageDataChunkEnd = this.currentStream.Position + dataLength;
                return dataLength;
            }
        }
    }

    /// <summary>
    /// Skips the data of an fdAT chunk, and the data of each fdAT chunk after it.
    /// </summary>
    /// <remarks>
    /// The consecutive fdAT chunks hold the compressed stream of one frame. The method reads them through <see cref="ReadNextDataChunk"/>, as the inflater
    /// does, so <see cref="nextChunk"/> then keeps the chunk after them.
    /// </remarks>
    /// <param name="chunk">The fdAT chunk. The stream is at the start of its data.</param>
    private void SkipFrameData(in PngChunk chunk)
    {
        this.currentStream.Skip(chunk.Length);
        this.imageDataChunkType = PngChunkType.FrameData;

        int length;
        while ((length = this.ReadNextDataChunk()) > 0)
        {
            this.currentStream.Skip(length);
        }
    }

    /// <summary>
    /// Checks an fdAT chunk of the chunk loop before the decoder reads the frame data in it.
    /// </summary>
    /// <param name="chunk">The fdAT chunk. The stream is at the start of its data.</param>
    /// <param name="frameStarted">A value that tells if an earlier fdAT chunk started the data of the current frame.</param>
    /// <returns>
    /// <see langword="true"/> if the chunk starts the data of the current frame. Otherwise <see langword="false"/>, and the method skipped the chunk.
    /// </returns>
    private bool TryStartFrameData(in PngChunk chunk, bool frameStarted)
    {
        if (!this.TryValidateFrameDataChunk(chunk))
        {
            // The damaged chunk breaks the compressed data of its frame. The fdAT chunks after it hold only a part of that data, so the decoder skips them too.
            this.SkipFrameData(chunk);
            return false;
        }

        if (frameStarted)
        {
            // The compressed stream of the current frame starts in an earlier fdAT chunk, and the stream ended. This chunk holds only data after the end of
            // that stream.
            this.SkipChunkDataAndCrc(chunk);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks the sequence number of an fdAT chunk.
    /// </summary>
    /// <remarks>
    /// An fdAT chunk starts with a 4-byte sequence number. See PNG Third Edition, section 11.3.6.3 "fdAT Frame Data Chunk":
    /// https://www.w3.org/TR/png-3/#fdAT-chunk. The default image comes before each fdAT chunk, so a chunk without a sequence number is damaged image data.
    /// </remarks>
    /// <param name="chunk">The fdAT chunk.</param>
    /// <returns>
    /// <see langword="true"/> if the chunk has a sequence number. <see langword="false"/> if it does not and the mode ignores image data errors. The caller
    /// then skips the chunk.
    /// </returns>
    private bool TryValidateFrameDataChunk(in PngChunk chunk)
    {
        if (chunk.Length >= sizeof(uint))
        {
            return true;
        }

        this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError("The frame data chunk does not contain a sequence number.");
        return false;
    }

    /// <summary>
    /// Reads the next chunk that the decoder uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method reads the data of the chunk and checks its CRC. The inflater reads the data of IDAT and fdAT chunks from the stream. Thus the stream stays at
    /// the start of the data of these chunks.
    /// </para>
    /// <para>
    /// The method skips each chunk that the decoder does not use (see <see cref="IsUsedChunk"/>). It reads no data of such a chunk into memory. When the mode
    /// checks the CRC of the chunk, the method streams the data through the CRC. Otherwise it moves the stream past the data and the CRC. The method also skips
    /// an ancillary chunk with a data length that is not valid for its type.
    /// </para>
    /// </remarks>
    /// <param name="buffer">A temporary buffer of at least 8 bytes.</param>
    /// <param name="chunk">The chunk. If the return value is <see langword="false"/>, the default value.</param>
    /// <returns><see langword="true"/> if the method reads a chunk. <see langword="false"/> if no chunk follows.</returns>
    private bool TryReadChunk(Span<byte> buffer, out PngChunk chunk)
    {
        if (this.nextChunk != null)
        {
            chunk = this.nextChunk.Value;

            this.nextChunk = null;

            return true;
        }

        while (this.TryReadChunkHeader(buffer, out int length, out PngChunkType type))
        {
            // IHDR is the first chunk. See PNG Third Edition, section 5.6 "Chunk ordering". Apple's pngcrush writes the CgBI chunk before IHDR.
            if (!this.hasHeader && type is not PngChunkType.Header and not PngChunkType.ProprietaryApple)
            {
                PngThrowHelper.ThrowInvalidHeader();
            }

            // The decoder cannot decode an image with a critical chunk that it does not know. See PNG Third Edition, section 5.4 "Chunk naming conventions".
            if (!IsAncillary(type)
                && type is not PngChunkType.Header and not PngChunkType.Palette and not PngChunkType.Data and not PngChunkType.End
                and not PngChunkType.ProprietaryApple)
            {
                PngThrowHelper.ThrowUnknownCriticalChunk(GetChunkTypeName(type));
            }

            long available = this.currentStream.Length - this.currentStream.Position;
            if (type is PngChunkType.Data or PngChunkType.FrameData)
            {
                // The inflater reads the data later. Data that ends early is truncated image data, and the scanline reader reports it. The color metadata read
                // stops at the first image data chunk, so it needs no CRC.
                chunk = new PngChunk((int)Math.Min(length, available), type);
                if (!this.colorMetadataOnly && chunk.IsCritical(this.segmentIntegrityHandling))
                {
                    long dataStart = this.currentStream.Position;
                    this.ValidateChunkCrc(chunk, buffer);
                    this.currentStream.Position = dataStart;
                }

                return true;
            }

            if (!this.IsUsedChunk(type))
            {
                // The color metadata read checks no CRC, as for the image data chunks. The decode checks the CRC without a copy of the data. Data that ends
                // early fails the CRC check.
                PngChunk skipped = new(length, type);
                if (!this.colorMetadataOnly && skipped.IsCritical(this.segmentIntegrityHandling))
                {
                    this.ValidateChunkCrc(skipped, buffer);
                }
                else
                {
                    this.currentStream.Skip(length);
                    this.currentStream.Skip(4);
                }

                continue;
            }

            // The decoder needs the data of a critical chunk. A critical chunk with complete data and a missing CRC reports a CRC error in the CRC check below.
            // An ancillary chunk also needs its 4 CRC bytes, so that a profile or text from a truncated stream does not reach the metadata.
            if (length > (IsAncillary(type) ? available - sizeof(uint) : available))
            {
                // No chunk follows a chunk that ends after the end of the stream.
                string message = $"The PNG {GetChunkTypeName(type)} chunk ends after the end of the stream.";
                if (!IsAncillary(type))
                {
                    PngThrowHelper.ThrowInvalidImageContentException(message);
                }

                this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError(message);
                this.currentStream.Position = this.currentStream.Length;
                break;
            }

            chunk = new PngChunk(length, type, this.ReadChunkData(length));

            try
            {
                if (chunk.IsCritical(this.segmentIntegrityHandling))
                {
                    this.ValidateChunkCrc(chunk, buffer);
                }
                else
                {
                    this.currentStream.Skip(4);
                }
            }
            catch
            {
                // The caller owns the chunk data only after this method returns. The CRC read can throw first, for example when the stream observes a
                // cancellation request. Thus the method disposes the data here.
                chunk.Data.Dispose();
                throw;
            }

            if (IsValidChunkLength(type, length))
            {
                return true;
            }

            // The CRC check comes first, so a chunk with damaged bytes reports the CRC error. A length error stops the decode for a critical chunk, and skips
            // an ancillary chunk.
            chunk.Data.Dispose();
            string lengthMessage = $"The PNG {GetChunkTypeName(type)} chunk has a data length of {length}, which is not valid for the chunk type.";
            if (!IsAncillary(type))
            {
                PngThrowHelper.ThrowInvalidImageContentException(lengthMessage);
            }

            this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError(lengthMessage);
        }

        chunk = default;
        return false;
    }

    /// <summary>
    /// Gets a value that tells if the decoder uses the data of a chunk type. The IDAT and fdAT chunks are not part of the check, because the inflater reads
    /// their data from the stream.
    /// </summary>
    /// <remarks>
    /// The color metadata read uses only the chunks that define the pixel format and the frames: IHDR, PLTE, tRNS, acTL and fcTL. The decode also uses the
    /// metadata chunks, IEND and CgBI.
    /// </remarks>
    /// <param name="type">The chunk type.</param>
    /// <returns><see langword="true"/> if the decoder uses the data. Otherwise <see langword="false"/>.</returns>
    private bool IsUsedChunk(PngChunkType type)
        => type is PngChunkType.Header or PngChunkType.Palette or PngChunkType.Transparency or PngChunkType.AnimationControl
            or PngChunkType.FrameControl
        || (!this.colorMetadataOnly
            && type is PngChunkType.Physical or PngChunkType.Gamma or PngChunkType.Cicp or PngChunkType.Text or PngChunkType.CompressedText
                or PngChunkType.InternationalText or PngChunkType.Exif or PngChunkType.EmbeddedColorProfile or PngChunkType.End
                or PngChunkType.ProprietaryApple);

    /// <summary>
    /// Reads the length and the type of the next chunk.
    /// </summary>
    /// <remarks>
    /// The length is at most 2^31-1, and each byte of the type is an ASCII letter. See PNG Third Edition, section 5.3 "Chunk layout":
    /// https://www.w3.org/TR/png-3/#5Chunk-layout. The <see cref="SegmentIntegrityHandling.Strict"/> mode stops with an error for a header that is not valid.
    /// The other modes look for the next valid header, one byte at a time, from the byte after the start of the damaged header.
    /// </remarks>
    /// <param name="buffer">A temporary buffer of at least 8 bytes.</param>
    /// <param name="length">The data length of the chunk.</param>
    /// <param name="type">The chunk type.</param>
    /// <returns><see langword="true"/> if the method reads a header. <see langword="false"/> at the end of the stream.</returns>
    private bool TryReadChunkHeader(Span<byte> buffer, out int length, out PngChunkType type)
    {
        while (true)
        {
            long position = this.currentStream.Position;
            if (this.currentStream.Read(buffer, 0, 8) != 8)
            {
                length = 0;
                type = default;
                return false;
            }

            length = BinaryPrimitives.ReadInt32BigEndian(buffer);
            type = (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
            if (length >= 0 && IsValidChunkType(type))
            {
                return true;
            }

            this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The PNG chunk header is not valid.");
            this.currentStream.Position = position + 1;
        }
    }

    /// <summary>
    /// Gets a value that tells if a data length is valid for the chunk type.
    /// </summary>
    /// <remarks>
    /// The limits come from the chunk layouts of PNG Third Edition, section 11 "Chunk specifications", for each chunk that the decoder reads. The IHDR, acTL,
    /// pHYs, gAMA and cICP chunks have a fixed length. A tRNS chunk holds at most one alpha value for each of the 256 palette entries. A tEXt chunk holds a
    /// keyword of at least 1 byte and a null separator. A zTXt or iCCP chunk holds a keyword of at least 1 byte, a null separator, the compression method and a
    /// zlib stream. An iTXt chunk holds a keyword of at least 1 byte, a null separator, 2 compression bytes and 2 more null separators. The first 4 bytes of
    /// an eXIf chunk hold the byte order and the integer 42. The minimum length does not include the zlib stream. The zlib decoder reports a short zlib
    /// stream. The decoder checks the fcTL and fdAT chunks as image data, in <see cref="TryReadFrameControlChunk"/> and
    /// <see cref="TryValidateFrameDataChunk"/>.
    /// </remarks>
    /// <param name="type">The chunk type.</param>
    /// <param name="length">The data length of the chunk.</param>
    /// <returns><see langword="true"/> if the length is valid. Otherwise <see langword="false"/>.</returns>
    private static bool IsValidChunkLength(PngChunkType type, int length)
        => type switch
        {
            PngChunkType.Header => length == PngHeader.Size,
            PngChunkType.AnimationControl => length == AnimationControl.Size,
            PngChunkType.Physical => length == PngPhysical.Size,
            PngChunkType.Gamma or PngChunkType.Cicp => length == 4,
            PngChunkType.Transparency => length <= 256,
            PngChunkType.Text => length >= 2,
            PngChunkType.CompressedText or PngChunkType.EmbeddedColorProfile => length >= 3,
            PngChunkType.InternationalText => length >= 6,
            PngChunkType.Exif => length >= 4,
            _ => true,
        };

    /// <summary>
    /// Gets a value that tells if a chunk type is ancillary.
    /// </summary>
    /// <remarks>
    /// Bit 5 of the first type byte is the ancillary bit, so an ancillary chunk type starts with a lowercase letter. See PNG Third Edition, section 5.4 "Chunk
    /// naming conventions". The type value holds the first type byte in its high byte.
    /// </remarks>
    /// <param name="type">The chunk type.</param>
    /// <returns><see langword="true"/> if the chunk type is ancillary. Otherwise <see langword="false"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool IsAncillary(PngChunkType type) => ((uint)type & 0x2000_0000U) != 0;

    /// <summary>
    /// Determines whether the 4-byte chunk type is valid (all ASCII letters).
    /// </summary>
    /// <param name="type">The chunk type.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool IsValidChunkType(PngChunkType type)
    {
        uint value = (uint)type;
        byte b0 = (byte)(value >> 24);
        byte b1 = (byte)(value >> 16);
        byte b2 = (byte)(value >> 8);
        byte b3 = (byte)value;
        return IsAsciiLetter(b0) && IsAsciiLetter(b1) && IsAsciiLetter(b2) && IsAsciiLetter(b3);
    }

    /// <summary>
    /// Returns a value indicating whether the given byte is an ASCII letter.
    /// </summary>
    /// <param name="b">The byte to check.</param>
    /// <returns>
    /// <see langword="true"/> if the byte is an ASCII letter. Otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool IsAsciiLetter(byte b)
        => (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z');

    /// <summary>
    /// Gets the four letters of a chunk type for an error message.
    /// </summary>
    /// <param name="type">The chunk type.</param>
    /// <returns>The four letters.</returns>
    private static string GetChunkTypeName(PngChunkType type)
    {
        Span<byte> name = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(name, (uint)type);
        return Encoding.ASCII.GetString(name);
    }

    /// <summary>
    /// Checks the CRC of a chunk, and reads the CRC from the stream.
    /// </summary>
    /// <remarks>
    /// The CRC covers the chunk type and the chunk data. See PNG Third Edition, section 5.3 "Chunk layout". A chunk with no data in memory, an image data chunk
    /// or a chunk that the decoder skips, has its data in the stream. The method then reads the data through <see cref="dataCrcBuffer"/> in blocks of the
    /// stream buffer size. Each block is one copy from the stream buffer.
    /// </remarks>
    /// <param name="chunk">
    /// The chunk. For a chunk with data in memory, the stream is at the CRC. For a chunk with no data in memory, the stream is at the start of the data.
    /// </param>
    /// <param name="buffer">A temporary buffer of at least 4 bytes.</param>
    private void ValidateChunkCrc(in PngChunk chunk, Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)chunk.Type);
        this.crc32.Reset();
        this.crc32.Append(buffer[..4]);

        if (chunk.Data is not null)
        {
            this.crc32.Append(chunk.Data.GetSpan());
        }
        else
        {
            this.dataCrcBuffer ??= this.configuration.MemoryAllocator.Allocate<byte>(this.configuration.StreamProcessingBufferSize);
            Span<byte> block = this.dataCrcBuffer.GetSpan();
            int remaining = chunk.Length;
            while (remaining > 0)
            {
                int read = this.currentStream.Read(block[..Math.Min(remaining, block.Length)]);
                if (read <= 0)
                {
                    break;
                }

                this.crc32.Append(block[..read]);
                remaining -= read;
            }
        }

        if (this.crc32.GetCurrentHashAsUInt32() != this.ReadChunkCrc(buffer))
        {
            PngThrowHelper.ThrowInvalidChunkCrc(GetChunkTypeName(chunk.Type));
        }
    }

    /// <summary>
    /// Reads the cycle redundancy chunk from the data.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private uint ReadChunkCrc(Span<byte> buffer)
    {
        uint crc = 0;
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        return crc;
    }

    /// <summary>
    /// Skips the chunk data and the cycle redundancy chunk read from the data.
    /// </summary>
    /// <param name="chunk">The image format chunk.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void SkipChunkDataAndCrc(in PngChunk chunk)
    {
        this.currentStream.Skip(chunk.Length);
        this.currentStream.Skip(4);
    }

    /// <summary>
    /// Reads the chunk data from the stream.
    /// </summary>
    /// <param name="length">The length of the chunk data to read. The stream holds at least this number of bytes.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private IMemoryOwner<byte> ReadChunkData(int length)
    {
        if (length == 0)
        {
            return new BasicArrayBuffer<byte>([]);
        }

        // We rent the buffer here to return it afterwards in Decode()
        IMemoryOwner<byte> buffer = this.configuration.MemoryAllocator.Allocate<byte>(length, AllocationOptions.Clean);

        try
        {
            this.currentStream.Read(buffer.GetSpan(), 0, length);
        }
        catch
        {
            // The read can throw before the caller takes ownership of the buffer (e.g. the stream
            // observes a cancellation request), so dispose it here to avoid leaking the rented memory.
            buffer.Dispose();
            throw;
        }

        return buffer;
    }

    /// <summary>
    /// Tries to reads a text chunk keyword, which have some restrictions to be valid:
    /// Keywords shall contain only printable Latin-1 characters and should not have leading or trailing whitespace.
    /// See: https://www.w3.org/TR/PNG/#11zTXt
    /// </summary>
    /// <param name="keywordBytes">The keyword bytes.</param>
    /// <param name="name">The name.</param>
    /// <returns>True, if the keyword could be read and is valid.</returns>
    private static bool TryReadTextKeyword(ReadOnlySpan<byte> keywordBytes, out string name)
    {
        name = string.Empty;

        // Keywords shall contain only printable Latin-1.
        foreach (byte c in keywordBytes)
        {
            if (c is not ((>= 32 and <= 126) or (>= 161 and <= 255)))
            {
                return false;
            }
        }

        // Keywords should not be empty or have leading or trailing whitespace.
        name = PngConstants.Encoding.GetString(keywordBytes);
        return !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith(' ') && !name.EndsWith(' ');
    }

    private static bool IsXmpTextData(ReadOnlySpan<byte> keywordBytes)
        => keywordBytes.SequenceEqual(PngConstants.XmpKeyword);

    private void SwapScanlineBuffers()
        => (this.scanline, this.previousScanline) = (this.previousScanline, this.scanline);
}
