// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
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
    /// The number of bytes per sample.
    /// </summary>
    private int bytesPerSample;

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
    /// The maximum memory in bytes that a zTXt, sPLT, iTXt, iCCP, or unknown chunk can occupy when decompressed.
    /// </summary>
    private readonly int maxUncompressedLength;

    /// <summary>
    /// A value indicating whether the image data has been read.
    /// </summary>
    private bool hasImageData;

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
                            if (!Equals(this.header, default(PngHeader)))
                            {
                                PngThrowHelper.ThrowInvalidHeader();
                            }

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
                            if (frameCount >= this.maxFrames)
                            {
                                goto EOF;
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
                        case PngChunkType.Data:
                            pngMetadata.AnimateRootFrame = currentFrameControl != null;
                            currentFrameControl ??= new((uint)this.header.Width, (uint)this.header.Height);
                            if (image is null)
                            {
                                this.InitializeImage(metadata, currentFrameControl.Value, out image);

                                // Both PLTE and tRNS chunks, if present, have been read at this point as per spec.
                                AssignColorPalette(this.palette, this.paletteAlpha, pngMetadata);
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

                            if (frameCount >= this.maxFrames)
                            {
                                goto EOF;
                            }

                            break;
                        case PngChunkType.Palette:
                            this.palette = chunk.Data.GetSpan().ToArray();
                            break;
                        case PngChunkType.Transparency:
                            this.paletteAlpha = chunk.Data.GetSpan().ToArray();
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
                            if (!this.skipMetadata)
                            {
                                byte[] exifData = new byte[chunk.Length];
                                chunk.Data.GetSpan().CopyTo(exifData);
                                MergeOrSetExifProfile(metadata, new(exifData), replaceExistingKeys: true);
                            }

                            break;
                        case PngChunkType.EmbeddedColorProfile:
                            this.ReadColorProfileChunk(metadata, chunk.Data.GetSpan());
                            break;
                        case PngChunkType.End:
                            goto EOF;
                        case PngChunkType.ProprietaryApple:
                            PngThrowHelper.ThrowInvalidChunkType("Proprietary Apple PNG detected! This PNG file is not conform to the specification and cannot be decoded.");
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
                            if (frameCount >= this.maxFrames)
                            {
                                break;
                            }

                            currentFrameControl = this.ReadFrameControlChunk(chunk.Data.GetSpan());

                            break;
                        case PngChunkType.FrameData:
                            if (frameCount >= this.maxFrames)
                            {
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

                            // Skip sequence number
                            this.currentStream.Skip(4);
                            this.SkipChunkDataAndCrc(chunk);
                            break;
                        case PngChunkType.Data:

                            // Spec says tRNS must be before IDAT so safe to exit.
                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            pngMetadata.AnimateRootFrame = currentFrameControl != null;
                            currentFrameControl ??= new((uint)this.header.Width, (uint)this.header.Height);
                            if (framesMetadata.Count == 0)
                            {
                                InitializeFrameMetadata(framesMetadata, currentFrameControl.Value);

                                // Both PLTE and tRNS chunks, if present, have been read at this point as per spec.
                                AssignColorPalette(this.palette, this.paletteAlpha, pngMetadata);
                            }

                            this.SkipChunkDataAndCrc(chunk);
                            break;
                        case PngChunkType.Palette:
                            this.palette = chunk.Data.GetSpan().ToArray();
                            break;

                        case PngChunkType.Transparency:
                            this.paletteAlpha = chunk.Data.GetSpan().ToArray();
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

                            if (!this.skipMetadata)
                            {
                                byte[] exifData = new byte[chunk.Length];
                                chunk.Data.GetSpan().CopyTo(exifData);
                                MergeOrSetExifProfile(metadata, new(exifData), replaceExistingKeys: true);
                            }

                            break;
                        case PngChunkType.End:
                            goto EOF;

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

            return new(new(this.header.Width, this.header.Height), metadata, framesMetadata);
        }
        finally
        {
            this.scanline?.Dispose();
            this.previousScanline?.Dispose();
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
    /// Attempts to convert a byte array to a new array where each value in the original array is represented by the
    /// specified number of bits.
    /// </summary>
    /// <param name="source">The bytes to convert from. Cannot be empty.</param>
    /// <param name="bytesPerScanline">The number of bytes per scanline.</param>
    /// <param name="bits">The number of bits per value.</param>
    /// <param name="buffer">The new array.</param>
    /// <returns>The resulting <see cref="ReadOnlySpan{Byte}"/> array.</returns>
    private bool TryScaleUpTo8BitArray(ReadOnlySpan<byte> source, int bytesPerScanline, int bits, [NotNullWhen(true)] out IMemoryOwner<byte>? buffer)
    {
        if (bits >= 8)
        {
            buffer = null;
            return false;
        }

        buffer = this.memoryAllocator.Allocate<byte>(bytesPerScanline * 8 / bits, AllocationOptions.Clean);
        ref byte sourceRef = ref MemoryMarshal.GetReference(source);
        ref byte resultRef = ref buffer.GetReference();
        int mask = 0xFF >> (8 - bits);
        int resultOffset = 0;

        for (int i = 0; i < bytesPerScanline; i++)
        {
            byte b = Unsafe.Add(ref sourceRef, (uint)i);
            for (int shift = 0; shift < 8; shift += bits)
            {
                int colorIndex = (b >> (8 - bits - shift)) & mask;
                Unsafe.Add(ref resultRef, (uint)resultOffset) = (byte)colorIndex;
                resultOffset++;
            }
        }

        return true;
    }

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
        image = new(this.configuration, this.header.Width, this.header.Height, metadata);

        PngFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetPngMetadata();
        frameMetadata.FromChunk(in frameControl);

        this.bytesPerPixel = this.CalculateBytesPerPixel();
        this.bytesPerScanline = this.CalculateScanlineLength(this.header.Width) + 1;
        this.bytesPerSample = 1;
        if (this.header.BitDepth >= 8)
        {
            this.bytesPerSample = this.header.BitDepth / 8;
        }

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
    /// <returns>
    /// The <see cref="int"/> representing the length.
    /// </returns>
    private int CalculateScanlineLength(int width)
    {
        int mod = this.header.BitDepth == 16 ? 16 : 8;
        int scanlineLength = width * this.header.BitDepth * this.bytesPerPixel;

        int amount = scanlineLength % mod;
        if (amount != 0)
        {
            scanlineLength += mod - amount;
        }

        return scanlineLength / mod;
    }

    /// <summary>
    /// Reads the scanlines within the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="chunkLength">The length of the chunk that containing the compressed scanline data.</param>
    /// <param name="image"> The pixel data.</param>
    /// <param name="pngMetadata">The png metadata</param>
    /// <param name="getData">A delegate to get more data from the inner stream for <see cref="ZlibInflateStream"/>.</param>
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
        using ZlibInflateStream inflateStream = new(this.currentStream, getData);
        if (!inflateStream.AllocateNewBytes(chunkLength, !this.hasImageData))
        {
            return;
        }

        DeflateStream dataStream = inflateStream.CompressedStream!;

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
        int currentRow = (int)frameControl.YOffset;
        int currentRowBytesRead = 0;
        int height = (int)frameControl.YMax;

        IMemoryOwner<TPixel>? blendMemory = null;
        Span<TPixel> blendRowBuffer = [];
        if (frameControl.BlendMode == FrameBlendMode.Over)
        {
            blendMemory = this.memoryAllocator.Allocate<TPixel>(imageFrame.Width, AllocationOptions.Clean);
            blendRowBuffer = blendMemory.Memory.Span;
        }

        while (currentRow < height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesPerFrameScanline = this.CalculateScanlineLength((int)frameControl.Width) + 1;
            Span<byte> scanSpan = this.scanline.GetSpan()[..bytesPerFrameScanline];
            Span<byte> prevSpan = this.previousScanline.GetSpan()[..bytesPerFrameScanline];

            while (currentRowBytesRead < bytesPerFrameScanline)
            {
                int bytesRead = compressedStream.Read(scanSpan, currentRowBytesRead, bytesPerFrameScanline - currentRowBytesRead);
                if (bytesRead <= 0)
                {
                    goto EXIT;
                }

                currentRowBytesRead += bytesRead;
            }

            currentRowBytesRead = 0;

            switch ((FilterType)scanSpan[0])
            {
                case FilterType.None:
                    break;

                case FilterType.Sub:
                    SubFilter.Decode(scanSpan, this.bytesPerPixel);
                    break;

                case FilterType.Up:
                    UpFilter.Decode(scanSpan, prevSpan);
                    break;

                case FilterType.Average:
                    AverageFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                case FilterType.Paeth:
                    PaethFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                    break;

                default:
                    if (this.segmentIntegrityHandling is SegmentIntegrityHandling.IgnoreData or SegmentIntegrityHandling.IgnoreAll)
                    {
                        goto EXIT;
                    }

                    PngThrowHelper.ThrowUnknownFilter();
                    break;
            }

            this.ProcessDefilteredScanline(frameControl, currentRow, scanSpan, imageFrame, pngMetadata, blendRowBuffer);
            this.SwapScanlineBuffers();
            currentRow++;
        }

        EXIT:
        this.hasImageData = true;
        blendMemory?.Dispose();
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
        int currentRow = Adam7.FirstRow[0] + (int)frameControl.YOffset;
        int currentRowBytesRead = 0;
        int pass = 0;
        int width = (int)frameControl.Width;
        int endRow = (int)frameControl.YMax;

        Buffer2D<TPixel> imageBuffer = imageFrame.PixelBuffer;

        IMemoryOwner<TPixel>? blendMemory = null;
        Span<TPixel> blendRowBuffer = [];
        if (frameControl.BlendMode == FrameBlendMode.Over)
        {
            blendMemory = this.memoryAllocator.Allocate<TPixel>(imageFrame.Width, AllocationOptions.Clean);
            blendRowBuffer = blendMemory.Memory.Span;
        }

        while (true)
        {
            int numColumns = Adam7.ComputeColumns(width, pass);

            if (numColumns == 0)
            {
                pass++;

                // This pass contains no data; skip to next pass
                continue;
            }

            int bytesPerInterlaceScanline = this.CalculateScanlineLength(numColumns) + 1;

            while (currentRow < endRow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (currentRowBytesRead < bytesPerInterlaceScanline)
                {
                    int bytesRead = compressedStream.Read(this.scanline.GetSpan(), currentRowBytesRead, bytesPerInterlaceScanline - currentRowBytesRead);
                    if (bytesRead <= 0)
                    {
                        goto EXIT;
                    }

                    currentRowBytesRead += bytesRead;
                }

                currentRowBytesRead = 0;

                Span<byte> scanSpan = this.scanline.Slice(0, bytesPerInterlaceScanline);
                Span<byte> prevSpan = this.previousScanline.Slice(0, bytesPerInterlaceScanline);

                switch ((FilterType)scanSpan[0])
                {
                    case FilterType.None:
                        break;

                    case FilterType.Sub:
                        SubFilter.Decode(scanSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Up:
                        UpFilter.Decode(scanSpan, prevSpan);
                        break;

                    case FilterType.Average:
                        AverageFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Paeth:
                        PaethFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                        break;

                    default:
                        if (this.segmentIntegrityHandling is SegmentIntegrityHandling.IgnoreData or SegmentIntegrityHandling.IgnoreAll)
                        {
                            goto EXIT;
                        }

                        PngThrowHelper.ThrowUnknownFilter();
                        break;
                }

                Span<TPixel> rowSpan = imageBuffer.DangerousGetRowSpan(currentRow);
                this.ProcessInterlacedDefilteredScanline(
                    frameControl,
                    this.scanline.GetSpan(),
                    rowSpan,
                    pngMetadata,
                    blendRowBuffer,
                    pixelOffset: Adam7.FirstColumn[pass],
                    increment: Adam7.ColumnIncrement[pass]);

                blendRowBuffer.Clear();
                this.SwapScanlineBuffers();

                currentRow += Adam7.RowIncrement[pass];
            }

            pass++;
            this.previousScanline.Clear();

            if (pass < 7)
            {
                currentRow = Adam7.FirstRow[pass];
            }
            else
            {
                pass = 0;
                break;
            }
        }

        EXIT:
        this.hasImageData = true;
        blendMemory?.Dispose();
    }

    /// <summary>
    /// Processes the de-filtered scanline filling the image pixel data
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="currentRow">The index of the current scanline being processed.</param>
    /// <param name="scanline">The de-filtered scanline</param>
    /// <param name="pixels">The image</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="blendRowBuffer">A span used to temporarily hold the decoded row pixel data for alpha blending.</param>
    private void ProcessDefilteredScanline<TPixel>(
        in FrameControl frameControl,
        int currentRow,
        ReadOnlySpan<byte> scanline,
        ImageFrame<TPixel> pixels,
        PngMetadata pngMetadata,
        Span<TPixel> blendRowBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<TPixel> destination = pixels.PixelBuffer.DangerousGetRowSpan(currentRow);

        bool blend = frameControl.BlendMode == FrameBlendMode.Over;
        Span<TPixel> rowSpan = blend
            ? blendRowBuffer
            : destination;

        // Trim the first marker byte from the buffer
        ReadOnlySpan<byte> trimmed = scanline[1..];

        // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
        IMemoryOwner<byte>? buffer = null;
        try
        {
            // TODO: The allocation here could be per frame, not per scanline.
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(
                trimmed,
                this.bytesPerScanline - 1,
                this.header.BitDepth,
                out buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    PngScanlineProcessor.ProcessGrayscaleScanline(
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        pngMetadata.TransparentColor);

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessGrayscaleWithAlphaScanline(
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)this.bytesPerPixel,
                        (uint)this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessPaletteScanline(
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        pngMetadata.ColorTable);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessRgbScanline(
                        this.configuration,
                        this.header.BitDepth,
                        frameControl,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.TransparentColor);

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessRgbaScanline(
                        this.configuration,
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
            }

            if (blend)
            {
                PixelBlender<TPixel> blender =
                    PixelOperations<TPixel>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);
                blender.Blend<TPixel>(this.configuration, destination, destination, rowSpan, 1F);
            }
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Processes the interlaced de-filtered scanline filling the image pixel data
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="scanline">The de-filtered scanline</param>
    /// <param name="destination">The current image row.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="blendRowBuffer">A span used to temporarily hold the decoded row pixel data for alpha blending.</param>
    /// <param name="pixelOffset">The column start index. Always 0 for none interlaced images.</param>
    /// <param name="increment">The column increment. Always 1 for none interlaced images.</param>
    private void ProcessInterlacedDefilteredScanline<TPixel>(
        in FrameControl frameControl,
        ReadOnlySpan<byte> scanline,
        Span<TPixel> destination,
        PngMetadata pngMetadata,
        Span<TPixel> blendRowBuffer,
        int pixelOffset = 0,
        int increment = 1)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool blend = frameControl.BlendMode == FrameBlendMode.Over;
        Span<TPixel> rowSpan = blend
            ? blendRowBuffer
            : destination;

        // Trim the first marker byte from the buffer
        ReadOnlySpan<byte> trimmed = scanline[1..];

        // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
        IMemoryOwner<byte>? buffer = null;
        try
        {
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(
                trimmed,
                this.bytesPerScanline,
                this.header.BitDepth,
                out buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    PngScanlineProcessor.ProcessInterlacedGrayscaleScanline(
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        pngMetadata.TransparentColor);

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedGrayscaleWithAlphaScanline(
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        (uint)this.bytesPerPixel,
                        (uint)this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessInterlacedPaletteScanline(
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        pngMetadata.ColorTable);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessInterlacedRgbScanline(
                        this.configuration,
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.TransparentColor);

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedRgbaScanline(
                        this.configuration,
                        this.header.BitDepth,
                        in frameControl,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
            }

            if (blend)
            {
                PixelBlender<TPixel> blender =
                    PixelOperations<TPixel>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);
                blender.Blend<TPixel>(this.configuration, destination, destination, rowSpan, 1F);
            }
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Decodes and assigns the color palette to the metadata
    /// </summary>
    /// <param name="palette">The palette buffer.</param>
    /// <param name="alpha">The alpha palette buffer.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    private static void AssignColorPalette(ReadOnlySpan<byte> palette, ReadOnlySpan<byte> alpha, PngMetadata pngMetadata)
    {
        if (palette.Length == 0)
        {
            return;
        }

        Color[] colorTable = new Color[palette.Length / Unsafe.SizeOf<Rgb24>()];
        ReadOnlySpan<Rgb24> rgbTable = MemoryMarshal.Cast<byte, Rgb24>(palette);
        Color.FromPixel(rgbTable, colorTable);

        if (alpha.Length > 0)
        {
            // The alpha chunk may contain as many transparency entries as there are palette entries
            // (more than that would not make any sense) or as few as one.
            for (int i = 0; i < alpha.Length; i++)
            {
                ref Color color = ref colorTable[i];
                color = color.WithAlpha(alpha[i] / 255F);
            }
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
                    ushort rc = BinaryPrimitives.ReadUInt16LittleEndian(alpha[..2]);
                    ushort gc = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(2, 2));
                    ushort bc = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(4, 2));

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
                    pngMetadata.TransparentColor = Color.FromPixel(new L16(BinaryPrimitives.ReadUInt16LittleEndian(alpha[..2])));
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
        this.header = PngHeader.Parse(data);

        this.header.Validate();

        pngMetadata.BitDepth = (PngBitDepth)this.header.BitDepth;
        pngMetadata.ColorType = this.header.ColorType;
        pngMetadata.InterlaceMethod = this.header.InterlaceMethod;

        this.pngColorType = this.header.ColorType;
        this.Dimensions = new(this.header.Width, this.header.Height);
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
            metadata.TextData.Add(new(name, value, string.Empty, string.Empty));
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

        int zeroIndex = data.IndexOf((byte)0);
        if (zeroIndex is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
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

        if (this.TryDecompressTextData(compressedData, PngConstants.Encoding, out string? uncompressed)
            && !TryReadTextChunkMetadata(baseMetadata, name, uncompressed))
        {
            metadata.TextData.Add(new(name, uncompressed, string.Empty, string.Empty));
        }
    }

    /// <summary>
    /// Checks if the given text chunk is actually storing parsable metadata.
    /// </summary>
    /// <param name="baseMetadata">The <see cref="ImageMetadata"/> object to store the parsed metadata in.</param>
    /// <param name="chunkName">The name of the text chunk.</param>
    /// <param name="chunkText">The contents of the text chunk.</param>
    /// <returns>True if metadata was successfully parsed from the text chunk. False if the
    /// text chunk was not identified as metadata, and should be stored in the metadata
    /// object unmodified.</returns>
    private static bool TryReadTextChunkMetadata(ImageMetadata baseMetadata, string chunkName, string chunkText)
    {
        if (chunkName.Equals("Raw profile type exif", StringComparison.OrdinalIgnoreCase) &&
            TryReadLegacyExifTextChunk(baseMetadata, chunkText))
        {
            // Successfully parsed legacy exif data from text
            return true;
        }

        // TODO: "Raw profile type iptc", potentially others?

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

        metadata.CicpProfile = new(colorPrimaries, transferFunction, matrixCoefficients, fullRange);
    }

    /// <summary>
    /// Reads exif data encoded into a text chunk with the name "raw profile type exif".
    /// This method was used by ImageMagick, exiftool, exiv2, digiKam, etc, before the
    /// 2017 update to png that allowed a true exif chunk.
    /// </summary>
    /// <param name="metadata">The <see cref="ImageMetadata"/> to store the decoded exif tags into.</param>
    /// <param name="data">The contents of the "raw profile type exif" text chunk.</param>
    private static bool TryReadLegacyExifTextChunk(ImageMetadata metadata, string data)
    {
        ReadOnlySpan<char> dataSpan = data.AsSpan();
        dataSpan = dataSpan.TrimStart();

        if (!StringEqualsInsensitive(dataSpan[..4], "exif".AsSpan()))
        {
            // "exif" identifier is missing from the beginning of the text chunk
            return false;
        }

        // Skip to the data length
        dataSpan = dataSpan[4..].TrimStart();
        int dataLengthEnd = dataSpan.IndexOf('\n');
        int dataLength = ParseInt32(dataSpan[..dataSpan.IndexOf('\n')]);

        // Skip to the hex-encoded data
        dataSpan = dataSpan[dataLengthEnd..].Trim();

        // Sequence of bytes for the exif header ("Exif" ASCII and two zero bytes).
        // This doesn't actually allocate.
        ReadOnlySpan<byte> exifHeader = [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];

        if (dataLength < exifHeader.Length)
        {
            // Not enough room for the required exif header, this data couldn't possibly be valid
            return false;
        }

        // Parse the hex-encoded data into the byte array we are going to hand off to ExifProfile
        byte[] exifBlob = new byte[dataLength - exifHeader.Length];

        try
        {
            // Check for the presence of the exif header in the hex-encoded binary data
            byte[] tempExifBuf = exifBlob;
            if (exifBlob.Length < exifHeader.Length)
            {
                // Need to allocate a temporary array, this should be an extremely uncommon (TODO: impossible?) case
                tempExifBuf = new byte[exifHeader.Length];
            }

            HexConverter.HexStringToBytes(dataSpan[..(exifHeader.Length * 2)], tempExifBuf);
            if (!tempExifBuf.AsSpan()[..exifHeader.Length].SequenceEqual(exifHeader))
            {
                // Exif header in the hex data is not valid
                return false;
            }

            // Skip over the exif header we just tested
            dataSpan = dataSpan[(exifHeader.Length * 2)..];
            dataLength -= exifHeader.Length;

            // Load the hex-encoded data, one line at a time
            for (int i = 0; i < dataLength;)
            {
                ReadOnlySpan<char> lineSpan = dataSpan;

                int newlineIndex = dataSpan.IndexOf('\n');
                if (newlineIndex != -1)
                {
                    lineSpan = dataSpan[..newlineIndex];
                }

                i += HexConverter.HexStringToBytes(lineSpan, exifBlob.AsSpan()[i..]);

                dataSpan = dataSpan[(newlineIndex + 1)..];
            }
        }
        catch
        {
            return false;
        }

        MergeOrSetExifProfile(metadata, new(exifBlob), replaceExistingKeys: false);
        return true;
    }

    /// <summary>
    /// Reads the color profile chunk. The data is stored similar to the zTXt chunk.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="data">The bytes containing the profile.</param>
    private void ReadColorProfileChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
    {
        int zeroIndex = data.IndexOf((byte)0);
        if (zeroIndex is < PngConstants.MinTextKeywordLength or > PngConstants.MaxTextKeywordLength)
        {
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

        if (this.TryDecompressZlibData(compressedData, this.maxUncompressedLength, out byte[] iccpProfileBytes))
        {
            metadata.IccProfile = new(iccpProfileBytes);
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
            using MemoryStream memoryStreamOutput = new(compressedData.Length);
            using UnmanagedMemoryStream memoryStreamInput = new(compressedDataBase, compressedData.Length);
            using BufferedReadStream bufferedStream = new(this.configuration, memoryStreamInput);
            using ZlibInflateStream inflateStream = new(bufferedStream);

            Span<byte> destUncompressedData = destBuffer.GetSpan();
            if (!inflateStream.AllocateNewBytes(compressedData.Length, false))
            {
                uncompressedBytesArray = [];
                return false;
            }

            int bytesRead = inflateStream.CompressedStream.Read(destUncompressedData, 0, destUncompressedData.Length);
            while (bytesRead != 0)
            {
                if (memoryStreamOutput.Length > maxLength)
                {
                    uncompressedBytesArray = [];
                    return false;
                }

                memoryStreamOutput.Write(destUncompressedData[..bytesRead]);
                bytesRead = inflateStream.CompressedStream.Read(destUncompressedData, 0, destUncompressedData.Length);
            }

            uncompressedBytesArray = memoryStreamOutput.ToArray();
            return true;
        }
    }

    /// <summary>
    /// Compares two ReadOnlySpan&lt;char&gt;s in a case-insensitive method.
    /// This is only needed because older frameworks are missing the extension method.
    /// </summary>
    /// <param name="span1">The first <see cref="Span{T}"/> to compare.</param>
    /// <param name="span2">The second <see cref="Span{T}"/> to compare.</param>
    /// <returns>True if the spans were identical, false otherwise.</returns>
    private static bool StringEqualsInsensitive(ReadOnlySpan<char> span1, ReadOnlySpan<char> span2)
        => span1.Equals(span2, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// int.Parse() a ReadOnlySpan&lt;char&gt;, with a fallback for older frameworks.
    /// </summary>
    /// <param name="span">The <see cref="int"/> to parse.</param>
    /// <returns>The parsed <see cref="int"/>.</returns>
    private static int ParseInt32(ReadOnlySpan<char> span) => int.Parse(span, provider: CultureInfo.InvariantCulture);

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
            metadata.ExifProfile = newProfile;
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
                pngMetadata.TextData.Add(new(keyword, uncompressed, language, translatedKeyword));
            }
        }
        else if (IsXmpTextData(keywordBytes))
        {
            metadata.XmpProfile = new(data[dataStartIdx..].ToArray());
        }
        else
        {
            string value = PngConstants.TranslatedEncoding.GetString(data[dataStartIdx..]);
            pngMetadata.TextData.Add(new(keyword, value, language, translatedKeyword));
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

        int length = this.currentStream.Read(buffer, 0, 4);
        if (length == 0)
        {
            return 0;
        }

        if (this.TryReadChunk(buffer, out PngChunk chunk))
        {
            if (chunk.Type is PngChunkType.Data or PngChunkType.FrameData)
            {
                chunk.Data?.Dispose();
                return chunk.Length;
            }

            this.nextChunk = chunk;
        }

        return 0;
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

        int length = this.currentStream.Read(buffer, 0, 4);
        if (length == 0)
        {
            return 0;
        }

        if (this.TryReadChunk(buffer, out PngChunk chunk))
        {
            if (chunk.Type is PngChunkType.FrameData)
            {
                chunk.Data?.Dispose();

                this.currentStream.Position += 4; // Skip sequence number
                return chunk.Length - 4;
            }

            this.nextChunk = chunk;
        }

        return 0;
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
            type = this.ReadChunkType(buffer);
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
            chunk = new(length, type);
            return true;
        }

        // A chunk might report a length that exceeds the length of the stream.
        // Take the minimum of the two values to ensure we don't read past the end of the stream.
        position = this.currentStream.Position;
        chunk = new(
            length: (int)Math.Min(length, this.currentStream.Length - position),
            type: type,
            data: this.ReadChunkData(length));

        this.ValidateChunk(chunk, buffer);

        // Restore the stream position for IDAT and fdAT chunks, because it will be decoded later and
        // was only read to verifying the CRC is correct.
        if (type is PngChunkType.Data or PngChunkType.FrameData)
        {
            this.currentStream.Position = position;
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
    /// <see langword="true"/> if the byte is an ASCII letter; otherwise, <see langword="false"/>.
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

                // ensure when throwing we dispose the data back to the memory allocator
                chunk.Data?.Dispose();
                PngThrowHelper.ThrowInvalidChunkCrc(chunkTypeName);
            }
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

        this.currentStream.Read(buffer.GetSpan(), 0, length);

        return buffer;
    }

    /// <summary>
    /// Identifies the chunk type from the chunk.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if the input stream is not valid.
    /// </exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    private PngChunkType ReadChunkType(Span<byte> buffer)
    {
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            return (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        PngThrowHelper.ThrowInvalidChunkType();

        // The IDE cannot detect the throw here.
        return default;
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
