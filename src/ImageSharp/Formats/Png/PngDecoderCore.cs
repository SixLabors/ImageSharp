// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Performs the png decoding operation.
/// </summary>
internal sealed class PngDecoderCore : IImageDecoderInternals
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
    /// Initializes a new instance of the <see cref="PngDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public PngDecoderCore(DecoderOptions options)
    {
        this.Options = options;
        this.configuration = options.Configuration;
        this.maxFrames = options.MaxFrames;
        this.skipMetadata = options.SkipMetadata;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    internal PngDecoderCore(DecoderOptions options, bool colorMetadataOnly)
    {
        this.Options = options;
        this.colorMetadataOnly = colorMetadataOnly;
        this.maxFrames = options.MaxFrames;
        this.skipMetadata = true;
        this.configuration = options.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <inheritdoc/>
    public DecoderOptions Options { get; }

    /// <inheritdoc/>
    public Size Dimensions => new(this.header.Width, this.header.Height);

    /// <inheritdoc/>
    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint frameCount = 0;
        ImageMetadata metadata = new();
        PngMetadata pngMetadata = metadata.GetPngMetadata();
        this.currentStream = stream;
        this.currentStream.Skip(8);
        Image<TPixel>? image = null;
        FrameControl? lastFrameControl = null;
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
                        case PngChunkType.FrameControl:
                            ++frameCount;
                            if (frameCount == this.maxFrames)
                            {
                                break;
                            }

                            currentFrame = null;
                            lastFrameControl = this.ReadFrameControlChunk(chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameData:
                            if (frameCount == this.maxFrames)
                            {
                                break;
                            }

                            if (image is null)
                            {
                                PngThrowHelper.ThrowMissingDefaultData();
                            }

                            if (lastFrameControl is null)
                            {
                                PngThrowHelper.ThrowMissingFrameControl();
                            }

                            if (currentFrame is null)
                            {
                                this.InitializeFrame(lastFrameControl.Value, image, out currentFrame);
                            }

                            this.currentStream.Position += 4;
                            this.ReadScanlines(
                                chunk.Length - 4,
                                currentFrame,
                                pngMetadata,
                                () =>
                                {
                                    int length = this.ReadNextDataChunk();
                                    if (this.ReadNextDataChunk() is 0)
                                    {
                                        return length;
                                    }

                                    this.currentStream.Position += 4; // Skip sequence number
                                    return length - 4;
                                },
                                cancellationToken);
                            lastFrameControl = null;
                            break;
                        case PngChunkType.Data:
                            if (image is null)
                            {
                                this.InitializeImage(metadata, lastFrameControl, out image);
                            }

                            this.ReadScanlines(chunk.Length, image.Frames.RootFrame, pngMetadata, this.ReadNextDataChunk, cancellationToken);
                            lastFrameControl = null;
                            break;
                        case PngChunkType.Palette:
                            byte[] pal = new byte[chunk.Length];
                            chunk.Data.GetSpan().CopyTo(pal);
                            this.palette = pal;
                            break;
                        case PngChunkType.Transparency:
                            byte[] alpha = new byte[chunk.Length];
                            chunk.Data.GetSpan().CopyTo(alpha);
                            this.paletteAlpha = alpha;
                            this.AssignTransparentMarkers(alpha, pngMetadata);
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
                                MergeOrSetExifProfile(metadata, new ExifProfile(exifData), replaceExistingKeys: true);
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
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        uint frameCount = 0;
        ImageMetadata metadata = new();
        PngMetadata pngMetadata = metadata.GetPngMetadata();
        this.currentStream = stream;
        FrameControl? lastFrameControl = null;
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
                        case PngChunkType.FrameControl:
                            ++frameCount;
                            if (frameCount == this.maxFrames)
                            {
                                break;
                            }

                            lastFrameControl = this.ReadFrameControlChunk(chunk.Data.GetSpan());
                            break;
                        case PngChunkType.FrameData:
                            if (frameCount == this.maxFrames)
                            {
                                break;
                            }

                            if (this.colorMetadataOnly)
                            {
                                goto EOF;
                            }

                            if (lastFrameControl is null)
                            {
                                PngThrowHelper.ThrowMissingFrameControl();
                            }

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

                            this.SkipChunkDataAndCrc(chunk);
                            break;
                        case PngChunkType.Transparency:
                            byte[] alpha = new byte[chunk.Length];
                            chunk.Data.GetSpan().CopyTo(alpha);
                            this.paletteAlpha = alpha;
                            this.AssignTransparentMarkers(alpha, pngMetadata);

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
                                MergeOrSetExifProfile(metadata, new ExifProfile(exifData), replaceExistingKeys: true);
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

            return new ImageInfo(new PixelTypeInfo(this.CalculateBitsPerPixel()), new(this.header.Width, this.header.Height), metadata);
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
    private void InitializeImage<TPixel>(ImageMetadata metadata, FrameControl? frameControl, out Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        image = Image.CreateUninitialized<TPixel>(
            this.configuration,
            this.header.Width,
            this.header.Height,
            metadata);

        if (frameControl is { } control)
        {
            PngFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetPngFrameMetadata();
            frameMetadata.FromChunk(control);
        }

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
    /// <param name="frameControl">The frame control information for the frame</param>
    /// <param name="image">The image that we will populate</param>
    /// <param name="frame">The created frame</param>
    private void InitializeFrame<TPixel>(FrameControl frameControl, Image<TPixel> image, out ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        frame = image.Frames.CreateFrame();

        PngFrameMetadata frameMetadata = frame.Metadata.GetPngFrameMetadata();

        frameMetadata.FromChunk(frameControl);

        this.previousScanline?.Dispose();
        this.scanline?.Dispose();
        this.previousScanline = this.memoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
        this.scanline = this.configuration.MemoryAllocator.Allocate<byte>(this.bytesPerScanline, AllocationOptions.Clean);
    }

    /// <summary>
    /// Calculates the correct number of bits per pixel for the given color type.
    /// </summary>
    /// <returns>The <see cref="int"/></returns>
    private int CalculateBitsPerPixel()
    {
        switch (this.pngColorType)
        {
            case PngColorType.Grayscale:
            case PngColorType.Palette:
                return this.header.BitDepth;
            case PngColorType.GrayscaleWithAlpha:
                return this.header.BitDepth * 2;
            case PngColorType.Rgb:
                return this.header.BitDepth * 3;
            case PngColorType.RgbWithAlpha:
                return this.header.BitDepth * 4;
            default:
                PngThrowHelper.ThrowNotSupportedColor();
                return -1;
        }
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
    /// <param name="cancellationToken">The cancellation token.</param>
    private void ReadScanlines<TPixel>(int chunkLength, ImageFrame<TPixel> image, PngMetadata pngMetadata, Func<int> getData, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using ZlibInflateStream deframeStream = new(this.currentStream, getData);
        deframeStream.AllocateNewBytes(chunkLength, true);
        DeflateStream dataStream = deframeStream.CompressedStream!;

        if (this.header.InterlaceMethod is PngInterlaceMode.Adam7)
        {
            this.DecodeInterlacedPixelData(dataStream, image, pngMetadata, cancellationToken);
        }
        else
        {
            this.DecodePixelData(dataStream, image, pngMetadata, cancellationToken);
        }
    }

    /// <summary>
    /// Decodes the raw pixel data row by row
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="image">The image to decode to.</param>
    /// <param name="pngMetadata">The png metadata</param>
    /// <param name="cancellationToken">The CancellationToken</param>
    private void DecodePixelData<TPixel>(DeflateStream compressedStream, ImageFrame<TPixel> image, PngMetadata pngMetadata, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int currentRow = Adam7.FirstRow[0];
        int currentRowBytesRead = 0;
        int height = image.Metadata.TryGetPngFrameMetadata(out PngFrameMetadata? frameMetadata) ? frameMetadata.Height : this.header.Height;
        while (currentRow < height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<byte> scanlineSpan = this.scanline.GetSpan();
            while (currentRowBytesRead < this.bytesPerScanline)
            {
                int bytesRead = compressedStream.Read(scanlineSpan, currentRowBytesRead, this.bytesPerScanline - currentRowBytesRead);
                if (bytesRead <= 0)
                {
                    return;
                }

                currentRowBytesRead += bytesRead;
            }

            currentRowBytesRead = 0;

            switch ((FilterType)scanlineSpan[0])
            {
                case FilterType.None:
                    break;

                case FilterType.Sub:
                    SubFilter.Decode(scanlineSpan, this.bytesPerPixel);
                    break;

                case FilterType.Up:
                    UpFilter.Decode(scanlineSpan, this.previousScanline.GetSpan());
                    break;

                case FilterType.Average:
                    AverageFilter.Decode(scanlineSpan, this.previousScanline.GetSpan(), this.bytesPerPixel);
                    break;

                case FilterType.Paeth:
                    PaethFilter.Decode(scanlineSpan, this.previousScanline.GetSpan(), this.bytesPerPixel);
                    break;

                default:
                    PngThrowHelper.ThrowUnknownFilter();
                    break;
            }

            this.ProcessDefilteredScanline(currentRow, scanlineSpan, image, pngMetadata);

            this.SwapScanlineBuffers();
            currentRow++;
        }
    }

    /// <summary>
    /// Decodes the raw interlaced pixel data row by row
    /// <see href="https://github.com/juehv/DentalImageViewer/blob/8a1a4424b15d6cc453b5de3f273daf3ff5e3a90d/DentalImageViewer/lib/jiu-0.14.3/net/sourceforge/jiu/codecs/PNGCodec.java"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="compressedStream">The compressed pixel data stream.</param>
    /// <param name="image">The current image.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodeInterlacedPixelData<TPixel>(DeflateStream compressedStream, ImageFrame<TPixel> image, PngMetadata pngMetadata, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int currentRow = Adam7.FirstRow[0];
        int currentRowBytesRead = 0;
        int pass = 0;
        int width = this.header.Width;
        int height = this.header.Height;
        if (image.Metadata.TryGetPngFrameMetadata(out PngFrameMetadata? frameMetadata))
        {
            width = frameMetadata.Width;
            height = frameMetadata.Height;
        }

        Buffer2D<TPixel> imageBuffer = image.PixelBuffer;
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

            while (currentRow < height)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (currentRowBytesRead < bytesPerInterlaceScanline)
                {
                    int bytesRead = compressedStream.Read(this.scanline.GetSpan(), currentRowBytesRead, bytesPerInterlaceScanline - currentRowBytesRead);
                    if (bytesRead <= 0)
                    {
                        return;
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
                        PngThrowHelper.ThrowUnknownFilter();
                        break;
                }

                Span<TPixel> rowSpan = imageBuffer.DangerousGetRowSpan(currentRow);
                this.ProcessInterlacedDefilteredScanline(this.scanline.GetSpan(), rowSpan, pngMetadata, Adam7.FirstColumn[pass], Adam7.ColumnIncrement[pass]);

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
    }

    /// <summary>
    /// Processes the de-filtered scanline filling the image pixel data
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="currentRow">The index of the current scanline being processed.</param>
    /// <param name="defilteredScanline">The de-filtered scanline</param>
    /// <param name="pixels">The image</param>
    /// <param name="pngMetadata">The png metadata.</param>
    private void ProcessDefilteredScanline<TPixel>(int currentRow, ReadOnlySpan<byte> defilteredScanline, ImageFrame<TPixel> pixels, PngMetadata pngMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<TPixel> rowSpan = pixels.PixelBuffer.DangerousGetRowSpan(currentRow);

        // Trim the first marker byte from the buffer
        ReadOnlySpan<byte> trimmed = defilteredScanline[1..];

        // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
        IMemoryOwner<byte>? buffer = null;
        try
        {
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
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentL16.GetValueOrDefault(),
                        pngMetadata.TransparentL8.GetValueOrDefault());

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessGrayscaleWithAlphaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)this.bytesPerPixel,
                        (uint)this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessPaletteScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.palette,
                        this.paletteAlpha);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessRgbScanline(
                        this.configuration,
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentRgb48.GetValueOrDefault(),
                        pngMetadata.TransparentRgb24.GetValueOrDefault());

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessRgbaScanline(
                        this.configuration,
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
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
    /// <param name="defilteredScanline">The de-filtered scanline</param>
    /// <param name="rowSpan">The current image row.</param>
    /// <param name="pngMetadata">The png metadata.</param>
    /// <param name="pixelOffset">The column start index. Always 0 for none interlaced images.</param>
    /// <param name="increment">The column increment. Always 1 for none interlaced images.</param>
    private void ProcessInterlacedDefilteredScanline<TPixel>(ReadOnlySpan<byte> defilteredScanline, Span<TPixel> rowSpan, PngMetadata pngMetadata, int pixelOffset = 0, int increment = 1)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Trim the first marker byte from the buffer
        ReadOnlySpan<byte> trimmed = defilteredScanline[1..];

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
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentL16.GetValueOrDefault(),
                        pngMetadata.TransparentL8.GetValueOrDefault());

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedGrayscaleWithAlphaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        (uint)this.bytesPerPixel,
                        (uint)this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessInterlacedPaletteScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        this.palette,
                        this.paletteAlpha);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessInterlacedRgbScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentRgb48.GetValueOrDefault(),
                        pngMetadata.TransparentRgb24.GetValueOrDefault());

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedRgbaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        (uint)pixelOffset,
                        (uint)increment,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
            }
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Decodes and assigns marker colors that identify transparent pixels in non indexed images.
    /// </summary>
    /// <param name="alpha">The alpha tRNS array.</param>
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

                    pngMetadata.TransparentRgb48 = new Rgb48(rc, gc, bc);
                    pngMetadata.HasTransparency = true;
                    return;
                }

                byte r = ReadByteLittleEndian(alpha, 0);
                byte g = ReadByteLittleEndian(alpha, 2);
                byte b = ReadByteLittleEndian(alpha, 4);
                pngMetadata.TransparentRgb24 = new Rgb24(r, g, b);
                pngMetadata.HasTransparency = true;
            }
        }
        else if (this.pngColorType == PngColorType.Grayscale)
        {
            if (alpha.Length >= 2)
            {
                if (this.header.BitDepth == 16)
                {
                    pngMetadata.TransparentL16 = new L16(BinaryPrimitives.ReadUInt16LittleEndian(alpha[..2]));
                }
                else
                {
                    pngMetadata.TransparentL8 = new L8(ReadByteLittleEndian(alpha, 0));
                }

                pngMetadata.HasTransparency = true;
            }
        }
        else if (this.pngColorType == PngColorType.Palette && alpha.Length > 0)
        {
            pngMetadata.HasTransparency = true;
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

        pngMetadata.NumberPlays = this.animationControl.NumberPlays;
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

        if (this.TryUncompressTextData(compressedData, PngConstants.Encoding, out string? uncompressed)
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
        ReadOnlySpan<byte> exifHeader = new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 };

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

        MergeOrSetExifProfile(metadata, new ExifProfile(exifBlob), replaceExistingKeys: false);
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

        if (this.TryUncompressZlibData(compressedData, out byte[] iccpProfileBytes))
        {
            metadata.IccProfile = new IccProfile(iccpProfileBytes);
        }
    }

    /// <summary>
    /// Tries to un-compress zlib compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data.</param>
    /// <param name="uncompressedBytesArray">The uncompressed bytes array.</param>
    /// <returns>True, if de-compressing was successful.</returns>
    private unsafe bool TryUncompressZlibData(ReadOnlySpan<byte> compressedData, out byte[] uncompressedBytesArray)
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
                uncompressedBytesArray = Array.Empty<byte>();
                return false;
            }

            int bytesRead = inflateStream.CompressedStream.Read(destUncompressedData, 0, destUncompressedData.Length);
            while (bytesRead != 0)
            {
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

            if (this.TryUncompressTextData(compressedData, PngConstants.TranslatedEncoding, out string? uncompressed))
            {
                pngMetadata.TextData.Add(new PngTextData(keyword, uncompressed, language, translatedKeyword));
            }
        }
        else if (IsXmpTextData(keywordBytes))
        {
            metadata.XmpProfile = new XmpProfile(data[dataStartIdx..].ToArray());
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
    private bool TryUncompressTextData(ReadOnlySpan<byte> compressedData, Encoding encoding, [NotNullWhen(true)] out string? value)
    {
        if (this.TryUncompressZlibData(compressedData, out byte[] uncompressedData))
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

        this.currentStream.Read(buffer, 0, 4);

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

        if (!this.TryReadChunkLength(buffer, out int length))
        {
            chunk = default;

            // IEND
            return false;
        }

        while (length < 0 || length > (this.currentStream.Length - this.currentStream.Position))
        {
            // Not a valid chunk so try again until we reach a known chunk.
            if (!this.TryReadChunkLength(buffer, out length))
            {
                chunk = default;

                return false;
            }
        }

        PngChunkType type = this.ReadChunkType(buffer);

        // If we're reading color metadata only we're only interested in the IHDR and tRNS chunks.
        // We can skip all other chunk data in the stream for better performance.
        if (this.colorMetadataOnly && type is not PngChunkType.Header and not PngChunkType.Transparency)
        {
            chunk = new PngChunk(length, type);

            return true;
        }

        long pos = this.currentStream.Position;
        chunk = new PngChunk(
            length: length,
            type: type,
            data: this.ReadChunkData(length));

        this.ValidateChunk(chunk, buffer);

        // Restore the stream position for IDAT and fdAT chunks, because it will be decoded later and
        // was only read to verifying the CRC is correct.
        if (type is PngChunkType.Data or PngChunkType.FrameData)
        {
            this.currentStream.Position = pos;
        }

        return true;
    }

    /// <summary>
    /// Validates the png chunk.
    /// </summary>
    /// <param name="chunk">The <see cref="PngChunk"/>.</param>
    /// <param name="buffer">Temporary buffer.</param>
    private void ValidateChunk(in PngChunk chunk, Span<byte> buffer)
    {
        uint inputCrc = this.ReadChunkCrc(buffer);

        if (chunk.IsCritical)
        {
            Span<byte> chunkType = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(chunkType, (uint)chunk.Type);

            uint validCrc = Crc32.Calculate(chunkType);
            validCrc = Crc32.Calculate(validCrc, chunk.Data.GetSpan());

            if (validCrc != inputCrc)
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
        // We rent the buffer here to return it afterwards in Decode()
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
            && !name.StartsWith(" ", StringComparison.Ordinal)
            && !name.EndsWith(" ", StringComparison.Ordinal);
    }

    private static bool IsXmpTextData(ReadOnlySpan<byte> keywordBytes)
        => keywordBytes.SequenceEqual(PngConstants.XmpKeyword);

    private void SwapScanlineBuffers()
        => (this.scanline, this.previousScanline) = (this.previousScanline, this.scanline);
}
