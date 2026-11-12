// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Performs the bitmap decoding operation.
/// </summary>
/// <remarks>
/// A useful decoding source example can be found at <see href="https://dxr.mozilla.org/mozilla-central/source/image/decoders/nsBMPDecoder.cpp"/>
/// </remarks>
internal sealed class BmpDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// The default mask for the red part of the color for 16 bit rgb bitmaps.
    /// </summary>
    private const int DefaultRgb16RMask = 0x7C00;

    /// <summary>
    /// The default mask for the green part of the color for 16 bit rgb bitmaps.
    /// </summary>
    private const int DefaultRgb16GMask = 0x3E0;

    /// <summary>
    /// The default mask for the blue part of the color for 16 bit rgb bitmaps.
    /// </summary>
    private const int DefaultRgb16BMask = 0x1F;

    /// <summary>
    /// RLE flag value that indicates following byte has special meaning.
    /// </summary>
    private const int RleCommand = 0x00;

    /// <summary>
    /// RLE flag value marking end of a scan line.
    /// </summary>
    private const int RleEndOfLine = 0x00;

    /// <summary>
    /// RLE flag value marking end of bitmap data.
    /// </summary>
    private const int RleEndOfBitmap = 0x01;

    /// <summary>
    /// RLE flag value marking the start of [x,y] offset instruction.
    /// </summary>
    private const int RleDelta = 0x02;

    /// <summary>
    /// The metadata.
    /// </summary>
    private ImageMetadata? metadata;

    /// <summary>
    /// The bitmap specific metadata.
    /// </summary>
    private BmpMetadata? bmpMetadata;

    /// <summary>
    /// The file header containing general information.
    /// </summary>
    private BmpFileHeader? fileHeader;

    /// <summary>
    /// The info header containing detailed information about the bitmap.
    /// </summary>
    private BmpInfoHeader infoHeader;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// How to deal with skipped pixels,
    /// which can occur during decoding run length encoded bitmaps.
    /// </summary>
    private readonly RleSkippedPixelHandling rleSkippedPixelHandling;

    /// <inheritdoc cref="BmpDecoderOptions.ProcessedAlphaMask"/>
    private readonly bool processedAlphaMask;

    /// <inheritdoc cref="BmpDecoderOptions.SkipFileHeader"/>
    private readonly bool skipFileHeader;

    /// <inheritdoc cref="BmpDecoderOptions.UseDoubleHeight"/>
    private readonly bool isDoubleHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public BmpDecoderCore(BmpDecoderOptions options)
        : base(options.GeneralOptions)
    {
        this.rleSkippedPixelHandling = options.RleSkippedPixelHandling;
        this.configuration = options.GeneralOptions.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
        this.processedAlphaMask = options.ProcessedAlphaMask;
        this.skipFileHeader = options.SkipFileHeader;
        this.isDoubleHeight = options.UseDoubleHeight;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        Image<TPixel>? image = null;
        try
        {
            this.ReadImageHeaders(stream, out bool inverted, out bool isIndexed, out Color[] palette);

            // The pixel decode supports these compressions only. Check before the allocation, so an unsupported file
            // requests no pixel buffer.
            if (this.infoHeader.Compression is not (BmpCompression.RGB or BmpCompression.RLE24 or BmpCompression.RLE8
                or BmpCompression.RLE4 or BmpCompression.BitFields or BmpCompression.BI_ALPHABITFIELDS))
            {
                BmpThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of bitmap files.");
            }

            if (isIndexed)
            {
                this.ConvertColorPalette<TPixel>(palette, this.metadata.IccProfile);
            }

            image = new Image<TPixel>(this.configuration, this.infoHeader.Width, this.infoHeader.Height, this.metadata);

            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            // The pixel decode does not throw for bad pixel data. When the data ends before the image is complete, the
            // decode stops, and the image keeps the pixels decoded before the end. This is the same for each
            // compression, and the same as for PNG and JPEG data. The image buffer is cleared at allocation, so the
            // pixels after the end are transparent black. The allocator limits protect against large dimensions.
            this.ReadPixelData(stream, pixels, palette, this.bmpMetadata.InfoHeaderType, inverted);

            if (!isIndexed)
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
    }

    /// <summary>
    /// Decodes the pixel data with the decoder for the compression and the bit depth of the bitmap.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="palette">The palette of an indexed bitmap.</param>
    /// <param name="infoHeaderType">The info header type of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadPixelData<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        Color[] palette,
        BmpInfoHeaderType infoHeaderType,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ushort bitsPerPixel = this.infoHeader.BitsPerPixel;

        switch (this.infoHeader.Compression)
        {
            case BmpCompression.RGB when bitsPerPixel is 32 && infoHeaderType is BmpInfoHeaderType.WinVersion3:
                this.ReadRgb32Slow(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);

                break;

            case BmpCompression.RGB when bitsPerPixel is 32:
                this.ReadRgb32Fast(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);

                break;

            case BmpCompression.RGB when bitsPerPixel is 24:
                this.ReadRgb24(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);

                break;

            case BmpCompression.RGB when bitsPerPixel is 16:
                this.ReadBitFields(
                    stream,
                    pixels,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    inverted,
                    16,
                    DefaultRgb16RMask,
                    DefaultRgb16GMask,
                    DefaultRgb16BMask,
                    0);

                break;

            case BmpCompression.RGB when bitsPerPixel is > 0 and <= 8 && this.processedAlphaMask:
                this.ReadRgbPaletteWithAlphaMask(
                    stream,
                    pixels,
                    palette,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    this.infoHeader.BitsPerPixel,
                    inverted);

                break;

            case BmpCompression.RGB when bitsPerPixel is > 0 and <= 8:
                this.ReadRgbPalette(
                    stream,
                    pixels,
                    palette,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    this.infoHeader.BitsPerPixel,
                    inverted);

                break;

            case BmpCompression.RLE24:
                this.ReadRle24(stream, pixels, this.infoHeader.Width, inverted);

                break;

            case BmpCompression.RLE8:
            case BmpCompression.RLE4:
                this.ReadRle(stream, this.infoHeader.Compression, pixels, palette, this.infoHeader.Width, inverted);

                break;

            // Decode checks the compression before this method, so no other compression comes here.
            case BmpCompression.BitFields:
            case BmpCompression.BI_ALPHABITFIELDS:
                this.ReadBitFields(stream, pixels, inverted);

                break;
        }
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadImageHeaders(stream, out _, out bool isIndexed, out Color[] palette);

        if (isIndexed)
        {
            // Identify has no destination pixel format; RgbaVector preserves converted colors as unassociated floating-point values.
            this.ConvertColorPalette<RgbaVector>(palette, this.metadata.IccProfile);
        }

        return new ImageInfo(new Size(this.infoHeader.Width, this.infoHeader.Height), this.metadata);
    }

    /// <summary>
    /// Converts an indexed BMP palette through its embedded ICC profile for the requested destination pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="palette">The palette shared with metadata and pixel expansion.</param>
    /// <param name="profile">The embedded ICC profile.</param>
    private void ConvertColorPalette<TPixel>(Color[] palette, IccProfile? profile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!IccProfile.TryGetIccProfileForColorConversion(profile, out IccProfile? sourceProfile))
        {
            return;
        }

        ColorConversionOptions options = new()
        {
            SourceIccProfile = sourceProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = this.memoryAllocator
        };

        ColorProfileConverter converter = new(options);
        converter.Convert<TPixel>(palette);
    }

    /// <summary>
    /// Converts the palette to <typeparamref name="TPixel"/> in a table with an entry for each index value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An index has <paramref name="bitsPerIndex"/> bits, so it is less than 2^bitsPerIndex. The table has that many
    /// entries. Thus each index in the pixel data selects an entry, and the pixel loops need no bounds checks.
    /// </para>
    /// <para>
    /// A file palette can have fewer colors than the index range. An index past the file palette has no defined color,
    /// so its entry is opaque black. A file palette can also have more colors than the index range, when the bit depth
    /// of the header does not limit it. The pixel data cannot select those colors, so the table leaves them out.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="colors">The file palette.</param>
    /// <param name="bitsPerIndex">The bit count of one index: 1, 2, 4 or 8.</param>
    /// <returns>The table. The caller disposes it.</returns>
    private IMemoryOwner<TPixel> CreatePaletteTable<TPixel>(Color[] colors, int bitsPerIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The callers pass the bit depth of a palette bitmap, or 8 for RLE data. The header check limits the bit
        // depth of a palette bitmap to 1, 2, 4 or 8. Thus the table has at most 256 entries, for each file palette.
        DebugGuard.IsTrue(bitsPerIndex is 1 or 2 or 4 or 8, "An index has 1, 2, 4 or 8 bits.");
        int entryCount = 1 << bitsPerIndex;
        int colorCount = Math.Min(colors.Length, entryCount);

        IMemoryOwner<TPixel> table = this.memoryAllocator.Allocate<TPixel>(entryCount);
        Span<TPixel> tableSpan = table.GetSpan();
        Color.ToPixel(colors.AsSpan(0, colorCount), tableSpan);
        tableSpan[colorCount..].Fill(TPixel.FromRgba32(new Rgba32(0, 0, 0, byte.MaxValue)));

        return table;
    }

    /// <summary>
    /// Returns the y- value based on the given height.
    /// </summary>
    /// <param name="y">The y- value representing the current row.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <returns>The <see cref="int"/> representing the inverted value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Invert(int y, int height, bool inverted) => (!inverted) ? height - y - 1 : y;

    /// <summary>
    /// Calculates the amount of bytes to pad a row.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="componentCount">The pixel component count.</param>
    /// <returns>
    /// The padding.
    /// </returns>
    private static int CalculatePadding(int width, int componentCount)
    {
        int padding = (width * componentCount) % 4;

        if (padding != 0)
        {
            padding = 4 - padding;
        }

        return padding;
    }

    /// <summary>
    /// Decodes a bitmap containing the BITFIELDS Compression type. For each color channel, there will be a bitmask
    /// which will be used to determine which bits belong to that channel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadBitFields<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Every depth other than 16 uses 32-bit samples. The size check in Decode uses the same rule.
        int bitsPerPixel = this.infoHeader.BitsPerPixel is 16 ? 16 : 32;
        uint redMask = (uint)this.infoHeader.RedMask;
        uint greenMask = (uint)this.infoHeader.GreenMask;
        uint blueMask = (uint)this.infoHeader.BlueMask;
        uint alphaMask = (uint)this.infoHeader.AlphaMask;

        // These masks store each pixel as the bytes B, G, R, A. That is the layout of a 32-bit RGB bitmap with alpha,
        // so the bulk BGRA conversion reads the row with no unpack.
        if (bitsPerPixel is 32 && redMask is 0x00FF0000 && greenMask is 0x0000FF00 && blueMask is 0x000000FF && alphaMask is 0xFF000000)
        {
            this.ReadRgb32Fast(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
            return;
        }

        this.ReadBitFields(
            stream,
            pixels,
            this.infoHeader.Width,
            this.infoHeader.Height,
            inverted,
            bitsPerPixel,
            redMask,
            greenMask,
            blueMask,
            alphaMask);
    }

    /// <summary>
    /// Decodes a bitmap that stores each pixel as one 16-bit or 32-bit sample, with a bit mask for each channel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="bitsPerPixel">The sample width: 16 or 32.</param>
    /// <param name="redMask">The bitmask for the red channel.</param>
    /// <param name="greenMask">The bitmask for the green channel.</param>
    /// <param name="blueMask">The bitmask for the blue channel.</param>
    /// <param name="alphaMask">The bitmask for the alpha channel, or 0 for an opaque bitmap.</param>
    private void ReadBitFields<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        bool inverted,
        int bitsPerPixel,
        uint redMask,
        uint greenMask,
        uint blueMask,
        uint alphaMask)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        BmpBitFields fields = CreateBitFields<TPixel>(redMask, greenMask, blueMask, alphaMask);

        // A channel with more than 8 bits keeps its precision through Vector4. Each other mask fits a 4-byte pixel.
        if (bitsPerPixel is 16)
        {
            if (fields.IsByteRange)
            {
                _ = this.ReadBitFields<TPixel, BmpBitFields.Sample16Operator>(stream, pixels, width, height, inverted, fields);
            }
            else
            {
                this.ReadBitFieldsScaled<TPixel, BmpBitFields.Sample16Operator>(stream, pixels, width, height, inverted, fields);
            }
        }
        else if (fields.IsByteRange)
        {
            _ = this.ReadBitFields<TPixel, BmpBitFields.Sample32Operator>(stream, pixels, width, height, inverted, fields);
        }
        else
        {
            this.ReadBitFieldsScaled<TPixel, BmpBitFields.Sample32Operator>(stream, pixels, width, height, inverted, fields);
        }
    }

    /// <summary>
    /// Creates the bit fields that decode to <typeparamref name="TPixel"/>.
    /// </summary>
    /// <remarks>
    /// For <see cref="Bgra32"/> the fields write B, G, R, A pixels. For each other pixel type they write R, G, B, A
    /// pixels, the order of <see cref="Rgba32"/>. <see cref="ReadBitFields{TPixel, TSample}"/> uses the same rule to
    /// write the image row directly for these two pixel types.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="redMask">The bitmask for the red channel.</param>
    /// <param name="greenMask">The bitmask for the green channel.</param>
    /// <param name="blueMask">The bitmask for the blue channel.</param>
    /// <param name="alphaMask">The bitmask for the alpha channel, or 0 for an opaque bitmap.</param>
    /// <returns>The bit fields.</returns>
    private static BmpBitFields CreateBitFields<TPixel>(uint redMask, uint greenMask, uint blueMask, uint alphaMask)
        where TPixel : unmanaged, IPixel<TPixel>
        => new(redMask, greenMask, blueMask, alphaMask, typeof(TPixel) == typeof(Bgra32));

    /// <summary>
    /// Decodes a bit-field bitmap whose channels each have 8 bits or fewer.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TSample">The stored sample width.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="fields">The channel masks, from <see cref="CreateBitFields{TPixel}"/>.</param>
    /// <param name="stopOnAlpha">
    /// Whether to stop at the first row with a fourth byte that is not 0. Only <see cref="ReadRgb32Slow{TPixel}"/> sets
    /// it, for 32-bit samples stored as B, G, R and a fourth byte.
    /// </param>
    /// <returns>
    /// <see langword="false"/> if <paramref name="stopOnAlpha"/> stopped the decode. Else <see langword="true"/>.
    /// </returns>
    private bool ReadBitFields<TPixel, TSample>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        bool inverted,
        BmpBitFields fields,
        bool stopOnAlpha = false)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : struct, BmpBitFields.ISampleOperator
    {
        int pixelBytes = width * TSample.BytesPerSample;

        // Stored rows start on four-byte boundaries.
        int stride = (pixelBytes + 3) & ~3;
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(stride);
        Span<byte> rowSpan = row.GetSpan();

        // The fields write Rgba32 pixels, or Bgra32 pixels for a Bgra32 image (see CreateBitFields). For those two
        // pixel types, the fields write the image row, and no conversion pass follows. For each other pixel type,
        // the fields write an Rgba32 row, and the bulk conversion writes the image row.
        bool direct = typeof(TPixel) == typeof(Rgba32) || typeof(TPixel) == typeof(Bgra32);
        using IMemoryOwner<Rgba32>? rgbaRow = direct ? null : this.memoryAllocator.Allocate<Rgba32>(width);

        for (int y = 0; y < height; y++)
        {
            if (!TryReadPixelBytes(stream, rowSpan, pixelBytes))
            {
                return true;
            }

            if (stopOnAlpha && HasNonZeroAlpha(rowSpan[..pixelBytes]))
            {
                return false;
            }

            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));

            if (rgbaRow is null)
            {
                fields.Unpack<TSample>(rowSpan, MemoryMarshal.Cast<TPixel, uint>(pixelRow));
            }
            else
            {
                Span<Rgba32> rgbaSpan = rgbaRow.GetSpan();
                fields.Unpack<TSample>(rowSpan, MemoryMarshal.Cast<Rgba32, uint>(rgbaSpan));
                PixelOperations<TPixel>.Instance.FromRgba32(this.configuration, rgbaSpan, pixelRow);
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes a bit-field bitmap that has a channel with more than 8 bits.
    /// </summary>
    /// <remarks>
    /// The fields scale each channel to the range from 0 to 1. Thus a channel with more than 8 bits keeps its
    /// precision for a pixel type that has more than 8 bits for each channel.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TSample">The stored sample width.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="fields">The channel masks.</param>
    private void ReadBitFieldsScaled<TPixel, TSample>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        bool inverted,
        BmpBitFields fields)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : struct, BmpBitFields.ISampleOperator
    {
        int pixelBytes = width * TSample.BytesPerSample;
        int stride = (pixelBytes + 3) & ~3;
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(stride);
        using IMemoryOwner<Vector4> vectorRow = this.memoryAllocator.Allocate<Vector4>(width);
        Span<byte> rowSpan = row.GetSpan();
        Span<Vector4> vectorSpan = vectorRow.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!TryReadPixelBytes(stream, rowSpan, pixelBytes))
            {
                return;
            }

            fields.UnpackScaled<TSample>(rowSpan, vectorSpan);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, vectorSpan, pixelRow);
        }
    }

    /// <summary>
    /// Decodes an RLE4 or RLE8 bitmap straight into the image rows.
    /// </summary>
    /// <remarks>
    /// <para>The data is a sequence of commands. Each command starts with two bytes, [a, b]:</para>
    /// <list type="bullet">
    /// <item><description>a &gt; 0: an encoded run of a pixels. RLE8: each pixel has index b. RLE4: the pixels
    /// alternate between the high nibble and the low nibble of b, high nibble first.</description></item>
    /// <item><description>a = 0, b = 0: end of line. The rest of the row is skipped.</description></item>
    /// <item><description>a = 0, b = 1: end of bitmap. The rest of the image is skipped.</description></item>
    /// <item><description>a = 0, b = 2: delta. Two bytes dx and dy follow. The data skips dy rows and dx
    /// pixels.</description></item>
    /// <item><description>a = 0, b &gt; 2: an absolute run of b indices. RLE8 stores one index in each byte. RLE4
    /// stores two in each byte, high nibble first. The run data is padded to a two-byte boundary.</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="compression">The compression type. Either RLE4 or RLE8.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="colors">The decoded pixel palette.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRle<TPixel>(
        BufferedReadStream stream,
        BmpCompression compression,
        Buffer2D<TPixel> pixels,
        Color[] colors,
        int width,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The run data holds one index in each byte (RLE8) or nibble (RLE4), so a table of 256 entries covers both.
        using IMemoryOwner<TPixel> palette = this.CreatePaletteTable<TPixel>(colors, 8);
        Span<TPixel> paletteSpan = palette.GetSpan();

        // A skipped pixel has no index. The first palette color option gives it the color of index 0.
        TPixel skipped = this.rleSkippedPixelHandling is RleSkippedPixelHandling.FirstColorOfPalette
            ? paletteSpan[0]
            : this.GetRleSkippedPixel<TPixel>();

        BmpRleWriter<TPixel> writer = new(pixels, inverted, skipped);
        bool rle4 = compression is BmpCompression.RLE4;

        // The reader parses the commands from a chunk of memory. See BmpRleReader. The data can end before the image
        // is complete. Then the decode stops, and the image keeps the pixels written so far (see Decode).
        BmpRleReader reader = new(stream, this.memoryAllocator);
        try
        {
            while (!writer.IsComplete)
            {
                if (!reader.TryRead(2, out ReadOnlySpan<byte> command))
                {
                    return;
                }

                int length = command[0];
                int value = command[1];

                if (length > 0)
                {
                    if (rle4)
                    {
                        // Encoded RLE4 run: even pixels take the high nibble (value >> 4), odd pixels take the low
                        // nibble (value & 0xF).
                        writer.FillPair(paletteSpan[value >> 4], paletteSpan[value & 0xF], length);
                    }
                    else
                    {
                        // Encoded RLE8 run: one color for the whole run.
                        writer.Fill(paletteSpan[value], length);
                    }

                    continue;
                }

                switch (value)
                {
                    case RleEndOfLine:
                        writer.EndOfLine();
                        break;

                    case RleEndOfBitmap:
                        writer.EndOfBitmap();
                        break;

                    case RleDelta:
                        // The delta moves dy rows down and dx pixels right. In the pixel sequence that is
                        // dy * width + dx pixels. The delta bytes are [dx, dy].
                        if (!reader.TryRead(2, out ReadOnlySpan<byte> delta))
                        {
                            return;
                        }

                        writer.Skip(((long)delta[1] * width) + delta[0]);
                        break;

                    default:
                        // Absolute run of 'value' indices. RLE4 stores two indices in each byte, high nibble first, so
                        // it stores (value + 1) / 2 bytes: the last byte has one index in its high nibble when 'value' is
                        // odd. RLE8 stores one index in each byte, so it stores 'value' bytes. Both expand from the run
                        // data to the pixels in one pass.
                        int byteCount = rle4 ? (value + 1) >> 1 : value;
                        if (!reader.TryRead(byteCount, out ReadOnlySpan<byte> run))
                        {
                            return;
                        }

                        if (rle4)
                        {
                            writer.Write4(run, value, paletteSpan);
                        }
                        else
                        {
                            writer.Write(run, paletteSpan);
                        }

                        // The run data ends on a two-byte boundary: an odd byte count has one pad byte.
                        if ((byteCount & 1) != 0)
                        {
                            reader.SkipPad();
                        }

                        break;
                }
            }

            reader.Finish();
        }
        finally
        {
            reader.Dispose();
        }
    }

    /// <summary>
    /// Decodes an RLE24 bitmap straight into the image rows.
    /// </summary>
    /// <remarks>
    /// The commands are the RLE8 commands of <see cref="ReadRle{TPixel}(BufferedReadStream, BmpCompression, Buffer2D{TPixel}, Color[], int, bool)"/>,
    /// with a B, G, R color in place of an index. An encoded run is [a, B, G, R]. An absolute run of b pixels stores
    /// 3 * b bytes, padded to a two-byte boundary.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRle24<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, int width, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // RLE24 has no palette. The first palette color option gives black, the color of zero bytes.
        TPixel skipped = this.GetRleSkippedPixel<TPixel>();
        BmpRleWriter<TPixel> writer = new(pixels, inverted, skipped);

        // The reader parses the commands from a chunk of memory. See BmpRleReader. The data can end before the image
        // is complete. Then the decode stops, and the image keeps the pixels written so far (see Decode).
        BmpRleReader reader = new(stream, this.memoryAllocator);
        try
        {
            while (!writer.IsComplete)
            {
                if (!reader.TryRead(2, out ReadOnlySpan<byte> command))
                {
                    return;
                }

                int length = command[0];
                int value = command[1];

                if (length > 0)
                {
                    // Encoded run: 'value' is the blue byte. The green byte and the red byte follow.
                    if (!reader.TryRead(2, out ReadOnlySpan<byte> greenRed))
                    {
                        return;
                    }

                    writer.Fill(TPixel.FromBgr24(new Bgr24(greenRed[1], greenRed[0], (byte)value)), length);
                    continue;
                }

                switch (value)
                {
                    case RleEndOfLine:
                        writer.EndOfLine();
                        break;

                    case RleEndOfBitmap:
                        writer.EndOfBitmap();
                        break;

                    case RleDelta:
                        // The delta moves dy rows down and dx pixels right: dy * width + dx pixels in the sequence.
                        // The delta bytes are [dx, dy].
                        if (!reader.TryRead(2, out ReadOnlySpan<byte> delta))
                        {
                            return;
                        }

                        writer.Skip(((long)delta[1] * width) + delta[0]);
                        break;

                    default:
                        int byteCount = value * 3;
                        if (!reader.TryRead(byteCount, out ReadOnlySpan<byte> run))
                        {
                            return;
                        }

                        writer.WriteBgr24(this.configuration, run);

                        // The run data ends on a two-byte boundary: an odd byte count has one pad byte.
                        if ((byteCount & 1) != 0)
                        {
                            reader.SkipPad();
                        }

                        break;
                }
            }

            reader.Finish();
        }
        finally
        {
            reader.Dispose();
        }
    }

    /// <summary>
    /// Gets the color of the pixels that RLE data skips, for the transparent and the black options.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>Transparent black for <see cref="RleSkippedPixelHandling.Transparent"/>, otherwise opaque black.</returns>
    private TPixel GetRleSkippedPixel<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
        => this.rleSkippedPixelHandling is RleSkippedPixelHandling.Transparent
            ? TPixel.FromUnassociatedScaledVector4(Vector4.Zero)

            // Default handling for skipped pixels is black (which is what System.Drawing is also doing).
            : TPixel.FromUnassociatedScaledVector4(new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

    /// <summary>
    /// Reads the color palette from the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="colors">The decoded pixel palette.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgbPalette<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        Color[] colors,
        int width,
        int height,
        int bitsPerPixel,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Pixels per byte (bits per pixel).
        int ppb = 8 / bitsPerPixel;

        int arrayWidth = (width + ppb - 1) / ppb;

        // Rows are aligned on 4 byte boundaries.
        int padding = arrayWidth % 4;
        if (padding != 0)
        {
            padding = 4 - padding;
        }

        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(arrayWidth + padding, AllocationOptions.Clean);
        Span<byte> rowSpan = row.GetSpan();

        using IMemoryOwner<TPixel> palette = this.CreatePaletteTable<TPixel>(colors, bitsPerPixel);
        Span<TPixel> paletteSpan = palette.GetSpan();

        for (int y = 0; y < height; y++)
        {
            int newY = Invert(y, height, inverted);
            if (!TryReadPixelBytes(stream, rowSpan, arrayWidth))
            {
                return;
            }

            BmpPaletteExpander.Expand<TPixel>(rowSpan, bitsPerPixel, paletteSpan, pixels.DangerousGetRowSpan(newY));
        }
    }

    /// <summary>
    /// Reads a palette bitmap that an AND mask follows, as an icon or cursor image stores it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The color rows come first, then the mask rows. The mask has 1 bit for each pixel, the first pixel in the most
    /// significant bit. A set bit makes the pixel transparent, a clear bit makes it opaque. Both row types start on
    /// four-byte boundaries.
    /// </para>
    /// <para>
    /// The decoder reads the color row and the mask row of each stored row together. It moves the stream position
    /// between the two areas, so it keeps no indices for the full image.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="colors">The decoded pixel palette.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgbPaletteWithAlphaMask<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        Color[] colors,
        int width,
        int height,
        int bitsPerPixel,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // A color row stores ceil(width * bitsPerPixel / 8) bytes. A mask row stores ceil(width / 8) bytes.
        // Each row size rounds up to a multiple of 4: (n + 3) & ~3 clears the low two bits after the add.
        int indexBytes = ((width * bitsPerPixel) + 7) / 8;
        int indexStride = (indexBytes + 3) & ~3;
        int maskBytes = (width + 7) / 8;
        int maskStride = (maskBytes + 3) & ~3;
        long indexStart = stream.Position;
        long maskStart = indexStart + ((long)indexStride * height);

        using IMemoryOwner<byte> indexRow = this.memoryAllocator.Allocate<byte>(indexStride, AllocationOptions.Clean);
        using IMemoryOwner<byte> maskRow = this.memoryAllocator.Allocate<byte>(maskBytes);
        Span<byte> indexSpan = indexRow.GetSpan();
        Span<byte> maskSpan = maskRow.GetSpan();

        // Convert each palette entry one time in two forms: opaque, and transparent. The mask bit of a pixel selects
        // the form. The alpha is replaced on unassociated color, before the pixel type applies its alpha association.
        // Each table has an entry for each of the 2^bitsPerPixel index values. The header check limits bitsPerPixel
        // to 1, 2, 4 or 8, so each table has at most 256 entries. The header check also limits the file palette to
        // 2^bitsPerPixel colors. The entries past the file palette are black, as in CreatePaletteTable.
        DebugGuard.IsTrue(bitsPerPixel is 1 or 2 or 4 or 8, "An index has 1, 2, 4 or 8 bits.");
        int entryCount = 1 << bitsPerPixel;
        using IMemoryOwner<TPixel> palettes = this.memoryAllocator.Allocate<TPixel>(entryCount * 2);
        Span<TPixel> opaque = palettes.GetSpan()[..entryCount];
        Span<TPixel> transparent = palettes.GetSpan()[entryCount..];
        for (int i = 0; i < entryCount; i++)
        {
            Vector4 color = i < colors.Length ? colors[i].ToScaledVector4(PixelAlphaRepresentation.Unassociated) : Vector4.Zero;
            color.W = 1F;
            opaque[i] = TPixel.FromUnassociatedScaledVector4(color);
            color.W = 0F;
            transparent[i] = TPixel.FromUnassociatedScaledVector4(color);
        }

        // The mask of the low bitsPerPixel bits, for example 0b1111 for 4 bits.
        int indexMask = entryCount - 1;

        for (int y = 0; y < height; y++)
        {
            stream.Position = indexStart + ((long)y * indexStride);
            if (!TryReadPixelBytes(stream, indexSpan, indexBytes))
            {
                return;
            }

            stream.Position = maskStart + ((long)y * maskStride);
            if (!TryReadPixelBytes(stream, maskSpan, maskBytes))
            {
                return;
            }

            // One pass writes each pixel with its opaque color. Most icon pixels are opaque.
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            BmpPaletteExpander.Expand<TPixel>(indexSpan, bitsPerPixel, opaque, pixelRow);

            // Then only the transparent pixels change. A mask byte of 0 has 8 opaque pixels, so the loop skips it.
            for (int maskIndex = 0; maskIndex < maskBytes; maskIndex++)
            {
                uint bits = maskSpan[maskIndex];
                while (bits != 0)
                {
                    // Pixel 0 of a mask byte is bit 7, so the highest set bit is the first transparent pixel left.
                    // In a 32-bit value, bit p has 31 - p leading zeros, so the pixel offset in the byte is
                    // 7 - p = LeadingZeroCount(bits) - 24. Clear the bit, so the next turn finds the next pixel.
                    int offset = BitOperations.LeadingZeroCount(bits) - 24;
                    bits &= ~(0x80U >> offset);

                    // The bits after the last pixel of the row are padding.
                    int x = (maskIndex * 8) + offset;
                    if (x >= width)
                    {
                        break;
                    }

                    // The index of pixel x starts at bit x * bitsPerPixel of the row, the first pixel in the most
                    // significant bits of a byte. Shift the index down to bit 0 and keep its low bitsPerPixel bits.
                    int bitPosition = x * bitsPerPixel;
                    int shift = 8 - bitsPerPixel - (bitPosition & 7);
                    int index = (indexSpan[bitPosition >> 3] >> shift) & indexMask;
                    pixelRow[x] = transparent[index];
                }
            }
        }
    }

    /// <summary>
    /// Reads the 24 bit color palette from the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgb24<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, int width, int height, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int padding = CalculatePadding(width, 3);
        using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, padding);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!TryReadPixelBytes(stream, rowSpan, width * 3))
            {
                return;
            }

            int newY = Invert(y, height, inverted);
            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);
            PixelOperations<TPixel>.Instance.FromBgr24Bytes(
                this.configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Reads the 32 bit color palette from the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgb32Fast<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, int width, int height, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int padding = CalculatePadding(width, 4);
        using IMemoryOwner<byte> row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, padding);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!TryReadPixelBytes(stream, rowSpan, width * 4))
            {
                return;
            }

            int newY = Invert(y, height, inverted);
            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);
            PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                this.configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Reads a 32-bit RGB bitmap with a version 3 header. The fourth byte of each pixel is alpha or is not used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A version 3 header has no alpha mask, so the header does not tell if the fourth byte is alpha. Many writers
    /// store 0 in the unused byte. Thus a bitmap whose fourth bytes are all 0 is opaque, and a bitmap with any other
    /// fourth byte has alpha.
    /// </para>
    /// <para>
    /// The decode starts as opaque, with the bit-field operator: B, G, R masks and no alpha mask, so each pixel gets
    /// alpha 255. It checks the fourth bytes of each row before it decodes the row. Thus an opaque bitmap decodes in
    /// one pass. At the first row with a fourth byte that is not 0, the bitmap has alpha: the stream returns to the
    /// first row, and the whole bitmap decodes again as B, G, R, A bytes with the bulk conversion. The rows before
    /// that row are the only rows that decode two times.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgb32Slow<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, int width, int height, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        long start = stream.Position;
        BmpBitFields opaque = CreateBitFields<TPixel>(0x00FF0000, 0x0000FF00, 0x000000FF, 0);

        if (!this.ReadBitFields<TPixel, BmpBitFields.Sample32Operator>(stream, pixels, width, height, inverted, opaque, true))
        {
            stream.Position = start;
            this.ReadRgb32Fast(stream, pixels, width, height, inverted);
        }
    }

    /// <summary>
    /// Gets a value indicating whether a row of B, G, R, A pixels has an alpha byte that is not 0.
    /// </summary>
    /// <remarks>
    /// Each pixel is one little-endian uint, with alpha in bits 24..31. The SIMD loops OR the pixels of the row into
    /// one register, then test the alpha bits of all lanes one time. The narrower widths and the scalar loop take the
    /// rest of the row. All forms give the same result as a test of each alpha byte.
    /// </remarks>
    /// <param name="row">The pixel bytes of the row, 4 for each pixel.</param>
    /// <returns><see langword="true"/> if an alpha byte is not 0. Else <see langword="false"/>.</returns>
    private static bool HasNonZeroAlpha(ReadOnlySpan<byte> row)
    {
        const uint alphaMask = 0xFF000000;
        ReadOnlySpan<uint> pixels = MemoryMarshal.Cast<byte, uint>(row);
        ref uint pixelBase = ref MemoryMarshal.GetReference(pixels);
        nuint length = (uint)pixels.Length;
        nuint x = 0;

        if (Vector512.IsHardwareAccelerated && length >= (uint)Vector512<uint>.Count)
        {
            Vector512<uint> any = Vector512<uint>.Zero;
            for (; x + (uint)Vector512<uint>.Count <= length; x += (uint)Vector512<uint>.Count)
            {
                any |= Vector512.LoadUnsafe(ref pixelBase, x);
            }

            if ((any & Vector512.Create(alphaMask)) != Vector512<uint>.Zero)
            {
                return true;
            }
        }

        if (Vector256.IsHardwareAccelerated && length - x >= (uint)Vector256<uint>.Count)
        {
            Vector256<uint> any = Vector256<uint>.Zero;
            for (; x + (uint)Vector256<uint>.Count <= length; x += (uint)Vector256<uint>.Count)
            {
                any |= Vector256.LoadUnsafe(ref pixelBase, x);
            }

            if ((any & Vector256.Create(alphaMask)) != Vector256<uint>.Zero)
            {
                return true;
            }
        }

        if (Vector128.IsHardwareAccelerated && length - x >= (uint)Vector128<uint>.Count)
        {
            Vector128<uint> any = Vector128<uint>.Zero;
            for (; x + (uint)Vector128<uint>.Count <= length; x += (uint)Vector128<uint>.Count)
            {
                any |= Vector128.LoadUnsafe(ref pixelBase, x);
            }

            if ((any & Vector128.Create(alphaMask)) != Vector128<uint>.Zero)
            {
                return true;
            }
        }

        uint rest = 0;
        for (; x < length; x++)
        {
            rest |= Unsafe.Add(ref pixelBase, x);
        }

        return (rest & alphaMask) != 0;
    }

    /// <summary>
    /// Reads one stored row of pixel data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stored row has its pixel bytes, then padding to a four-byte boundary. Some writers leave out the padding of
    /// the final row. Thus the read requires only the pixel bytes. It fills the rest of the buffer when the stream has
    /// those bytes.
    /// </para>
    /// <para>
    /// Data that ends before the image is complete is not an error. The caller stops and keeps the rows decoded
    /// before the end, as the PNG and JPEG decoders do. The image buffer is cleared at allocation, so the rows after
    /// the end are transparent black.
    /// </para>
    /// </remarks>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="buffer">The buffer, with room for the required bytes and any padding.</param>
    /// <param name="requiredBytes">The byte count of the pixels in the row.</param>
    /// <returns><see langword="true"/> if the stream has the pixel bytes of the row. Else <see langword="false"/>.</returns>
    private static bool TryReadPixelBytes(BufferedReadStream stream, Span<byte> buffer, int requiredBytes)
        => stream.ReadAtLeast(buffer, requiredBytes, false) >= requiredBytes;

    /// <summary>
    /// Reads the <see cref="BmpInfoHeader"/> from the stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    [MemberNotNull(nameof(metadata))]
    [MemberNotNull(nameof(bmpMetadata))]
    private void ReadInfoHeader(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[BmpInfoHeader.MaxHeaderSize];
        long infoHeaderStart = stream.Position;

        // Resolution is stored in PPM.
        this.metadata = new ImageMetadata
        {
            ResolutionUnits = PixelResolutionUnit.PixelsPerMeter
        };

        // Read the header size.
        stream.Read(buffer, 0, BmpInfoHeader.HeaderSizeSize);

        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        if (headerSize is < BmpInfoHeader.CoreSize or > BmpInfoHeader.MaxHeaderSize)
        {
            BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. HeaderSize is '{headerSize}'.");
        }

        // Read the rest of the header.
        stream.Read(buffer, BmpInfoHeader.HeaderSizeSize, headerSize - BmpInfoHeader.HeaderSizeSize);

        BmpInfoHeaderType infoHeaderType = BmpInfoHeaderType.WinVersion2;
        if (headerSize == BmpInfoHeader.CoreSize)
        {
            // 12 bytes
            infoHeaderType = BmpInfoHeaderType.WinVersion2;
            this.infoHeader = BmpInfoHeader.ParseCore(buffer);
        }
        else if (headerSize == BmpInfoHeader.Os22ShortSize)
        {
            // 16 bytes
            infoHeaderType = BmpInfoHeaderType.Os2Version2Short;
            this.infoHeader = BmpInfoHeader.ParseOs22Short(buffer);
        }
        else if (headerSize == BmpInfoHeader.SizeV3)
        {
            // == 40 bytes
            infoHeaderType = BmpInfoHeaderType.WinVersion3;
            this.infoHeader = BmpInfoHeader.ParseV3(buffer);

            // If the info header is BMP version 3 and the compression type is BITFIELDS,
            // color masks for each color channel follow the info header.
            if (this.infoHeader.Compression == BmpCompression.BitFields)
            {
                Span<byte> bitfieldsBuffer = stackalloc byte[12];
                stream.Read(bitfieldsBuffer);
                Span<byte> data = bitfieldsBuffer;
                this.infoHeader.RedMask = BinaryPrimitives.ReadInt32LittleEndian(data[..4]);
                this.infoHeader.GreenMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
                this.infoHeader.BlueMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
            }
            else if (this.infoHeader.Compression == BmpCompression.BI_ALPHABITFIELDS)
            {
                Span<byte> bitfieldsBuffer = stackalloc byte[16];
                stream.Read(bitfieldsBuffer);
                Span<byte> data = bitfieldsBuffer;
                this.infoHeader.RedMask = BinaryPrimitives.ReadInt32LittleEndian(data[..4]);
                this.infoHeader.GreenMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
                this.infoHeader.BlueMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
                this.infoHeader.AlphaMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
            }
        }
        else if (headerSize == BmpInfoHeader.AdobeV3Size)
        {
            // == 52 bytes
            infoHeaderType = BmpInfoHeaderType.AdobeVersion3;
            this.infoHeader = BmpInfoHeader.ParseAdobeV3(buffer, withAlpha: false);
        }
        else if (headerSize == BmpInfoHeader.AdobeV3WithAlphaSize)
        {
            // == 56 bytes
            infoHeaderType = BmpInfoHeaderType.AdobeVersion3WithAlpha;
            this.infoHeader = BmpInfoHeader.ParseAdobeV3(buffer, withAlpha: true);
        }
        else if (headerSize == BmpInfoHeader.Os2v2Size)
        {
            // == 64 bytes
            infoHeaderType = BmpInfoHeaderType.Os2Version2;
            this.infoHeader = BmpInfoHeader.ParseOs2Version2(buffer);
        }
        else if (headerSize == BmpInfoHeader.SizeV4)
        {
            // == 108 bytes
            infoHeaderType = BmpInfoHeaderType.WinVersion4;
            this.infoHeader = BmpInfoHeader.ParseV4(buffer);
        }
        else if (headerSize > BmpInfoHeader.SizeV4)
        {
            // > 108 bytes
            infoHeaderType = BmpInfoHeaderType.WinVersion5;
            this.infoHeader = BmpInfoHeader.ParseV5(buffer);
            if (this.infoHeader.ProfileData != 0 && this.infoHeader.ProfileSize != 0)
            {
                long streamPosition = stream.Position;
                this.ExecuteAncillarySegmentAction(() => this.ReadIccProfile(stream, this.metadata, infoHeaderStart));
                stream.Position = streamPosition;
            }
        }
        else
        {
            BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. HeaderSize '{headerSize}'.");
        }

        if (this.infoHeader.XPelsPerMeter > 0 && this.infoHeader.YPelsPerMeter > 0)
        {
            this.metadata.HorizontalResolution = this.infoHeader.XPelsPerMeter;
            this.metadata.VerticalResolution = this.infoHeader.YPelsPerMeter;
        }
        else
        {
            // Convert default metadata values to PPM.
            this.metadata.HorizontalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetadata.DefaultHorizontalResolution));
            this.metadata.VerticalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetadata.DefaultVerticalResolution));
        }

        if (this.isDoubleHeight)
        {
            this.infoHeader.Height >>= 1;
        }

        ushort bitsPerPixel = this.infoHeader.BitsPerPixel;
        this.bmpMetadata = this.metadata.GetBmpMetadata();
        this.bmpMetadata.InfoHeaderType = infoHeaderType;
        this.bmpMetadata.BitsPerPixel = (BmpBitsPerPixel)bitsPerPixel;

        this.Dimensions = new Size(this.infoHeader.Width, this.infoHeader.Height);
    }

    /// <summary>
    /// Reads the embedded ICC profile from the BMP V5 info header.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="imageMetadata">The image metadata.</param>
    /// <param name="infoHeaderStart">The stream position where the info header begins.</param>
    private void ReadIccProfile(BufferedReadStream stream, ImageMetadata imageMetadata, long infoHeaderStart)
    {
        long profileStart = infoHeaderStart + this.infoHeader.ProfileData;
        if (this.infoHeader.ProfileData < 0 ||
            this.infoHeader.ProfileSize <= 0 ||
            !stream.IsReadRangeValid(profileStart, (uint)this.infoHeader.ProfileSize))
        {
            BmpThrowHelper.ThrowInvalidImageContentException("Not enough data to read BMP ICC profile.");
        }

        stream.Position = profileStart;
        IccReader reader = new();
        if (reader.Read(stream, this.infoHeader.ProfileSize) != this.infoHeader.ProfileSize)
        {
            BmpThrowHelper.ThrowInvalidImageContentException("Not enough data to read BMP ICC profile.");
        }

        imageMetadata.IccProfile = reader.ToProfile();
    }

    /// <summary>
    /// Reads the <see cref="BmpFileHeader"/> from the stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadFileHeader(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[BmpFileHeader.Size];
        stream.Read(buffer, 0, BmpFileHeader.Size);

        short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(buffer);
        switch (fileTypeMarker)
        {
            case BmpConstants.TypeMarkers.Bitmap:
                this.fileHeader = BmpFileHeader.Parse(buffer);
                break;
            case BmpConstants.TypeMarkers.BitmapArray:
                // Because we only decode the first bitmap in the array, the array header will be ignored.
                // The bitmap file header of the first image follows the array header.
                stream.Read(buffer, 0, BmpFileHeader.Size);
                this.fileHeader = BmpFileHeader.Parse(buffer);
                if (this.fileHeader.Value.Type != BmpConstants.TypeMarkers.Bitmap)
                {
                    BmpThrowHelper.ThrowNotSupportedException(
                        $"Unsupported bitmap file inside a BitmapArray file. File header bitmap type marker '{this.fileHeader.Value.Type}'.");
                }

                break;

            default:
                BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. File header bitmap type marker '{fileTypeMarker}'.");
                break;
        }
    }

    /// <summary>
    /// Reads the <see cref="BmpFileHeader"/> and <see cref="BmpInfoHeader"/> from the stream and sets the corresponding fields.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="inverted">Whether the image orientation is inverted.</param>
    /// <param name="isIndexed">Whether the image uses its palette for pixel indices.</param>
    /// <param name="palette">The color palette.</param>
    [MemberNotNull(nameof(metadata))]
    [MemberNotNull(nameof(bmpMetadata))]
    private void ReadImageHeaders(BufferedReadStream stream, out bool inverted, out bool isIndexed, out Color[] palette)
    {
        if (!this.skipFileHeader)
        {
            this.ReadFileHeader(stream);
        }

        this.ReadInfoHeader(stream);

        // Direct-color BMPs may carry a table but convert their pixel data after decode instead.
        isIndexed = this.infoHeader.Compression switch
        {
            BmpCompression.RLE4 or BmpCompression.RLE8 => true,
            BmpCompression.RGB => this.infoHeader.BitsPerPixel is > 0 and <= 8,
            _ => false
        };

        // see http://www.drdobbs.com/architecture-and-design/the-bmp-file-format-part-1/184409517
        // If the height is negative, then this is a Windows bitmap whose origin
        // is the upper-left corner and not the lower-left. The inverted flag
        // indicates a lower-left origin.Our code will be outputting an
        // upper-left origin pixel array.
        inverted = false;
        if (this.infoHeader.Height < 0)
        {
            inverted = true;
            this.infoHeader.Height = -this.infoHeader.Height;
        }

        if (this.infoHeader.Width <= 0 || this.infoHeader.Height <= 0)
        {
            BmpThrowHelper.ThrowInvalidImageContentException("Width and height must be greater than 0.");
        }

        // Uncompressed pixel data is only defined for these depths. Rejecting any other depth here, rather than in
        // the pixel readers, also keeps identification from reporting a header that cannot be decoded.
        if (this.infoHeader.Compression is BmpCompression.RGB && this.infoHeader.BitsPerPixel is not (1 or 2 or 4 or 8 or 16 or 24 or 32))
        {
            BmpThrowHelper.ThrowInvalidImageContentException($"Invalid bits per pixel: {this.infoHeader.BitsPerPixel}");
        }

        // Reject pixel offsets beyond the input before allocating or processing the color table.
        if (this.fileHeader.HasValue && this.fileHeader.Value.Offset > stream.Length)
        {
            BmpThrowHelper.ThrowInvalidImageContentException(
                $"Pixel data offset {this.fileHeader.Value.Offset} exceeds file size {stream.Length}.");
        }

        // Core headers store RGBTRIPLE entries; later headers store RGBQUAD or RGB2 entries.
        int bytesPerColorMapEntry = this.infoHeader.HeaderSize == BmpInfoHeader.CoreSize
            ? Unsafe.SizeOf<Bgr24>()
            : Unsafe.SizeOf<Bgra32>();

        // ClrUsed is an unsigned DWORD on disk, so retain its full range while validating the palette count.
        uint colorCount = (uint)this.infoHeader.ClrUsed;

        // Indexed palettes cannot exceed the number of addressable colors; zero selects the full table.
        if (this.infoHeader.BitsPerPixel is 1 or 2 or 4 or 8)
        {
            uint maxColors = (uint)ColorNumerics.GetColorCountForBitDepth(this.infoHeader.BitsPerPixel);

            if (colorCount > maxColors)
            {
                BmpThrowHelper.ThrowInvalidImageContentException(
                    $"The color table contains {colorCount} entries, exceeding the maximum of {maxColors} for a {this.infoHeader.BitsPerPixel}-bit image.");
            }

            if (colorCount == 0)
            {
                colorCount = maxColors;
            }
        }

        // Widen before multiplying so a malicious count cannot wrap to a small read followed by a huge palette allocation.
        ulong colorMapByteCount = (ulong)colorCount * (uint)bytesPerColorMapEntry;

        // File-backed palettes must fit before pixel data; headerless embedded DIB palettes use the stream bound below.
        if (this.fileHeader.HasValue && colorMapByteCount > 0)
        {
            long bytesBeforePixels = (long)this.fileHeader.Value.Offset - stream.Position;
            if (bytesBeforePixels < 0 || (ulong)bytesBeforePixels < colorMapByteCount)
            {
                BmpThrowHelper.ThrowInvalidImageContentException(
                    "Reading the color map would read beyond the bitmap offset. " +
                    $"Either the color map size of '{colorMapByteCount}' is invalid or the bitmap offset.");
            }
        }

        // The complete table must be present and fit an int-sized buffer before allocation or narrowing.
        if (!stream.TryGetReadLength(colorMapByteCount, out int colorMapSizeBytes))
        {
            BmpThrowHelper.ThrowInvalidImageContentException(
                $"The color table requires {colorMapByteCount} bytes, but the input does not contain that palette data.");
        }

        palette = [];

        if (colorMapSizeBytes > 0)
        {
            // Share the decoded table with metadata and pixel lookup so both use identical colors.
            // The byte count is validated before narrowing the declared count for allocation.
            palette = new Color[(int)colorCount];
            using IMemoryOwner<byte> paletteBuffer = this.memoryAllocator.Allocate<byte>(colorMapSizeBytes);
            Span<byte> paletteBytes = paletteBuffer.GetSpan();

            if (stream.Read(paletteBytes) != colorMapSizeBytes)
            {
                BmpThrowHelper.ThrowInvalidImageContentException("Could not read enough data for the palette!");
            }

            // Bulk color conversion requires contiguous BGR pixels. Use the start of the read buffer
            // as the packed destination, with exactly three bytes per palette entry, avoiding a second buffer.
            // Three-byte entries already have this layout; four-byte entries are compacted below.
            Span<Bgr24> source = MemoryMarshal.Cast<byte, Bgr24>(paletteBytes[..(palette.Length * Unsafe.SizeOf<Bgr24>())]);

            if (bytesPerColorMapEntry == Unsafe.SizeOf<Bgra32>())
            {
                // BMP palette entries contain B, G, R and a reserved byte; discard the fourth byte in place.
                SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(paletteBytes[..(palette.Length * Unsafe.SizeOf<Bgra32>())], MemoryMarshal.AsBytes(source));
            }

            Color.FromPixel((ReadOnlySpan<Bgr24>)source, palette);

            this.bmpMetadata.ColorTable = palette;
        }

        int skipAmount = 0;
        if (this.fileHeader.HasValue)
        {
            skipAmount = this.fileHeader.Value.Offset - (int)stream.Position;
        }

        if ((skipAmount + (int)stream.Position) > stream.Length)
        {
            BmpThrowHelper.ThrowInvalidImageContentException("Invalid file header offset found. Offset is greater than the stream length.");
        }

        if (skipAmount > 0)
        {
            stream.Skip(skipAmount);
        }
    }
}
