// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga;

/// <summary>
/// Performs the tga decoding operation.
/// </summary>
internal sealed class TgaDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// General configuration options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The metadata.
    /// </summary>
    private ImageMetadata? metadata;

    /// <summary>
    /// The tga specific metadata.
    /// </summary>
    private TgaMetadata? tgaMetadata;

    /// <summary>
    /// The file header containing general information about the image.
    /// </summary>
    private TgaFileHeader fileHeader;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// Indicates whether there is a alpha channel present.
    /// </summary>
    private bool hasAlpha;

    /// <summary>
    /// A value that tells if the pixel data holds color map indices. ReadFileHeader sets it from the image type, the color map type and the pixel depth.
    /// </summary>
    private bool usesColorMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="TgaDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public TgaDecoderCore(DecoderOptions options)
        : base(options)
    {
        this.configuration = options.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // The decoder reads and checks the header and the color map before it allocates the pixel buffer. A layout that the decoder cannot use is a structural
        // error, so it throws in each mode.
        TgaImageOrigin origin = this.ReadFileHeader(stream);
        stream.Skip(this.fileHeader.IdLength);
        Color[]? colors = this.ReadColorMap(stream);

        Image<TPixel> image = new(this.configuration, this.fileHeader.Width, this.fileHeader.Height, this.metadata);
        try
        {
            this.ReadPixels(stream, image.GetRootFramePixelBuffer(), colors, origin);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadFileHeader(stream);
        stream.Skip(this.fileHeader.IdLength);
        this.ReadColorMap(stream);

        return new ImageInfo(new Size(this.fileHeader.Width, this.fileHeader.Height), this.metadata);
    }

    /// <summary>
    /// Reads the pixel data of the image. The method reads each row in stream order and then converts it to the destination row. Data that ends early, or a
    /// color map index outside the stored color map, is damaged image data. The method reports it one time and then stops. The pixels that the decoder did not
    /// read keep the fill value.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The stream, at the first byte of the image data.</param>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="colors">The color map of a color mapped image. Otherwise, <see langword="null"/>.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadPixels<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, Color[]? colors, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;

        // TGA stores each pixel in whole bytes, so a 15-bit pixel uses two bytes. A color mapped image stores 8-bit or 16-bit indices. ReadFileHeader allows
        // only the depths 8, 15, 16, 24 and 32, so a pixel has 1 to 4 bytes.
        int bytesPerPixel = (this.fileHeader.PixelDepth + 7) >> 3;
        bool isRunLengthEncoded = this.fileHeader.ImageType.IsRunLengthEncoded();
        bool invertX = InvertX(origin);
        bool invertY = origin is TgaImageOrigin.BottomLeft or TgaImageOrigin.BottomRight;

        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(width * bytesPerPixel);
        using IMemoryOwner<TPixel>? palette = colors is null ? null : this.memoryAllocator.Allocate<TPixel>(colors.Length);
        Span<byte> rowSpan = row.GetSpan();
        Span<TPixel> paletteSpan = palette is null ? default : palette.GetSpan();
        if (colors is not null)
        {
            // The method converts each color map entry to the pixel type one time. The index rows then copy the entries.
            Color.ToPixel(colors, paletteSpan);
        }

        // The state of the current RLE packet. A packet can continue in the next row. The run pixel holds the one stored pixel of a run-length packet, which
        // is at most 4 bytes.
        Span<byte> runPixel = stackalloc byte[4];
        runPixel = runPixel[..bytesPerPixel];
        int packetPixels = 0;
        bool isRunPacket = false;

        for (int y = 0; y < height; y++)
        {
            int count = isRunLengthEncoded
                ? ReadRleRow(stream, rowSpan, runPixel, ref packetPixels, ref isRunPacket)
                : stream.Read(rowSpan) / bytesPerPixel;

            // The method does not convert a partial pixel at the end of the data.
            Span<byte> source = rowSpan[..(count * bytesPerPixel)];
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(invertY ? height - y - 1 : y);

            // A right-origin row stores its pixels from right to left. The pixels go to the right end of the row in stream order. Then the reverse puts stream
            // pixel i at column width - 1 - i. The image buffer starts clean. Thus the pixels that the conversion does not write have the fill value before and
            // after the reverse.
            Span<TPixel> destination = invertX ? pixelRow[(width - count)..] : pixelRow[..count];
            int converted = count;
            if (colors is null)
            {
                this.ConvertRow(source, destination);
            }
            else
            {
                converted = this.ConvertIndices(source, destination, paletteSpan);
            }

            if (invertX)
            {
                destination.Reverse();
            }

            if (converted < count)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Tga color index is outside the color map");
                return;
            }

            if (count < width)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
                return;
            }
        }

        // A packet must not write past the last pixel of the image. The decoder dropped the pixels that do not fit.
        if (packetPixels > 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Run length packet exceeds the image size");
        }
    }

    /// <summary>
    /// Decodes RLE packets until one row of stored pixels is complete or the data ends.
    /// </summary>
    /// <remarks>
    /// A packet starts with a repetition count byte. Its low 7 bits store the pixel count minus 1. A run-length packet has bit 7 set and one pixel value for
    /// all its pixels. A raw packet has one pixel value for each pixel. See the Truevision TGA File Format Specification, Version 2.0, section "Run-Length
    /// Encoding of Images".
    /// </remarks>
    /// <param name="stream">The image data stream.</param>
    /// <param name="row">The row of stored pixels.</param>
    /// <param name="runPixel">The stored pixel of the current run-length packet. Its length is the pixel size.</param>
    /// <param name="packetPixels">The pixels of the current packet that the method did not decode yet.</param>
    /// <param name="isRunPacket">A value that tells if the current packet is a run-length packet.</param>
    /// <returns>The number of complete pixels in the row. It is less than the row width only when the data ends.</returns>
    private static int ReadRleRow(BufferedReadStream stream, Span<byte> row, Span<byte> runPixel, ref int packetPixels, ref bool isRunPacket)
    {
        int bytesPerPixel = runPixel.Length;
        int width = row.Length / bytesPerPixel;
        int x = 0;
        while (x < width)
        {
            if (packetPixels == 0)
            {
                int packetHeader = stream.ReadByte();
                if (packetHeader < 0)
                {
                    return x;
                }

                packetPixels = (packetHeader & 127) + 1;
                isRunPacket = (packetHeader & 128) != 0;
                if (isRunPacket && stream.Read(runPixel) != bytesPerPixel)
                {
                    return x;
                }
            }

            int pixelCount = Math.Min(packetPixels, width - x);
            Span<byte> target = row.Slice(x * bytesPerPixel, pixelCount * bytesPerPixel);
            if (isRunPacket)
            {
                for (int i = 0; i < target.Length; i += bytesPerPixel)
                {
                    runPixel.CopyTo(target[i..]);
                }
            }
            else
            {
                int bytesRead = stream.Read(target);
                if (bytesRead != target.Length)
                {
                    return x + (bytesRead / bytesPerPixel);
                }
            }

            x += pixelCount;
            packetPixels -= pixelCount;
        }

        return width;
    }

    /// <summary>
    /// Converts one row of stored pixels to the destination pixel type, in stream order.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="source">The stored pixels. The conversion can change them.</param>
    /// <param name="destination">The destination pixels. Its length is the number of stored pixels.</param>
    private void ConvertRow<TPixel>(Span<byte> source, Span<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int count = destination.Length;
        switch (this.fileHeader.PixelDepth)
        {
            case 8:
                PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, source, destination, count);
                break;

            case 15:
            case 16:
                if (this.fileHeader.ImageType is TgaImageType.BlackAndWhite or TgaImageType.RleBlackAndWhite)
                {
                    // Without alpha bits, the second byte is not alpha, so each pixel is opaque.
                    if (!this.hasAlpha)
                    {
                        SetAlphaBits16(source, 0xFF00);
                    }

                    PixelOperations<TPixel>.Instance.FromLa16Bytes(this.configuration, source, destination, count);
                }
                else
                {
                    if (!this.hasAlpha)
                    {
                        SetAlphaBits16(source, 0x8000);
                    }

                    PixelOperations<TPixel>.Instance.FromBgra5551Bytes(this.configuration, source, destination, count);
                }

                break;

            case 24:
                PixelOperations<TPixel>.Instance.FromBgr24Bytes(this.configuration, source, destination, count);
                break;

            default:
                // ReadFileHeader allows only 32 bits here. Without alpha bits, the fourth byte is not alpha, so each pixel is opaque. Each B, G, R, A pixel is
                // one little-endian uint, and A is its high byte.
                if (!this.hasAlpha)
                {
                    Span<uint> values = MemoryMarshal.Cast<byte, uint>(source);
                    TensorPrimitives.BitwiseOr(values, 0xFF000000u, values);
                }

                PixelOperations<TPixel>.Instance.FromBgra32Bytes(this.configuration, source, destination, count);
                break;
        }
    }

    /// <summary>
    /// Converts one row of color map indices to pixels, in stream order. The conversion stops at the first index outside the stored color map.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="indices">The stored 8-bit or 16-bit little-endian indices.</param>
    /// <param name="destination">The destination pixels. Its length is the number of stored indices.</param>
    /// <param name="palette">The stored color map entries, converted to the pixel type.</param>
    /// <returns>The number of converted pixels. It is less than the row length only when an index is outside the color map.</returns>
    private int ConvertIndices<TPixel>(ReadOnlySpan<byte> indices, Span<TPixel> destination, ReadOnlySpan<TPixel> palette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // An index is absolute. CMapStart is the absolute index of the first stored entry. See TGA 2.0, field 4.1. The subtraction maps the index into the
        // stored entries. The unsigned compare rejects indices that are less than CMapStart and indices after the last stored entry.
        int firstEntry = this.fileHeader.CMapStart;
        uint entryCount = (uint)palette.Length;
        if (this.fileHeader.PixelDepth == 8)
        {
            for (int x = 0; x < destination.Length; x++)
            {
                uint entry = (uint)(indices[x] - firstEntry);
                if (entry >= entryCount)
                {
                    return x;
                }

                destination[x] = palette[(int)entry];
            }
        }
        else
        {
            // TGA stores a 16-bit index least significant byte first.
            ReadOnlySpan<ushort> wideIndices = MemoryMarshal.Cast<byte, ushort>(indices);
            for (int x = 0; x < destination.Length; x++)
            {
                uint entry = (uint)(wideIndices[x] - firstEntry);
                if (entry >= entryCount)
                {
                    return x;
                }

                destination[x] = palette[(int)entry];
            }
        }

        return destination.Length;
    }

    /// <summary>
    /// Sets the alpha bits of each 16-bit little-endian value, so each pixel is opaque. The alpha of a <see cref="Bgra5551"/> value is bit 15 (mask 0x8000).
    /// The alpha of a <see cref="La16"/> value is the high byte (mask 0xFF00).
    /// </summary>
    /// <param name="values">The stored 16-bit values.</param>
    /// <param name="alphaMask">The alpha bits of one value.</param>
    private static void SetAlphaBits16(Span<byte> values, ushort alphaMask)
    {
        Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(values);
        TensorPrimitives.BitwiseOr(samples, alphaMask, samples);
    }

    /// <summary>
    /// Reads the color map when the pixel data holds color map indices, and keeps its colors in the TGA metadata. Otherwise, the method skips the color map.
    /// True-color images do not use it. See TGA 2.0, field 2, page 6.
    /// </summary>
    /// <param name="stream">The stream, at the first color map entry.</param>
    /// <returns>The decoded colors in index order, or <see langword="null"/> if the pixel data does not use them.</returns>
    private Color[]? ReadColorMap(BufferedReadStream stream)
    {
        if (!this.usesColorMap)
        {
            // An image type without color map indices can also contain a color map. The method skips these bytes.
            if (this.fileHeader.CMapLength > 0)
            {
                // TGA stores each entry in whole bytes, including 15-bit entries.
                int colorMapPixelSizeInBytes = (this.fileHeader.CMapDepth + 7) / 8;
                stream.Skip(this.fileHeader.CMapLength * colorMapPixelSizeInBytes);
            }

            return null;
        }

        if (this.fileHeader.CMapLength == 0)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Missing tga color map length");
        }

        // CMapDepth describes each stored color, while PixelDepth describes the index in image data.
        // Fifteen-bit colors still occupy two whole bytes in the TGA color map.
        if (this.fileHeader.CMapDepth is not (8 or 15 or 16 or 24 or 32))
        {
            TgaThrowHelper.ThrowNotSupportedException($"Unsupported tga color map depth {this.fileHeader.CMapDepth}");
        }

        int bytesPerEntry = (this.fileHeader.CMapDepth + 7) >> 3;
        int byteLength = this.fileHeader.CMapLength * bytesPerEntry;

        // The raw bytes are needed only while creating the Color table retained by both Identify and Decode.
        using IMemoryOwner<byte> palette = this.memoryAllocator.Allocate<byte>(byteLength);
        Span<byte> raw = palette.GetSpan()[..byteLength];

        if (stream.Read(raw) != raw.Length)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Not enough data to read the color map");
        }

        // Fifteen-bit entries have no alpha. 16-bit entries use alpha only when the header declares it.
        if (bytesPerEntry == 2 && (this.fileHeader.CMapDepth == 15 || !this.hasAlpha))
        {
            SetAlphaBits16(raw, 0x8000);
        }

        Color[] colors = new Color[this.fileHeader.CMapLength];
        for (int i = 0; i < colors.Length; i++)
        {
            ref byte entry = ref raw[i * bytesPerEntry];
            colors[i] = bytesPerEntry switch
            {
                1 => Color.FromPixel(new L8(entry)),
                2 => Color.FromPixel(Unsafe.As<byte, Bgra5551>(ref entry)),
                3 => Color.FromPixel(Unsafe.As<byte, Bgr24>(ref entry)),
                _ => Color.FromPixel(Unsafe.As<byte, Bgra32>(ref entry))
            };
        }

        // CMapStart is the absolute index of the first stored color, not a byte offset;
        // the file contains exactly CMapLength entries beginning at that index.
        this.tgaMetadata!.ColorMapFirstEntryIndex = this.fileHeader.CMapStart;
        this.tgaMetadata.ColorMapEntryBitDepth = this.fileHeader.CMapDepth;
        this.tgaMetadata.ColorTable = colors;

        return colors;
    }

    /// <summary>
    /// Indicates whether the x coordinates needs to be inverted, to keep a top left origin.
    /// </summary>
    /// <param name="origin">The image origin.</param>
    /// <returns>True, if x coordinate needs to be inverted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InvertX(TgaImageOrigin origin) =>
        origin switch
        {
            TgaImageOrigin.TopRight => true,
            TgaImageOrigin.BottomRight => true,
            _ => false
        };

    /// <summary>
    /// Reads the tga file header from the stream and checks it.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <returns>The image origin.</returns>
    [MemberNotNull(nameof(metadata))]
    [MemberNotNull(nameof(tgaMetadata))]
    private TgaImageOrigin ReadFileHeader(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];

        if (stream.Read(buffer, 0, TgaFileHeader.Size) != TgaFileHeader.Size)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Not enough data to read the tga file header");
        }

        this.fileHeader = TgaFileHeader.Parse(buffer);
        TgaImageType imageType = this.fileHeader.ImageType;

        // Image type 0 has no image data, and the other unknown types have no defined layout. See TGA 2.0, field 3, page 6.
        if (imageType == TgaImageType.NoImageData || !imageType.IsValid())
        {
            TgaThrowHelper.ThrowInvalidImageContentException($"Invalid tga image type {(byte)imageType} found");
        }

        if (this.fileHeader.ColorMapType is not 0 and not 1)
        {
            TgaThrowHelper.ThrowNotSupportedException($"Unknown tga colormap type {this.fileHeader.ColorMapType} found");
        }

        // A color mapped image needs a color map.
        if (imageType.IsColorMapped() && this.fileHeader.ColorMapType == 0)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Missing tga color map");
        }

        if (this.fileHeader.Width == 0 || this.fileHeader.Height == 0)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Width and height must be greater than 0.");
        }

        // An 8-bit image that is not color mapped but has a color map stores color map indices. Thus the decode keeps the colors of the color map. Other
        // pixel depths store their colors directly.
        this.usesColorMap = imageType.IsColorMapped() || (this.fileHeader.PixelDepth == 8 && this.fileHeader.ColorMapType == 1);
        if (imageType.IsColorMapped())
        {
            if (this.fileHeader.PixelDepth is not 8 and not 16)
            {
                TgaThrowHelper.ThrowNotSupportedException($"Unsupported tga color map index depth {this.fileHeader.PixelDepth}");
            }
        }
        else if (this.fileHeader.PixelDepth is not 8 and not 15 and not 16 and not 24 and not 32)
        {
            TgaThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of tga files.");
        }

        this.Dimensions = new Size(this.fileHeader.Width, this.fileHeader.Height);

        this.metadata = new ImageMetadata();
        this.tgaMetadata = this.metadata.GetTgaMetadata();
        this.tgaMetadata.BitsPerPixel = (TgaBitsPerPixel)this.fileHeader.PixelDepth;
        this.tgaMetadata.Compression = imageType.IsRunLengthEncoded() ? TgaCompression.RunLength : TgaCompression.None;

        // TrueColor images with 32 bits per pixel are assumed to always have 8 bit alpha channel,
        // because some encoders do not set correctly the alpha bits in the image descriptor.
        int alphaBits = this.IsTrueColor32BitPerPixel(this.tgaMetadata.BitsPerPixel) ? 8 : this.fileHeader.ImageDescriptor & 0xf;
        if (alphaBits is not 0 and not 1 and not 8)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Invalid alpha channel bits");
        }

        this.tgaMetadata.AlphaChannelBits = (byte)alphaBits;
        this.hasAlpha = alphaBits > 0;

        // Bits 4 and 5 describe the image origin.
        return (TgaImageOrigin)((this.fileHeader.ImageDescriptor & 0x30) >> 4);
    }

    private bool IsTrueColor32BitPerPixel(TgaBitsPerPixel bitsPerPixel) => bitsPerPixel == TgaBitsPerPixel.Bit32 &&
                                                                           (this.fileHeader.ImageType == TgaImageType.TrueColor ||
                                                                            this.fileHeader.ImageType == TgaImageType.RleTrueColor);
}
