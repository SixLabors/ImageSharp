// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
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
                            frameCount++;
                            currentFrame = null;
                            currentFrameControl = this.ReadFrameControlChunk(chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameData:
                        {
                            ValidateFrameDataChunk(chunk);

                            if (frameCount > this.maxFrames)
                            {
                                goto EOF;
                            }

                            if (currentFrame is not null)
                            {
                                // The frame of the current fcTL chunk already read its zlib stream, which starts in the
                                // first fdAT chunk. A later fdAT chunk holds only data after the end of that stream.
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            if (image is null)
                            {
                                PngThrowHelper.ThrowMissingDefaultData();
                            }

                            if (currentFrameControl is null)
                            {
                                PngThrowHelper.ThrowMissingFrameControl();
                            }

                            this.InitializeFrame(previousFrameControl, currentFrameControl.Value, image, previousFrame, out currentFrame);

                            this.currentStream.Position += 4;
                            this.ReadScanlines(
                                chunk.Length - 4,
                                currentFrame,
                                pngMetadata,
                                this.ReadNextFrameDataChunk,
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

                            pngMetadata.AnimateRootFrame = currentFrameControl != null;
                            currentFrameControl ??= new FrameControl((uint)this.header.Width, (uint)this.header.Height);
                            if (image is null)
                            {
                                // PLTE and tRNS precede IDAT. Validate the required indexed palette before
                                // allocating pixel storage.
                                this.AssignColorPalette<TPixel>(this.palette, this.paletteAlpha, pngMetadata, metadata.IccProfile);
                                this.InitializeImage(metadata, currentFrameControl.Value, out image);
                            }

                            this.ReadScanlines(
                                chunk.Length,
                                image.Frames.RootFrame,
                                pngMetadata,
                                this.ReadNextDataChunk,
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
                            this.Options.SegmentIntegrityHandling.ExecuteAncillarySegmentAction(
                                () => this.ReadColorProfileChunk(metadata, chunk.Data.GetSpan()));
                            break;
                        case PngChunkType.End:
                            goto EOF;
                        case PngChunkType.ProprietaryApple:
                            this.ReadCgbiChunk();
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
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            ReadPhysicalChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Gamma:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            ReadGammaChunk(pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Cicp:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            ReadCicpChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameControl:
                            ++frameCount;
                            if (frameCount > this.maxFrames)
                            {
                                break;
                            }

                            currentFrameControl = this.ReadFrameControlChunk(chunk.Data.GetSpan());

                            break;
                        case PngChunkType.FrameData:
                            ValidateFrameDataChunk(chunk);

                            if (frameCount > this.maxFrames)
                            {
                                // Must skip the chunk data even when we've hit maxFrames, because TryReadChunk
                                // restores the stream position to the start of the fdAT data after CRC validation.
                                this.SkipChunkDataAndCrc(chunk);
                                this.SkipRemainingFrameDataChunks(buffer);
                                break;
                            }

                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            if (currentFrameControl is null)
                            {
                                PngThrowHelper.ThrowMissingFrameControl();
                            }

                            InitializeFrameMetadata(framesMetadata, currentFrameControl.Value);

                            // Skip data for this and all remaining FrameData chunks belonging to the same frame
                            // (comparable to how Decode consumes them via ReadScanlines + ReadNextFrameDataChunk).
                            this.SkipChunkDataAndCrc(chunk);
                            this.SkipRemainingFrameDataChunks(buffer);
                            break;
                        case PngChunkType.Data:

                            // Spec says tRNS must be before IDAT so safe to exit.
                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            pngMetadata.AnimateRootFrame = currentFrameControl != null;
                            currentFrameControl ??= new FrameControl((uint)this.header.Width, (uint)this.header.Height);
                            if (framesMetadata.Count == 0)
                            {
                                InitializeFrameMetadata(framesMetadata, currentFrameControl.Value);

                                // Both PLTE and tRNS chunks, if present, have been read at this point as per spec.
                                // Identify has no destination pixel format; RgbaVector preserves converted colors as unassociated floating-point values.
                                this.AssignColorPalette<RgbaVector>(this.palette, this.paletteAlpha, pngMetadata, metadata.IccProfile);
                            }

                            this.SkipChunkDataAndCrc(chunk);
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
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            this.ReadTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.CompressedText:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            this.ReadCompressedTextChunk(metadata, pngMetadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.InternationalText:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            this.ReadInternationalTextChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.Exif:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            }

                            this.ReadExifChunk(metadata, chunk);

                            break;
                        case PngChunkType.EmbeddedColorProfile:
                            this.Options.SegmentIntegrityHandling.ExecuteAncillarySegmentAction(
                                () => this.ReadColorProfileChunk(metadata, chunk.Data.GetSpan()));
                            break;
                        case PngChunkType.End:
                            goto EOF;

                        case PngChunkType.ProprietaryApple:
                            this.ReadCgbiChunk();
                            break;

                        default:
                            if (this.colorMetadataOnly)
                            {
                                this.SkipChunkDataAndCrc(chunk);
                            }

                            break;
                    }
                }
                finally
                {
                    chunk.Data?.Dispose(); // Data is rented in ReadChunkData()
                }
            }

            EOF:
            if (this.header.Width == 0 && this.header.Height == 0)
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
    /// Reads the data chunk containing gamma data.
    /// </summary>
    /// <param name="pngMetadata">The metadata to read to.</param>
    /// <param name="data">The data containing physical data.</param>
    private static void ReadGammaChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            // Ignore invalid gamma chunks.
            return;
        }

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

        this.bytesPerPixel = this.CalculateBytesPerPixel();
        this.bytesPerScanline = CalculateScanlineLength(this.header.Width, this.header.BitDepth, this.bytesPerPixel) + 1;

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
    /// Calculates the correct number of bytes per pixel for the given color type.
    /// </summary>
    /// <returns>The <see cref="int"/></returns>
    private int CalculateBytesPerPixel()
        => this.pngColorType
        switch
        {
            PngColorType.Grayscale => this.header.BitDepth == 16 ? 2 : 1,
            PngColorType.GrayscaleWithAlpha => this.header.BitDepth == 16 ? 4 : 2,
            PngColorType.Palette => 1,
            PngColorType.Rgb => this.header.BitDepth == 16 ? 6 : 3,
            _ => this.header.BitDepth == 16 ? 8 : 4,
        };

    /// <summary>
    /// Calculates the scanline length.
    /// </summary>
    /// <param name="width">The width of the row.</param>
    /// <param name="bitDepth">The number of bits per sample.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel.</param>
    /// <returns>
    /// The <see cref="int"/> representing the length.
    /// </returns>
    internal static int CalculateScanlineLength(int width, int bitDepth, int bytesPerPixel)
    {
        int mod = bitDepth == 16 ? 16 : 8;
        long scanlineLength = (long)width * bitDepth * bytesPerPixel;

        long amount = scanlineLength % mod;
        if (amount != 0)
        {
            scanlineLength += mod - amount;
        }

        scanlineLength /= mod;
        if (scanlineLength >= int.MaxValue)
        {
            PngThrowHelper.ThrowInvalidImageContentException("PNG scanline length exceeds the supported maximum.");
        }

        return (int)scanlineLength;
    }

    /// <summary>
    /// Reads the scanlines within the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="chunkLength">The length of the chunk that containing the compressed scanline data.</param>
    /// <param name="image"> The pixel data.</param>
    /// <param name="pngMetadata">The png metadata</param>
    /// <param name="getData">A delegate to get more data from the inner stream when chunk boundaries are crossed.</param>
    /// <param name="frameControl">The frame control</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void ReadScanlines<TPixel>(
        int chunkLength,
        ImageFrame<TPixel> image,
        PngMetadata pngMetadata,
        Func<int> getData,
        in FrameControl frameControl,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // CgBI IDATs wrap a raw DEFLATE payload directly (no zlib CMF/FLG header
        // and no Adler-32 trailer); skip the zlib header parser entirely.
        if (this.isCgbi)
        {
            using ChunkedReadStream segmentStream = new(this.currentStream, getData);
            segmentStream.SetCurrentSegmentLength(chunkLength);
            using DeflateStream cgbiDataStream = new(segmentStream, CompressionMode.Decompress, leaveOpen: true);
            this.DecodeFromDeflate(cgbiDataStream, image, pngMetadata, frameControl, cancellationToken);
            return;
        }

        using ZlibInflateReader inflateStream = new(this.currentStream, getData);
        if (!inflateStream.AllocateNewBytes(chunkLength, false))
        {
            // The zlib header is part of the image data. A missing or bad header is damaged image data.
            // No row decodes. The frame region gets the same pixels as for data that ends before the first row.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data does not start with a valid zlib header.");
            ClearUndecodedPixels(frameControl, image, -1, (int)frameControl.YOffset);
            return;
        }

        this.DecodeFromDeflate(inflateStream.CompressedStream!, image, pngMetadata, frameControl, cancellationToken);
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

        Span<TPixel> blendRowBuffer = blendMemory is null ? [] : blendMemory.Memory.Span;

        while (currentRow < height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesPerFrameScanline = CalculateScanlineLength((int)frameControl.Width, this.header.BitDepth, this.bytesPerPixel) + 1;
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
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Unknown filter type.");
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
            int numColumns = Adam7.ComputeColumns(width, pass);

            if (numColumns == 0)
            {
                // This pass has no scanlines. The next pass starts at its first row in the frame region.
                // Pass 6 always has columns, so the pass index stays less than 7.
                pass++;
                currentRow = Adam7.FirstRow[pass] + (int)frameControl.YOffset;
                continue;
            }

            int bytesPerInterlaceScanline = CalculateScanlineLength(numColumns, this.header.BitDepth, this.bytesPerPixel) + 1;

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
                        this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Unknown filter type.");
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
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data ends before the last scanline.");
                    return false;
                }

                bytesRead += count;
            }
        }
        catch (InvalidDataException ex)
        {
            // The inflater found a deflate block that is not valid.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The PNG image data is damaged.", ex);
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
    /// Reads a header chunk from the data.
    /// </summary>
    /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
    private FrameControl ReadFrameControlChunk(ReadOnlySpan<byte> data)
    {
        FrameControl fcTL = FrameControl.Parse(data);

        fcTL.Validate(this.header);

        return fcTL;
    }

    /// <summary>
    /// Reads a header chunk from the data.
    /// </summary>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
    private void ReadHeaderChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
    {
        if (!Equals(this.header, default(PngHeader)))
        {
            PngThrowHelper.ThrowInvalidHeader();
        }

        this.header = PngHeader.Parse(data);

        this.header.Validate();

        pngMetadata.BitDepth = (PngBitDepth)this.header.BitDepth;
        pngMetadata.ColorType = this.header.ColorType;
        pngMetadata.InterlaceMethod = this.header.InterlaceMethod;

        this.pngColorType = this.header.ColorType;
        this.Dimensions = new Size(this.header.Width, this.header.Height);

        // Apple's pngcrush emits the CgBI chunk before IHDR, so the header
        // compatibility check is deferred until both chunks have been seen.
        if (this.isCgbi)
        {
            ThrowIfInvalidCgbiContent(this.header);
        }
    }

    /// <summary>
    /// Marks the image as CgBI and validates a header that has already been read.
    /// </summary>
    private void ReadCgbiChunk()
    {
        this.isCgbi = true;

        // Although Apple's pngcrush normally writes CgBI before IHDR, accepting the
        // reverse order must apply the same compatibility validation.
        if (!Equals(this.header, default(PngHeader)))
        {
            ThrowIfInvalidCgbiContent(this.header);
        }
    }

    private static void ThrowIfInvalidCgbiContent(in PngHeader header)
    {
        if (header.BitDepth != 8 || (header.ColorType is not PngColorType.Rgb and not PngColorType.RgbWithAlpha))
        {
            PngThrowHelper.ThrowInvalidImageContentException(
                $"CgBI is only supported for 8-bit truecolor images. Was bit depth '{header.BitDepth}', color type '{header.ColorType}'.");
        }
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

        if (keywordEnd < 0 || keywordEnd + 2 > data.Length)
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
    /// <param name="data">The bytes containing the profile.</param>
    private static void ReadCicpChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            // Ignore invalid cICP chunks.
            return;
        }

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
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read PNG ICC compression method.");
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

            if (!inflateStream.AllocateNewBytes(compressedData.Length, false))
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG ICC profile.");
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
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid PNG ICC profile.");
                    return;
                }

                metadata.IccProfile = reader.ToProfile(this.Options.SegmentIntegrityHandling.IgnoresAncillaryErrors());
            }
            catch (InvalidDataException ex)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG ICC profile.", ex);
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
            if (!inflateStream.AllocateNewBytes(compressedData.Length, false))
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
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid compressed PNG metadata.", ex);
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

        if (zeroIndexKeyword < 0 || zeroIndexKeyword + 4 > data.Length)
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
    /// Reads the next data chunk.
    /// </summary>
    /// <returns>Count of bytes in the next data chunk, or 0 if there are no more data chunks left.</returns>
    private int ReadNextDataChunk()
    {
        if (this.nextChunk != null)
        {
            return 0;
        }

        Span<byte> buffer = stackalloc byte[20];

        // The compressed stream is the concatenation of the payloads of all consecutive data chunks, and the
        // specification permits zero-length data chunks. A return value of 0 tells the inflater that the
        // compressed stream has ended, so empty chunks are skipped rather than reported.
        while (true)
        {
            // Skip the CRC of the previous chunk.
            int length = this.currentStream.Read(buffer, 0, 4);
            if (length == 0 || !this.TryReadChunk(buffer, out PngChunk chunk))
            {
                return 0;
            }

            if (chunk.Type is not PngChunkType.Data)
            {
                // Only IDAT payloads belong to this stream. Frame data must return to the outer loop
                // for its own framing checks.
                this.nextChunk = chunk;
                return 0;
            }

            chunk.Data?.Dispose();
            if (chunk.Length > 0)
            {
                return chunk.Length;
            }
        }
    }

    /// <summary>
    /// Reads the next animated frame data chunk.
    /// </summary>
    /// <returns>Count of bytes in the next data chunk, or 0 if there are no more data chunks left.</returns>
    private int ReadNextFrameDataChunk()
    {
        if (this.nextChunk != null)
        {
            return 0;
        }

        Span<byte> buffer = stackalloc byte[20];

        // As with data chunks, frame data chunks with no payload after the sequence number are valid and are
        // skipped, so that a return value of 0 only ever means that the frame's compressed stream has ended.
        while (true)
        {
            // Skip the CRC of the previous chunk.
            int length = this.currentStream.Read(buffer, 0, 4);
            if (length == 0 || !this.TryReadChunk(buffer, out PngChunk chunk))
            {
                return 0;
            }

            if (chunk.Type is not PngChunkType.FrameData)
            {
                this.nextChunk = chunk;
                return 0;
            }

            chunk.Data?.Dispose();

            // A short fdAT chunk holds no compressed data. A missing sequence number is a structural error in
            // every mode. The check occurs here, before the frame reports truncated image data.
            ValidateFrameDataChunk(chunk);

            this.currentStream.Position += 4; // Skip sequence number
            int dataLength = chunk.Length - 4;
            if (dataLength > 0)
            {
                return dataLength;
            }
        }
    }

    /// <summary>
    /// Skips any remaining <see cref="PngChunkType.FrameData"/> chunks belonging to the current frame.
    /// This mirrors how <see cref="ReadNextFrameDataChunk"/> is used during decoding:
    /// consecutive fdAT chunks are consumed until a non-fdAT chunk is encountered,
    /// which is stored in <see cref="nextChunk"/> for the next iteration.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    private void SkipRemainingFrameDataChunks(Span<byte> buffer)
    {
        while (this.TryReadChunk(buffer, out PngChunk chunk))
        {
            if (chunk.Type is PngChunkType.FrameData && chunk.Length >= sizeof(uint))
            {
                chunk.Data?.Dispose();
                this.SkipChunkDataAndCrc(chunk);
            }
            else
            {
                // Preserve other chunks, including malformed fdAT, for validation by the outer chunk loop.
                this.nextChunk = chunk;
                return;
            }
        }
    }

    /// <summary>
    /// Checks the required sequence number before consuming a frame data chunk.
    /// </summary>
    /// <param name="chunk">The frame data chunk.</param>
    private static void ValidateFrameDataChunk(in PngChunk chunk)
    {
        // A missing sequence number is a structural error in every integrity mode.
        if (chunk.Length < sizeof(uint))
        {
            PngThrowHelper.ThrowInvalidImageContentException("The frame data chunk does not contain a sequence number.");
        }
    }

    /// <summary>
    /// Reads a chunk from the stream.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="chunk">The image format chunk.</param>
    /// <returns>
    /// The <see cref="PngChunk"/>.
    /// </returns>
    private bool TryReadChunk(Span<byte> buffer, out PngChunk chunk)
    {
        if (this.nextChunk != null)
        {
            chunk = this.nextChunk.Value;

            this.nextChunk = null;

            return true;
        }

        if (this.currentStream.Position >= this.currentStream.Length - 1)
        {
            // IEND
            chunk = default;
            return false;
        }

        // Capture the current position so we can revert back to it if we fail to read a valid chunk.
        long position = this.currentStream.Position;

        if (!this.TryReadChunkLength(buffer, out int length))
        {
            // IEND
            chunk = default;
            return false;
        }

        while (length < 0)
        {
            // Not a valid chunk so try again until we reach a known chunk.
            if (!this.TryReadChunkLength(buffer, out length))
            {
                // IEND
                chunk = default;
                return false;
            }
        }

        PngChunkType type;

        // Loop until we get a chunk type that is valid.
        while (true)
        {
            if (!this.TryReadChunkType(buffer, out type))
            {
                // The stream ends inside the chunk type. This ends the chunk sequence.
                chunk = default;
                return false;
            }

            if (!IsValidChunkType(type))
            {
                // The chunk type is invalid.
                // Revert back to the next byte past the previous position and try again.
                this.currentStream.Position = ++position;

                // If we are now at the end of the stream, we're done.
                if (this.currentStream.Position >= this.currentStream.Length)
                {
                    chunk = default;
                    return false;
                }

                // Read the next chunk’s length.
                if (!this.TryReadChunkLength(buffer, out length))
                {
                    chunk = default;
                    return false;
                }

                while (length < 0)
                {
                    if (!this.TryReadChunkLength(buffer, out length))
                    {
                        chunk = default;
                        return false;
                    }
                }

                // Continue to try reading the next chunk.
                continue;
            }

            // We have a valid chunk type.
            break;
        }

        // If we're reading color metadata only we're only interested in the IHDR and tRNS chunks.
        // We can skip most other chunk data in the stream for better performance.
        if (this.colorMetadataOnly &&
            type != PngChunkType.Header &&
            type != PngChunkType.Transparency &&
            type != PngChunkType.Palette &&
            type != PngChunkType.AnimationControl &&
            type != PngChunkType.FrameControl)
        {
            chunk = new PngChunk(length, type);
            return true;
        }

        // A chunk might report a length that exceeds the length of the stream.
        // Take the minimum of the two values to ensure we don't read past the end of the stream.
        position = this.currentStream.Position;
        long available = this.currentStream.Length - position;

        // These chunks can carry profiles. Require the declared payload and all four CRC bytes.
        // The integrity policy controls the CRC comparison.
        if (type is PngChunkType.Exif or PngChunkType.EmbeddedColorProfile or PngChunkType.Text
            or PngChunkType.CompressedText or PngChunkType.InternationalText && length > available - sizeof(uint))
        {
            this.segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The PNG metadata chunk is incomplete.");

            this.currentStream.Position = this.currentStream.Length;
            chunk = default;
            return false;
        }

        // The inflater reads the payload of an image data chunk from the stream later. The CRC check reads it in
        // blocks only, so the chunk needs no buffer of its own.
        if (type is PngChunkType.Data or PngChunkType.FrameData)
        {
            chunk = new PngChunk((int)Math.Min(length, available), type);
            this.ValidateImageDataChunk(chunk, buffer, position);
            return true;
        }

        chunk = new PngChunk(
            length: (int)Math.Min(length, available),
            type: type,
            data: this.ReadChunkData(length));

        try
        {
            this.ValidateChunk(chunk, buffer);
        }
        catch
        {
            // The caller owns the chunk data only after this method returns. The CRC read can throw first, for example
            // when the stream observes a cancellation request, so dispose the data here.
            chunk.Data?.Dispose();
            throw;
        }

        return true;
    }

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
    /// Validates the png chunk.
    /// </summary>
    /// <param name="chunk">The <see cref="PngChunk"/>.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ValidateChunk(in PngChunk chunk, Span<byte> buffer)
    {
        uint inputCrc = this.ReadChunkCrc(buffer);
        if (chunk.IsCritical(this.segmentIntegrityHandling))
        {
            Span<byte> chunkType = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(chunkType, (uint)chunk.Type);

            this.crc32.Reset();
            this.crc32.Append(chunkType);
            this.crc32.Append(chunk.Data.GetSpan());

            if (this.crc32.GetCurrentHashAsUInt32() != inputCrc)
            {
                string chunkTypeName = Encoding.ASCII.GetString(chunkType);
                PngThrowHelper.ThrowInvalidChunkCrc(chunkTypeName);
            }
        }
    }

    /// <summary>
    /// Validates the CRC of an image data chunk, then restores the stream position to the start of the payload.
    /// </summary>
    /// <remarks>
    /// The payload goes through <see cref="dataCrcBuffer"/> in blocks of the stream buffer size, so each block is one
    /// copy from the stream buffer. When the integrity handling ignores image data errors, the check reads nothing.
    /// </remarks>
    /// <param name="chunk">The image data chunk.</param>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="payloadStart">The stream position of the first payload byte.</param>
    private void ValidateImageDataChunk(in PngChunk chunk, Span<byte> buffer, long payloadStart)
    {
        if (chunk.IsCritical(this.segmentIntegrityHandling))
        {
            Span<byte> chunkType = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(chunkType, (uint)chunk.Type);

            this.crc32.Reset();
            this.crc32.Append(chunkType);

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

            if (this.crc32.GetCurrentHashAsUInt32() != this.ReadChunkCrc(buffer))
            {
                PngThrowHelper.ThrowInvalidChunkCrc(Encoding.ASCII.GetString(chunkType));
            }
        }

        // The inflater reads the payload from its start.
        this.currentStream.Position = payloadStart;
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
    /// <param name="length">The length of the chunk data to read.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private IMemoryOwner<byte> ReadChunkData(int length)
    {
        if (length == 0)
        {
            return new BasicArrayBuffer<byte>([]);
        }

        // We rent the buffer here to return it afterwards in Decode()
        // We don't want to throw a degenerated memory exception here as we want to allow partial decoding
        // so limit the length.
        length = (int)Math.Min(length, this.currentStream.Length - this.currentStream.Position);
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
    /// Attempts to read the chunk type of the next chunk.
    /// </summary>
    /// <remarks>
    /// A stream that ends inside the chunk type ends the chunk sequence, as a stream that ends inside the chunk length
    /// does. When this cuts the image data short, the scanline reader reports the damage.
    /// </remarks>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="type">The chunk type. If the return value is <see langword="false"/>, this parameter is the default value.</param>
    /// <returns>
    /// <see langword="true"/> if the method reads the chunk type. Otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private bool TryReadChunkType(Span<byte> buffer, out PngChunkType type)
    {
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            type = (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
            return true;
        }

        type = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the length of the next chunk.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="result">The result length. If the return type is <see langword="false"/> this parameter is passed uninitialized.</param>
    /// <returns>
    /// Whether the length was read.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private bool TryReadChunkLength(Span<byte> buffer, out int result)
    {
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            result = BinaryPrimitives.ReadInt32BigEndian(buffer);

            return true;
        }

        result = default;
        return false;
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
