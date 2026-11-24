// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
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
        Image<TPixel>? image = null;

        try
        {
            TgaImageOrigin origin = this.ReadFileHeader(stream);
            stream.Skip(this.fileHeader.IdLength);

            // Parse the color map, if present.
            if (this.fileHeader.ColorMapType is not 0 and not 1)
            {
                TgaThrowHelper.ThrowNotSupportedException($"Unknown tga colormap type {this.fileHeader.ColorMapType} found");
            }

            if (this.fileHeader.Width <= 0 || this.fileHeader.Height <= 0)
            {
                TgaThrowHelper.ThrowInvalidImageContentException("Width and height must be greater than 0.");
            }

            image = new Image<TPixel>(this.configuration, this.fileHeader.Width, this.fileHeader.Height, this.metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            if (this.fileHeader.ColorMapType == 1)
            {
                // Identify and Decode retain the same decoded Color table; pixel expansion reads those entries directly.
                ReadOnlySpan<Color> colors = this.ReadColorMap(stream, this.tgaMetadata);

                if (this.fileHeader.ImageType == TgaImageType.RleColorMapped)
                {
                    this.ReadPalettedRle(
                        stream,
                        this.fileHeader.Width,
                        this.fileHeader.Height,
                        pixels,
                        colors,
                        origin);
                }
                else
                {
                    this.ReadPaletted(
                        stream,
                        this.fileHeader.Width,
                        this.fileHeader.Height,
                        pixels,
                        colors,
                        origin);
                }

                return image;
            }

            // Even if the image type indicates it is not a paletted image, it can still contain a palette. Skip those bytes.
            if (this.fileHeader.CMapLength > 0)
            {
                // TGA stores each entry in whole bytes, including 15-bit entries.
                int colorMapPixelSizeInBytes = (this.fileHeader.CMapDepth + 7) / 8;
                stream.Skip(this.fileHeader.CMapLength * colorMapPixelSizeInBytes);
            }

            switch (this.fileHeader.PixelDepth)
            {
                case 8:
                    if (this.fileHeader.ImageType.IsRunLengthEncoded())
                    {
                        this.ReadRle(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, 1, origin);
                    }
                    else
                    {
                        this.ReadMonoChrome(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                    }

                    break;

                case 15:
                case 16:
                    if (this.fileHeader.ImageType.IsRunLengthEncoded())
                    {
                        this.ReadRle(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, 2, origin);
                    }
                    else
                    {
                        this.ReadBgra16(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                    }

                    break;

                case 24:
                    if (this.fileHeader.ImageType.IsRunLengthEncoded())
                    {
                        this.ReadRle(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, 3, origin);
                    }
                    else
                    {
                        this.ReadBgr24(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                    }

                    break;

                case 32:
                    if (this.fileHeader.ImageType.IsRunLengthEncoded())
                    {
                        this.ReadRle(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, 4, origin);
                    }
                    else
                    {
                        this.ReadBgra32(stream, this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                    }

                    break;

                default:
                    TgaThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of tga files.");
                    break;
            }

            return image;
        }
        catch (IndexOutOfRangeException e)
        {
            image?.Dispose();
            throw new ImageFormatException("TGA image does not have a valid format.", e);
        }
        catch
        {
            image?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads a uncompressed TGA image with a palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="palette">The color palette.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadPaletted<TPixel>(
        BufferedReadStream stream,
        int width,
        int height,
        Buffer2D<TPixel> pixels,
        ReadOnlySpan<Color> palette,
        TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // ReadColorMap decoded each entry once; index lookup packs its retained Color into the requested TPixel.
        bool invertX = InvertX(origin);

        for (int y = 0; y < height; y++)
        {
            int newY = InvertY(y, height, origin);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(newY);

            if (invertX)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    if (!this.TryReadPaletteIndex(stream, out int index))
                    {
                        // The image data ended. The pixels that the decoder did not read keep the fill value.
                        return;
                    }

                    if (index >= 0)
                    {
                        pixelRow[x] = palette[index].ToPixel<TPixel>();
                    }
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    if (!this.TryReadPaletteIndex(stream, out int index))
                    {
                        // The image data ended. The pixels that the decoder did not read keep the fill value.
                        return;
                    }

                    if (index >= 0)
                    {
                        pixelRow[x] = palette[index].ToPixel<TPixel>();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads a run length encoded TGA image with a palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="palette">The color palette.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadPalettedRle<TPixel>(
        BufferedReadStream stream,
        int width,
        int height,
        Buffer2D<TPixel> pixels,
        ReadOnlySpan<Color> palette,
        TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // RLE packets repeat complete 8- or 16-bit indices, so their stride follows PixelDepth rather than CMapDepth.
        int indexBytes = this.fileHeader.PixelDepth / 8;
        using IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * indexBytes, AllocationOptions.Clean);
        Span<byte> bufferSpan = buffer.GetSpan();
        int decodedPixels = this.UncompressRle(stream, width, height, bufferSpan, indexBytes);

        // The decoded pixels are in stream order. The method writes only those pixels. Thus, if the image data is
        // damaged, the other pixels keep the fill value.
        for (int y = 0, rowStartIdx = 0; rowStartIdx < decodedPixels; y++, rowStartIdx += width)
        {
            int newY = InvertY(y, height, origin);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(newY);
            int rowPixels = Math.Min(width, decodedPixels - rowStartIdx);
            for (int x = 0; x < rowPixels; x++)
            {
                int idx = (rowStartIdx + x) * indexBytes;
                int index = this.GetPaletteIndex(indexBytes == 1 ? bufferSpan[idx] : BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan[idx..]));
                if (index >= 0)
                {
                    int newX = InvertX(x, width, origin);
                    pixelRow[newX] = palette[index].ToPixel<TPixel>();
                }
            }
        }
    }

    /// <summary>
    /// Reads a uncompressed monochrome TGA image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="origin">the image origin.</param>
    private void ReadMonoChrome<TPixel>(BufferedReadStream stream, int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (InvertX(origin))
        {
            for (int y = 0; y < height; y++)
            {
                int newY = InvertY(y, height, origin);
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);
                for (int x = width - 1; x >= 0; x--)
                {
                    if (!this.ReadL8Pixel(stream, x, pixelSpan))
                    {
                        return;
                    }
                }
            }

            return;
        }

        using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 1, 0);
        Span<byte> rowSpan = row.GetSpan();
        if (InvertY(origin))
        {
            for (int y = height - 1; y >= 0; y--)
            {
                if (!this.ReadL8Row(stream, width, pixels, rowSpan, y))
                {
                    return;
                }
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                if (!this.ReadL8Row(stream, width, pixels, rowSpan, y))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Reads a uncompressed TGA image where each pixels has 16 bit.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadBgra16<TPixel>(BufferedReadStream stream, int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool invertX = InvertX(origin);
        using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 2, 0);
        Span<byte> rowSpan = row.GetSpan();
        Span<byte> scratchBuffer = stackalloc byte[2];

        for (int y = 0; y < height; y++)
        {
            int newY = InvertY(y, height, origin);
            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);

            if (invertX)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int bytesRead = stream.Read(scratchBuffer);
                    if (bytesRead != 2)
                    {
                        // The image data ended. The pixels that the decoder did not read keep the fill value.
                        this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
                        return;
                    }

                    if (!this.hasAlpha)
                    {
                        scratchBuffer[1] |= 1 << 7;
                    }

                    if (this.fileHeader.ImageType == TgaImageType.BlackAndWhite)
                    {
                        pixelSpan[x] = TPixel.FromLa16(Unsafe.As<byte, La16>(ref MemoryMarshal.GetReference(scratchBuffer)));
                    }
                    else
                    {
                        pixelSpan[x] = TPixel.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref MemoryMarshal.GetReference(scratchBuffer)));
                    }
                }
            }
            else
            {
                int bytesRead = stream.Read(rowSpan);

                // A short read keeps the complete pixels of the row. The other pixels keep the fill value.
                int pixelCount = bytesRead / 2;
                Span<byte> pixelBytes = rowSpan[..(pixelCount * 2)];

                if (!this.hasAlpha)
                {
                    // We need to set the alpha component value to fully opaque.
                    for (int x = 1; x < pixelBytes.Length; x += 2)
                    {
                        pixelBytes[x] |= 1 << 7;
                    }
                }

                if (this.fileHeader.ImageType == TgaImageType.BlackAndWhite)
                {
                    PixelOperations<TPixel>.Instance.FromLa16Bytes(this.configuration, pixelBytes, pixelSpan, pixelCount);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromBgra5551Bytes(this.configuration, pixelBytes, pixelSpan, pixelCount);
                }

                if (bytesRead != rowSpan.Length)
                {
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Reads a uncompressed TGA image where each pixels has 24 bit.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadBgr24<TPixel>(BufferedReadStream stream, int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (InvertX(origin))
        {
            Span<byte> scratchBuffer = stackalloc byte[4];
            for (int y = 0; y < height; y++)
            {
                int newY = InvertY(y, height, origin);
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);
                for (int x = width - 1; x >= 0; x--)
                {
                    if (!this.ReadBgr24Pixel(stream, x, pixelSpan, scratchBuffer))
                    {
                        return;
                    }
                }
            }

            return;
        }

        using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, 0);
        Span<byte> rowSpan = row.GetSpan();

        if (InvertY(origin))
        {
            for (int y = height - 1; y >= 0; y--)
            {
                if (!this.ReadBgr24Row(stream, width, pixels, rowSpan, y))
                {
                    return;
                }
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                if (!this.ReadBgr24Row(stream, width, pixels, rowSpan, y))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Reads a uncompressed TGA image where each pixels has 32 bit.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadBgra32<TPixel>(BufferedReadStream stream, int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool invertX = InvertX(origin);

        Guard.NotNull(this.tgaMetadata);

        if (this.tgaMetadata.AlphaChannelBits == 8 && !invertX)
        {
            using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, 0);
            Span<byte> rowSpan = row.GetSpan();

            if (InvertY(origin))
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    if (!this.ReadBgra32Row(stream, width, pixels, rowSpan, y))
                    {
                        return;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    if (!this.ReadBgra32Row(stream, width, pixels, rowSpan, y))
                    {
                        return;
                    }
                }
            }

            return;
        }

        Span<byte> scratchBuffer = stackalloc byte[4];

        for (int y = 0; y < height; y++)
        {
            int newY = InvertY(y, height, origin);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(newY);
            if (invertX)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    if (!this.ReadBgra32Pixel(stream, x, pixelRow, scratchBuffer))
                    {
                        return;
                    }
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    if (!this.ReadBgra32Pixel(stream, x, pixelRow, scratchBuffer))
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads a run length encoded TGA image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    /// <param name="origin">The image origin.</param>
    private void ReadRle<TPixel>(BufferedReadStream stream, int width, int height, Buffer2D<TPixel> pixels, int bytesPerPixel, TgaImageOrigin origin)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel color = default;

        Guard.NotNull(this.tgaMetadata);

        byte alphaBits = this.tgaMetadata.AlphaChannelBits;
        using IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * bytesPerPixel, AllocationOptions.Clean);
        Span<byte> bufferSpan = buffer.GetSpan();
        int decodedPixels = this.UncompressRle(stream, width, height, bufferSpan, bytesPerPixel);

        // The decoded pixels are in stream order. The method writes only those pixels. Thus, if the image data is
        // damaged, the other pixels keep the fill value.
        for (int y = 0, rowStartPixel = 0; rowStartPixel < decodedPixels; y++, rowStartPixel += width)
        {
            int newY = InvertY(y, height, origin);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(newY);
            int rowStartIdx = rowStartPixel * bytesPerPixel;
            int rowPixels = Math.Min(width, decodedPixels - rowStartPixel);
            for (int x = 0; x < rowPixels; x++)
            {
                int idx = rowStartIdx + (x * bytesPerPixel);
                switch (bytesPerPixel)
                {
                    case 1:
                        color = TPixel.FromL8(Unsafe.As<byte, L8>(ref bufferSpan[idx]));
                        break;
                    case 2:
                        if (!this.hasAlpha)
                        {
                            // Set alpha value to 1, to treat it as opaque for Bgra5551.
                            bufferSpan[idx + 1] = (byte)(bufferSpan[idx + 1] | 128);
                        }

                        if (this.fileHeader.ImageType == TgaImageType.RleBlackAndWhite)
                        {
                            color = TPixel.FromLa16(Unsafe.As<byte, La16>(ref bufferSpan[idx]));
                        }
                        else
                        {
                            color = TPixel.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref bufferSpan[idx]));
                        }

                        break;
                    case 3:
                        color = TPixel.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                        break;
                    case 4:
                        if (this.hasAlpha)
                        {
                            color = TPixel.FromBgra32(Unsafe.As<byte, Bgra32>(ref bufferSpan[idx]));
                        }
                        else
                        {
                            byte alpha = alphaBits == 0 ? byte.MaxValue : bufferSpan[idx + 3];
                            color = TPixel.FromBgra32(new Bgra32(bufferSpan[idx + 2], bufferSpan[idx + 1], bufferSpan[idx], alpha));
                        }

                        break;
                }

                int newX = InvertX(x, width, origin);
                pixelRow[newX] = color;
            }
        }
    }

    /// <summary>
    /// Reads the declared color map and retains its colors in TGA metadata.
    /// </summary>
    /// <param name="stream">The stream positioned at the first color map entry.</param>
    /// <param name="metadata">The metadata that receives the decoded color map.</param>
    /// <returns>The decoded colors in index order.</returns>
    private Color[] ReadColorMap(BufferedReadStream stream, TgaMetadata metadata)
    {
        if (this.fileHeader.CMapLength == 0)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Missing tga color map length");
        }

        // CMapDepth describes each stored color, while PixelDepth describes the index in image data.
        // Fifteen-bit colors still occupy two whole bytes in the TGA color map.
        int bytesPerEntry = this.fileHeader.CMapDepth switch
        {
            8 => 1,
            15 or 16 => 2,
            24 => 3,
            32 => 4,
            _ => throw new NotSupportedException($"Unsupported tga color map depth {this.fileHeader.CMapDepth}")
        };

        if (this.fileHeader.PixelDepth is not 8 and not 16)
        {
            TgaThrowHelper.ThrowNotSupportedException($"Unsupported tga color map index depth {this.fileHeader.PixelDepth}");
        }

        int byteLength = this.fileHeader.CMapLength * bytesPerEntry;

        // The raw bytes are needed only while creating the Color table retained by both Identify and Decode.
        using IMemoryOwner<byte> palette = this.memoryAllocator.Allocate<byte>(byteLength);
        Span<byte> raw = palette.GetSpan()[..byteLength];

        if (stream.Read(raw) != raw.Length)
        {
            TgaThrowHelper.ThrowInvalidImageContentException("Not enough data to read the color map");
        }

        Color[] colors = new Color[this.fileHeader.CMapLength];
        for (int i = 0; i < colors.Length; i++)
        {
            Span<byte> entry = raw.Slice(i * bytesPerEntry, bytesPerEntry);
            colors[i] = bytesPerEntry switch
            {
                1 => Color.FromPixel(new L8(entry[0])),
                2 => Color.FromPixel(this.ReadPaletteBgra5551(entry)),
                3 => Color.FromPixel(Unsafe.As<byte, Bgr24>(ref MemoryMarshal.GetReference(entry))),
                _ => Color.FromPixel(Unsafe.As<byte, Bgra32>(ref MemoryMarshal.GetReference(entry)))
            };
        }

        // CMapStart is the absolute index of the first stored color, not a byte offset;
        // the file contains exactly CMapLength entries beginning at that index.
        metadata.ColorMapFirstEntryIndex = this.fileHeader.CMapStart;
        metadata.ColorMapEntryBitDepth = this.fileHeader.CMapDepth;
        metadata.ColorTable = colors;

        return colors;
    }

    /// <summary>
    /// Reads a 15- or 16-bit palette entry with the same alpha convention as pixel decoding.
    /// </summary>
    /// <param name="entry">The two stored bytes.</param>
    /// <returns>The palette color.</returns>
    private Bgra5551 ReadPaletteBgra5551(ReadOnlySpan<byte> entry)
    {
        Bgra5551 color = new() { PackedValue = BinaryPrimitives.ReadUInt16LittleEndian(entry) };

        if (this.fileHeader.CMapDepth == 15 || !this.hasAlpha)
        {
            // Fifteen-bit entries have no alpha; 16-bit entries use it only when declared.
            color.PackedValue |= 0x8000;
        }

        return color;
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadFileHeader(stream);

        if (this.fileHeader.ColorMapType == 1)
        {
            stream.Skip(this.fileHeader.IdLength);

            this.ReadColorMap(stream, this.tgaMetadata);
        }

        return new ImageInfo(
            new Size(this.fileHeader.Width, this.fileHeader.Height),
            this.metadata);
    }

    /// <summary>
    /// Reads one row of 8-bit gray pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="row">The row buffer for the encoded bytes.</param>
    /// <param name="y">The destination row.</param>
    /// <returns><see langword="true"/> if the row is complete. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadL8Row<TPixel>(BufferedReadStream stream, int width, Buffer2D<TPixel> pixels, Span<byte> row, int y)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesRead = stream.Read(row);
        Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
        if (bytesRead != row.Length)
        {
            // The pixels of the row that the decoder read stay. The other pixels keep the fill value.
            PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, row[..bytesRead], pixelSpan, bytesRead);
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
            return false;
        }

        PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, row, pixelSpan, width);
        return true;
    }

    /// <summary>
    /// Reads one 8-bit gray pixel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="x">The destination column.</param>
    /// <param name="pixelSpan">The destination row.</param>
    /// <returns><see langword="true"/> if the method reads the pixel. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadL8Pixel<TPixel>(BufferedReadStream stream, int x, Span<TPixel> pixelSpan)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int value = stream.ReadByte();
        if (value < 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a gray pixel");
            return false;
        }

        pixelSpan[x] = TPixel.FromL8(new L8((byte)value));
        return true;
    }

    /// <summary>
    /// Reads one 24-bit BGR pixel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="x">The destination column.</param>
    /// <param name="pixelSpan">The destination row.</param>
    /// <param name="scratchBuffer">The buffer for the encoded bytes.</param>
    /// <returns><see langword="true"/> if the method reads the pixel. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBgr24Pixel<TPixel>(BufferedReadStream stream, int x, Span<TPixel> pixelSpan, Span<byte> scratchBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesRead = stream.Read(scratchBuffer, 0, 3);
        if (bytesRead != 3)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a bgr pixel");
            return false;
        }

        pixelSpan[x] = TPixel.FromBgr24(Unsafe.As<byte, Bgr24>(ref MemoryMarshal.GetReference(scratchBuffer)));
        return true;
    }

    /// <summary>
    /// Reads one row of 24-bit BGR pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="row">The row buffer for the encoded bytes.</param>
    /// <param name="y">The destination row.</param>
    /// <returns><see langword="true"/> if the row is complete. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBgr24Row<TPixel>(BufferedReadStream stream, int width, Buffer2D<TPixel> pixels, Span<byte> row, int y)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesRead = stream.Read(row);
        Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
        if (bytesRead != row.Length)
        {
            // The complete pixels of the row stay. The other pixels keep the fill value.
            int pixelCount = bytesRead / 3;
            PixelOperations<TPixel>.Instance.FromBgr24Bytes(this.configuration, row[..(pixelCount * 3)], pixelSpan, pixelCount);
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
            return false;
        }

        PixelOperations<TPixel>.Instance.FromBgr24Bytes(this.configuration, row, pixelSpan, width);
        return true;
    }

    /// <summary>
    /// Reads one 32-bit BGRA pixel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="x">The destination column.</param>
    /// <param name="pixelRow">The destination row.</param>
    /// <param name="scratchBuffer">The buffer for the encoded bytes.</param>
    /// <returns><see langword="true"/> if the method reads the pixel. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBgra32Pixel<TPixel>(BufferedReadStream stream, int x, Span<TPixel> pixelRow, Span<byte> scratchBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesRead = stream.Read(scratchBuffer, 0, 4);
        if (bytesRead != 4)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a bgra pixel");
            return false;
        }

        Guard.NotNull(this.tgaMetadata);

        byte alpha = this.tgaMetadata.AlphaChannelBits == 0 ? byte.MaxValue : scratchBuffer[3];
        pixelRow[x] = TPixel.FromBgra32(new Bgra32(scratchBuffer[2], scratchBuffer[1], scratchBuffer[0], alpha));
        return true;
    }

    /// <summary>
    /// Reads one row of 32-bit BGRA pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="row">The row buffer for the encoded bytes.</param>
    /// <param name="y">The destination row.</param>
    /// <returns><see langword="true"/> if the row is complete. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBgra32Row<TPixel>(BufferedReadStream stream, int width, Buffer2D<TPixel> pixels, Span<byte> row, int y)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesRead = stream.Read(row);
        Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
        if (bytesRead != row.Length)
        {
            // The complete pixels of the row stay. The other pixels keep the fill value.
            int pixelCount = bytesRead / 4;
            PixelOperations<TPixel>.Instance.FromBgra32Bytes(this.configuration, row[..(pixelCount * 4)], pixelSpan, pixelCount);
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel row");
            return false;
        }

        PixelOperations<TPixel>.Instance.FromBgra32Bytes(this.configuration, row, pixelSpan, width);
        return true;
    }

    /// <summary>
    /// Reads an image index and locates its entry in the stored color map.
    /// </summary>
    /// <param name="stream">The image data stream.</param>
    /// <param name="relativeIndex">
    /// The index relative to the first stored color map entry, or -1 if the index is outside the color map.
    /// </param>
    /// <returns><see langword="true"/> if the method reads the index. <see langword="false"/> if the image data ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadPaletteIndex(BufferedReadStream stream, out int relativeIndex)
    {
        relativeIndex = -1;
        int low = stream.ReadByte();
        if (low < 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read color index");
            return false;
        }

        int index = low;
        if (this.fileHeader.PixelDepth == 16)
        {
            // TGA stores a 16-bit image index least-significant byte first.
            int high = stream.ReadByte();
            if (high < 0)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read color index");
                return false;
            }

            index |= high << 8;
        }

        relativeIndex = this.GetPaletteIndex(index);
        return true;
    }

    /// <summary>
    /// Converts an absolute color-map index to an entry in the colors stored in this file.
    /// An index outside the color map is damaged image data. In the IgnoreImageData mode, the method returns -1, and
    /// the caller keeps the fill value for the pixel.
    /// </summary>
    /// <param name="index">The image's absolute palette index.</param>
    /// <returns>The index relative to the first stored color map entry, or -1 if the index is outside the color map.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetPaletteIndex(int index)
    {
        // Subtraction maps the absolute image index into the stored table. Casting to unsigned
        // rejects both indices below CMapStart and indices beyond its CMapLength entries.
        int relativeIndex = index - this.fileHeader.CMapStart;
        if ((uint)relativeIndex >= this.fileHeader.CMapLength)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Tga color index is outside the color map");
            return -1;
        }

        return relativeIndex;
    }

    /// <summary>
    /// Produce uncompressed tga data from a run length encoded stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="buffer">Buffer for uncompressed data.</param>
    /// <param name="bytesPerPixel">The bytes used per pixel.</param>
    /// <returns>
    /// The number of pixels decoded in stream order. This is less than the pixel count of the image only when the
    /// image data is damaged and the decoder ignores image data errors.
    /// </returns>
    private int UncompressRle(BufferedReadStream stream, int width, int height, Span<byte> buffer, int bytesPerPixel)
    {
        int uncompressedPixels = 0;
        Span<byte> pixel = stackalloc byte[bytesPerPixel];
        int totalPixels = width * height;
        while (uncompressedPixels < totalPixels)
        {
            int packetHeader = stream.ReadByte();
            if (packetHeader < 0)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a run length packet");
                return uncompressedPixels;
            }

            // The low 7 bits store the pixel count of the packet minus one.
            // A packet must not write past the last pixel of the image.
            int packetPixels = (packetHeader & 127) + 1;
            if (packetPixels > totalPixels - uncompressedPixels)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Run length packet exceeds the image size");
                packetPixels = totalPixels - uncompressedPixels;
            }

            // The high bit of a run length packet is set to 1.
            if ((packetHeader >> 7) == 1)
            {
                int bytesRead = stream.Read(pixel);
                if (bytesRead != bytesPerPixel)
                {
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel from the stream");
                    return uncompressedPixels;
                }

                int bufferIdx = uncompressedPixels * bytesPerPixel;
                for (int i = 0; i < packetPixels; i++, uncompressedPixels++)
                {
                    pixel.CopyTo(buffer[bufferIdx..]);
                    bufferIdx += bytesPerPixel;
                }
            }
            else
            {
                // Non-run-length encoded packet.
                int bufferIdx = uncompressedPixels * bytesPerPixel;
                for (int i = 0; i < packetPixels; i++, uncompressedPixels++)
                {
                    int bytesRead = stream.Read(pixel);
                    if (bytesRead != bytesPerPixel)
                    {
                        this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read a pixel from the stream");
                        return uncompressedPixels;
                    }

                    pixel.CopyTo(buffer[bufferIdx..]);
                    bufferIdx += bytesPerPixel;
                }
            }
        }

        return uncompressedPixels;
    }

    /// <summary>
    /// Returns the y- value based on the given height.
    /// </summary>
    /// <param name="y">The y- value representing the current row.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="origin">The image origin.</param>
    /// <returns>The <see cref="int"/> representing the inverted value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InvertY(int y, int height, TgaImageOrigin origin)
    {
        if (InvertY(origin))
        {
            return height - y - 1;
        }

        return y;
    }

    /// <summary>
    /// Indicates whether the y coordinates needs to be inverted, to keep a top left origin.
    /// </summary>
    /// <param name="origin">The image origin.</param>
    /// <returns>True, if y coordinate needs to be inverted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InvertY(TgaImageOrigin origin) => origin switch
    {
        TgaImageOrigin.BottomLeft => true,
        TgaImageOrigin.BottomRight => true,
        _ => false
    };

    /// <summary>
    /// Returns the x- value based on the given width.
    /// </summary>
    /// <param name="x">The x- value representing the current column.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="origin">The image origin.</param>
    /// <returns>The <see cref="int"/> representing the inverted value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InvertX(int x, int width, TgaImageOrigin origin)
    {
        if (InvertX(origin))
        {
            return width - x - 1;
        }

        return x;
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
    /// Reads the tga file header from the stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <returns>The image origin.</returns>
    [MemberNotNull(nameof(metadata))]
    [MemberNotNull(nameof(tgaMetadata))]
    private TgaImageOrigin ReadFileHeader(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];

        stream.Read(buffer, 0, TgaFileHeader.Size);
        this.fileHeader = TgaFileHeader.Parse(buffer);
        this.Dimensions = new Size(this.fileHeader.Width, this.fileHeader.Height);

        this.metadata = new ImageMetadata();
        this.tgaMetadata = this.metadata.GetTgaMetadata();
        this.tgaMetadata.BitsPerPixel = (TgaBitsPerPixel)this.fileHeader.PixelDepth;
        this.tgaMetadata.Compression = this.fileHeader.ImageType.IsRunLengthEncoded() ? TgaCompression.RunLength : TgaCompression.None;

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
