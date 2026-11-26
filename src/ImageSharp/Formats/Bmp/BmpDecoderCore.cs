// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Performs the bitmap decoding operation.
/// </summary>
internal sealed class BmpDecoderCore : ImageDecoderCore
{
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
    /// The message for uncompressed pixel data that ends before the image is complete.
    /// </summary>
    private const string PixelDataEndMessage = "The BMP pixel data ends before the image is complete.";

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

    /// <summary>
    /// Makes one pixel of a row transparent. <see cref="ApplyMask{TOperator}"/> calls it for each set bit of the AND mask.
    /// </summary>
    private interface IMaskOperator
    {
        /// <summary>
        /// Makes a pixel transparent.
        /// </summary>
        /// <param name="x">The pixel index in the row.</param>
        void SetTransparent(int x);
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        Image<TPixel>? image = null;
        try
        {
            this.ReadImageHeaders(stream, out bool inverted, out bool isIndexed, out Color[] palette);

            if (isIndexed)
            {
                this.ConvertColorPalette<TPixel>(palette, this.metadata.IccProfile);
            }

            image = new Image<TPixel>(this.configuration, this.infoHeader.Width, this.infoHeader.Height, this.metadata);

            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            // Pixel data that is damaged, or that ends before the image is complete, throws in the Strict and
            // IgnoreAncillary modes. In the IgnoreImageData mode, the image keeps the pixels decoded before the damage.
            // This is the same for each compression. The image buffer is cleared at allocation, so the other pixels are
            // transparent black. The allocator limits protect against large dimensions.
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
        long pixelDataStart = stream.Position;

        switch (this.infoHeader.Compression)
        {
            // An icon bitmap can keep alpha in the fourth byte of a 32-bit BI_RGB pixel. See ReadRgb32Slow and the alpha rule in ReadInfoHeader.
            case BmpInfoCompression.Rgb when bitsPerPixel is 32 && this.processedAlphaMask:
                this.ReadRgb32Slow(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);

                break;

            case BmpInfoCompression.Rgb when bitsPerPixel is 24:
                this.ReadRgb24(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                if (this.processedAlphaMask)
                {
                    this.ReadAlphaMask(stream, pixels, pixelDataStart, bitsPerPixel, inverted);
                }

                break;

            case BmpInfoCompression.Rgb when bitsPerPixel <= 8 && this.processedAlphaMask:
                this.ReadRgbPaletteWithAlphaMask(
                    stream,
                    pixels,
                    palette,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    this.infoHeader.BitsPerPixel,
                    inverted);

                break;

            case BmpInfoCompression.Rgb when bitsPerPixel <= 8:
                this.ReadRgbPalette(
                    stream,
                    pixels,
                    palette,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    this.infoHeader.BitsPerPixel,
                    inverted);

                break;

            case BmpInfoCompression.Rle24:
                // RLE24 has no palette. Thus the FirstColorOfPalette option gives black, the color of a pixel with only zero bytes.
                this.ReadRle(stream, pixels, inverted, this.GetRleSkippedPixel<TPixel>(), new BmpRleWriter<TPixel>.Rle24Operator(this.configuration));

                break;

            case BmpInfoCompression.Rle8:
            case BmpInfoCompression.Rle4:
                this.ReadRle(stream, this.infoHeader.Compression, pixels, palette, inverted);

                break;

            case BmpInfoCompression.Huffman1D:
                this.ReadHuffman1D(stream, pixels, palette, this.infoHeader.Width, this.infoHeader.Height, inverted);

                break;

            // The header check in ReadImageHeaders allows no other compression. BI_RGB has fixed masks at 16 and 32 bits (see ReadInfoHeader). Thus the decoder
            // reads it as bit fields.
            case BmpInfoCompression.Rgb:
            case BmpInfoCompression.BitFields:
            case BmpInfoCompression.AlphaBitFields:
                this.ReadBitFields(stream, pixels, inverted);

                // A 16-bit icon bitmap stores an AND mask after the color rows. A 32-bit icon pixel keeps alpha.
                if (this.processedAlphaMask && bitsPerPixel is 16)
                {
                    this.ReadAlphaMask(stream, pixels, pixelDataStart, bitsPerPixel, inverted);
                }

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

        ColorProfileConverter? converter = ColorProfileConverter.CreateIccForDecoding(options);
        converter?.Convert<TPixel>(palette);
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
    /// Decodes a bitmap with a bit mask for each channel: a BI_BITFIELDS bitmap, or a 16-bit or 32-bit BI_RGB bitmap.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <param name="pixels">The output pixel buffer that holds the decoded image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadBitFields<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The header check in ReadImageHeaders allows only 16 and 32 bits.
        int bitsPerPixel = this.infoHeader.BitsPerPixel;
        uint redMask = this.infoHeader.RedMask;
        uint greenMask = this.infoHeader.GreenMask;
        uint blueMask = this.infoHeader.BlueMask;
        uint alphaMask = this.infoHeader.AlphaMask;

        // ReadInfoHeader finds the 16-bit layouts from the masks, and sets the color type from them. These layouts are the layouts of Bgr565 and Bgra5551, so
        // the bulk conversions read the row with no unpack. The 5-5-5 layout with no alpha is Bgra5551 with an opaque bit 15.
        switch (this.bmpMetadata!.ColorType)
        {
            case BmpColorType.Bgr565:
                this.ReadRgb565(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                return;

            case BmpColorType.Bgra5551:
                this.ReadRgb555(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted, true);
                return;

            case BmpColorType.Bgr when this.bmpMetadata.BitsPerPixel is BmpBitsPerPixel.Bit16:
                this.ReadRgb555(stream, pixels, this.infoHeader.Width, this.infoHeader.Height, inverted, false);
                return;
        }

        // These masks store each pixel as the bytes B, G, R, A. That is the layout of a 32-bit RGB bitmap with alpha, so the bulk BGRA conversion reads the row
        // with no unpack.
        if (bitsPerPixel is 32
            && redMask == BmpConstants.Bgra32Masks.Red
            && greenMask == BmpConstants.Bgra32Masks.Green
            && blueMask == BmpConstants.Bgra32Masks.Blue
            && alphaMask == BmpConstants.Bgra32Masks.Alpha)
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
        BmpBitFields fields = new(redMask, greenMask, blueMask, alphaMask);

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
    /// Decodes a bit-field bitmap whose channels each have 8 bits or fewer.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TSample">The stored sample width.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="fields">The channel masks.</param>
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
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, TSample.BytesPerSample * 8));
        Span<byte> rowSpan = row.GetSpan();

        // The fields write an Rgba32 row. The bulk conversion then writes the image row.
        using IMemoryOwner<Rgba32> rgbaRow = this.memoryAllocator.Allocate<Rgba32>(width);
        Span<Rgba32> rgbaSpan = rgbaRow.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, pixelBytes))
            {
                return true;
            }

            if (stopOnAlpha && HasNonZeroAlpha(rowSpan[..pixelBytes]))
            {
                return false;
            }

            fields.Unpack<TSample>(rowSpan, MemoryMarshal.Cast<Rgba32, uint>(rgbaSpan));
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            PixelOperations<TPixel>.Instance.FromRgba32(this.configuration, rgbaSpan, pixelRow);
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
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, TSample.BytesPerSample * 8));
        using IMemoryOwner<Vector4> vectorRow = this.memoryAllocator.Allocate<Vector4>(width);
        Span<byte> rowSpan = row.GetSpan();
        Span<Vector4> vectorSpan = vectorRow.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, pixelBytes))
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
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="compression">The compression type. Either RLE4 or RLE8.</param>
    /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
    /// <param name="colors">The decoded pixel palette.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRle<TPixel>(BufferedReadStream stream, BmpInfoCompression compression, Buffer2D<TPixel> pixels, Color[] colors, bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The run data holds one index in each byte (RLE8) or nibble (RLE4), so a table of 256 entries covers both.
        using IMemoryOwner<TPixel> palette = this.CreatePaletteTable<TPixel>(colors, 8);
        Span<TPixel> paletteSpan = palette.GetSpan();

        // A skipped pixel has no index. The first palette color option gives it the color of index 0.
        TPixel skipped = this.rleSkippedPixelHandling is RleSkippedPixelHandling.FirstColorOfPalette
            ? paletteSpan[0]
            : this.GetRleSkippedPixel<TPixel>();

        if (compression is BmpInfoCompression.Rle4)
        {
            this.ReadRle(stream, pixels, inverted, skipped, new BmpRleWriter<TPixel>.Rle4Operator(paletteSpan));
        }
        else
        {
            this.ReadRle(stream, pixels, inverted, skipped, new BmpRleWriter<TPixel>.Rle8Operator(paletteSpan));
        }
    }

    /// <summary>
    /// Decodes an RLE4, RLE8 or RLE24 bitmap straight into the image rows.
    /// </summary>
    /// <remarks>
    /// <para>The data is a sequence of commands. Each command starts with two bytes, [a, b]:</para>
    /// <list type="bullet">
    /// <item><description>a &gt; 0: an encoded run of a pixels. b starts the color of the run. See the operator.</description></item>
    /// <item><description>a = 0, b = 0: end of line. The decoder skips the rest of the row.</description></item>
    /// <item><description>a = 0, b = 1: end of bitmap. The decoder skips the rest of the image.</description></item>
    /// <item><description>a = 0, b = 2: delta. Two bytes dx and dy follow. The data skips dy rows and dx pixels.</description></item>
    /// <item><description>a = 0, b &gt; 2: an absolute run of b pixels. The operator gives the byte count of the run. Padding extends the run data to a
    /// two-byte boundary.</description></item>
    /// </list>
    /// <para>
    /// The reader reports data that ends before the image is complete. In the IgnoreImageData mode, the method then returns, and the image keeps the pixels
    /// that the method wrote before the end.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <typeparam name="TRun">The run operator of the compression.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <param name="pixels">The image pixels.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="skipped">The color of the pixels that the data skips.</param>
    /// <param name="run">The run operator.</param>
    private void ReadRle<TPixel, TRun>(BufferedReadStream stream, Buffer2D<TPixel> pixels, bool inverted, TPixel skipped, TRun run)
        where TPixel : unmanaged, IPixel<TPixel>
        where TRun : struct, BmpRleWriter<TPixel>.IRunOperator, allows ref struct
    {
        int width = pixels.Width;
        BmpRleWriter<TPixel> writer = new(pixels, inverted, skipped);

        // The reader parses the commands from a chunk of memory. See BmpRleReader.
        BmpRleReader reader = new(stream, this.memoryAllocator, this.Options.SegmentIntegrityHandling);
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
                    if (!run.WriteEncoded(ref reader, ref writer, length, value))
                    {
                        return;
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
                        int byteCount = run.GetAbsoluteByteCount(value);
                        if (!reader.TryRead(byteCount, out ReadOnlySpan<byte> data))
                        {
                            return;
                        }

                        run.WriteAbsolute(ref writer, data, value);

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
    /// Decodes an OS/2 Huffman 1D bitmap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data uses the one-dimensional coding scheme of ITU-T T.4. An EOL code comes before the first row and after each row, and six EOL codes end the data.
    /// The rows have no padding. See ITU-T Recommendation T.4 (07/2003), section 4.1, pages 3-4: https://www.itu.int/rec/T-REC-T.4
    /// </para>
    /// <para>
    /// A white run is 0 bits, so it selects palette index 0. The OS/2 references do not give the order of the code bits in a byte. The bmpsuite reference file
    /// pal1huffmsb.bmp stores the first code bit in the most significant bit, as libtiff writes CCITT data: https://github.com/jsummers/bmpsuite
    /// </para>
    /// <para>
    /// A code that is not valid, or a run that is longer than the row, ends its row. The decode continues after the next EOL code. The row decoder reports such
    /// damage, and data that ends before the last row. It throws in the Strict and IgnoreAncillary modes. In the IgnoreImageData mode, the image keeps the
    /// decoded rows.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <param name="pixels">The output pixel buffer that holds the decoded image.</param>
    /// <param name="colors">The decoded pixel palette.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadHuffman1D<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        Color[] colors,
        int width,
        int height,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The compressed data runs to the end of the stream. With no data, the first row decode returns false.
        long remaining = stream.Length - stream.Position;
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>((width + 7) / 8);
        using IMemoryOwner<TPixel> palette = this.CreatePaletteTable<TPixel>(colors, 1);
        Span<byte> rowSpan = row.GetSpan();
        Span<TPixel> paletteSpan = palette.GetSpan();
        T4RowDecoder decoder = new(
            stream,
            CcittBitOrder.MostSignificantBitFirst,
            (int)Math.Min(remaining, int.MaxValue),
            width,
            this.Options.SegmentIntegrityHandling);

        for (int y = 0; y < height; y++)
        {
            // The row decoder writes only the 1 bits, so each row starts with all bits 0.
            rowSpan.Clear();
            if (!decoder.DecodeRow(rowSpan))
            {
                break;
            }

            PaletteExpander.Expand<TPixel>(rowSpan, 1, paletteSpan, pixels.DangerousGetRowSpan(Invert(y, height, inverted)));
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
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, bitsPerPixel), AllocationOptions.Clean);
        Span<byte> rowSpan = row.GetSpan();

        using IMemoryOwner<TPixel> palette = this.CreatePaletteTable<TPixel>(colors, bitsPerPixel);
        Span<TPixel> paletteSpan = palette.GetSpan();

        for (int y = 0; y < height; y++)
        {
            int newY = Invert(y, height, inverted);
            if (!this.TryReadPixelBytes(stream, rowSpan, arrayWidth))
            {
                return;
            }

            PaletteExpander.Expand<TPixel>(rowSpan, bitsPerPixel, paletteSpan, pixels.DangerousGetRowSpan(newY));
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
        // A color row stores ceil(width * bitsPerPixel / 8) bytes. A mask row stores ceil(width / 8) bytes. Each row starts on a four-byte boundary.
        int indexBytes = ((width * bitsPerPixel) + 7) / 8;
        int indexStride = BmpConstants.GetStride(width, bitsPerPixel);
        int maskBytes = (width + 7) / 8;
        int maskStride = BmpConstants.GetStride(width, 1);
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

        // The color rows come before all mask rows, so data that ends early can hold color rows without their mask
        // rows. Missing mask bits are 0, as other missing image data is. A clear bit is opaque, so those rows keep
        // their decoded colors.
        bool maskEnded = false;
        for (int y = 0; y < height; y++)
        {
            // Some streams do not allow a seek past their end, for example the stream of an icon entry. Thus the
            // decoder finds the end of the data before each seek.
            long indexPosition = indexStart + ((long)y * indexStride);
            if (indexPosition > stream.Length)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(PixelDataEndMessage);
                return;
            }

            stream.Position = indexPosition;
            if (!this.TryReadPixelBytes(stream, indexSpan, indexBytes))
            {
                return;
            }

            if (maskEnded)
            {
                maskSpan.Clear();
            }
            else
            {
                int maskBytesRead = ReadMaskRow(stream, maskStart + ((long)y * maskStride), maskSpan);
                if (maskBytesRead < maskBytes)
                {
                    // The mask bytes that are read stay. The bytes after them are 0.
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(PixelDataEndMessage);
                    maskSpan[maskBytesRead..].Clear();
                    maskEnded = true;
                }
            }

            // One pass writes each pixel with its opaque color. Most icon pixels are opaque.
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            PaletteExpander.Expand<TPixel>(indexSpan, bitsPerPixel, opaque, pixelRow);

            // Then only the transparent pixels change.
            ApplyMask(maskSpan, width, new PaletteMaskOperator<TPixel>(pixelRow, indexSpan, transparent, bitsPerPixel, indexMask));
        }
    }

    /// <summary>
    /// Reads the AND mask that follows the color rows of a 16-bit or 24-bit icon or cursor bitmap. Each pixel with a set
    /// mask bit becomes transparent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An icon image stores the color rows (icXOR), then the AND mask rows (icAND) for each color depth. The mask has the
    /// dimensions of the color rows and 1 bit for each pixel. See "Icons", section "The ICO File":
    /// https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// </para>
    /// <para>
    /// A 32-bit icon pixel keeps alpha in its fourth byte, so the 32-bit path does not read the mask.
    /// <see cref="ReadRgbPaletteWithAlphaMask{TPixel}"/> reads the mask of a palette bitmap with its color rows.
    /// </para>
    /// <para>
    /// Data that ends before the mask is complete is damaged. The missing mask bits are 0, as in the palette path. A
    /// clear bit is opaque, so those pixels keep their decoded colors.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The decoded color pixels. The method changes the masked pixels.</param>
    /// <param name="pixelDataStart">The stream position of the first color row.</param>
    /// <param name="bitsPerPixel">The bit count of a color pixel: 16 or 24.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadAlphaMask<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        long pixelDataStart,
        int bitsPerPixel,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;

        // A color row and a mask row each start on a four-byte boundary. The mask rows start after all color rows.
        int maskBytes = (width + 7) / 8;
        int maskStride = BmpConstants.GetStride(width, 1);
        long maskStart = pixelDataStart + ((long)BmpConstants.GetStride(width, bitsPerPixel) * height);

        using IMemoryOwner<byte> maskRow = this.memoryAllocator.Allocate<byte>(maskBytes);
        Span<byte> maskSpan = maskRow.GetSpan();

        for (int y = 0; y < height; y++)
        {
            int maskBytesRead = ReadMaskRow(stream, maskStart + ((long)y * maskStride), maskSpan);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            ApplyMask(maskSpan[..maskBytesRead], width, new AlphaMaskOperator<TPixel>(pixelRow));

            if (maskBytesRead < maskBytes)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(PixelDataEndMessage);
                return;
            }
        }
    }

    /// <summary>
    /// Reads one row of the AND mask of an icon bitmap.
    /// </summary>
    /// <remarks>
    /// Some streams do not allow a seek past their end, for example the stream of an icon entry. Thus the method finds the end of the data before the seek.
    /// </remarks>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <param name="position">The stream position of the mask row.</param>
    /// <param name="mask">The destination. Its length is the byte count of the mask bits of a row.</param>
    /// <returns>The byte count that the stream has. It is less than the length of <paramref name="mask"/> when the data ends.</returns>
    private static int ReadMaskRow(BufferedReadStream stream, long position, Span<byte> mask)
    {
        if (position > stream.Length)
        {
            return 0;
        }

        stream.Position = position;
        return stream.ReadAtLeast(mask, mask.Length, false);
    }

    /// <summary>
    /// Makes transparent each pixel of a row that has a set bit in the AND mask.
    /// </summary>
    /// <remarks>
    /// The mask has 1 bit for each pixel, the first pixel in the most significant bit. A mask byte of 0 has 8 opaque pixels, so the loop skips it in one step.
    /// Most icon pixels are opaque.
    /// </remarks>
    /// <typeparam name="TOperator">The operator that makes a pixel transparent.</typeparam>
    /// <param name="mask">The mask bytes of the row.</param>
    /// <param name="width">The pixel count of the row.</param>
    /// <param name="op">The operator.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMask<TOperator>(ReadOnlySpan<byte> mask, int width, TOperator op)
        where TOperator : struct, IMaskOperator, allows ref struct
    {
        for (int maskIndex = 0; maskIndex < mask.Length; maskIndex++)
        {
            uint bits = mask[maskIndex];
            while (bits != 0)
            {
                // Pixel 0 of a mask byte is bit 7, so the highest set bit is the first transparent pixel that is left. In a 32-bit value, bit p has 31 - p
                // leading zeros. Thus the pixel offset in the byte is 7 - p = LeadingZeroCount(bits) - 24. The loop then clears the bit, so that the next
                // iteration finds the next pixel.
                int offset = BitOperations.LeadingZeroCount(bits) - 24;
                bits &= ~(0x80U >> offset);

                // The bits after the last pixel of the row are padding.
                int x = (maskIndex * 8) + offset;
                if (x >= width)
                {
                    break;
                }

                op.SetTransparent(x);
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
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, 24));
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, width * 3))
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
    /// Reads a 16-bit bitmap with the 5-5-5 layout: blue in bits 0..4, green in bits 5..9 and red in bits 10..14.
    /// </summary>
    /// <remarks>
    /// This is the <see cref="Bgra5551"/> layout, so the row goes to the bulk <see cref="Bgra5551"/> conversion, as in
    /// the TGA decoder. When bit 15 is not alpha, the read sets it in each pixel, so that each pixel is opaque.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    /// <param name="hasAlpha">Whether bit 15 is alpha.</param>
    private void ReadRgb555<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        bool inverted,
        bool hasAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, 16));
        Span<byte> rowSpan = row.GetSpan();

        // Each pixel is one little-endian 16-bit value, so the pixel bytes of the row read as one ushort per pixel.
        Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(rowSpan[..(width * 2)]);

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, width * 2))
            {
                return;
            }

            if (!hasAlpha)
            {
                TensorPrimitives.BitwiseOr(samples, (ushort)BmpConstants.Bgr555Masks.Alpha, samples);
            }

            int newY = Invert(y, height, inverted);
            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(newY);
            PixelOperations<TPixel>.Instance.FromBgra5551Bytes(
                this.configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Reads a 16-bit bitmap with the 5-6-5 layout: blue in bits 0..4, green in bits 5..10 and red in bits 11..15.
    /// </summary>
    /// <remarks>
    /// This is the <see cref="Bgr565"/> layout, so the row goes to the bulk <see cref="Bgr565"/> conversion. The
    /// layout has no alpha bit, so each pixel is opaque.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="inverted">Whether the bitmap is inverted.</param>
    private void ReadRgb565<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        bool inverted)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, 16));
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, width * 2))
            {
                return;
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(Invert(y, height, inverted));
            PixelOperations<TPixel>.Instance.FromBgr565Bytes(
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
        using IMemoryOwner<byte> row = this.memoryAllocator.Allocate<byte>(BmpConstants.GetStride(width, 32));
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (!this.TryReadPixelBytes(stream, rowSpan, width * 4))
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
    /// Reads a 32-bit BI_RGB icon bitmap. The fourth byte of each pixel is alpha or is not used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The icon format does not tell if the fourth byte of an icon pixel is alpha.
    /// Many writers store 0 in the unused byte. Thus an icon bitmap whose fourth bytes are all 0 is opaque, and an icon
    /// bitmap with any other fourth byte has alpha.
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
        BmpBitFields opaque = new(BmpConstants.Bgra32Masks.Red, BmpConstants.Bgra32Masks.Green, BmpConstants.Bgra32Masks.Blue, 0);

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
        const uint alphaMask = BmpConstants.Bgra32Masks.Alpha;
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
    /// Data that ends before the image is complete throws in the Strict and IgnoreAncillary modes. In the
    /// IgnoreImageData mode, the method returns <see langword="false"/>, and the caller stops and keeps the rows decoded
    /// before the end. The image buffer is cleared at allocation, so the rows after the end are transparent black.
    /// </para>
    /// </remarks>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="buffer">The buffer, with room for the required bytes and any padding.</param>
    /// <param name="requiredBytes">The byte count of the pixels in the row.</param>
    /// <returns><see langword="true"/> if the stream has the pixel bytes of the row. Else <see langword="false"/>.</returns>
    private bool TryReadPixelBytes(BufferedReadStream stream, Span<byte> buffer, int requiredBytes)
    {
        if (stream.ReadAtLeast(buffer, requiredBytes, false) >= requiredBytes)
        {
            return true;
        }

        this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(PixelDataEndMessage);
        return false;
    }

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

        // The method reads the header size first, and then the rest of the header. The dispatch below rejects each size that it does not know. The method
        // rejects a size outside the range of known sizes before the read. Thus the decoder reports a damaged size as an unsupported header, not as truncated
        // data. Inside the range, the read stays inside the buffer.
        ReadHeaderBytes(stream, buffer[..BmpInfoHeader.HeaderSizeSize]);
        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        if (headerSize is < BmpInfoHeader.CoreSize or > BmpInfoHeader.MaxHeaderSize)
        {
            BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. HeaderSize '{headerSize}'.");
        }

        ReadHeaderBytes(stream, buffer[BmpInfoHeader.HeaderSizeSize..headerSize]);

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
            if (this.infoHeader.Compression == BmpInfoCompression.BitFields)
            {
                Span<byte> bitfieldsBuffer = stackalloc byte[12];
                ReadHeaderBytes(stream, bitfieldsBuffer);
                Span<byte> data = bitfieldsBuffer;
                this.infoHeader.RedMask = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
                this.infoHeader.GreenMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
                this.infoHeader.BlueMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            }
            else if (this.infoHeader.Compression == BmpInfoCompression.AlphaBitFields)
            {
                Span<byte> bitfieldsBuffer = stackalloc byte[16];
                ReadHeaderBytes(stream, bitfieldsBuffer);
                Span<byte> data = bitfieldsBuffer;
                this.infoHeader.RedMask = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
                this.infoHeader.GreenMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
                this.infoHeader.BlueMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
                this.infoHeader.AlphaMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
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

            // Only PROFILE_EMBEDDED profile data is ICC data. For PROFILE_LINKED, the data is the file name of the
            // profile, so the decoder does not read it.
            if (this.infoHeader.CsType is BmpColorSpace.PROFILE_EMBEDDED)
            {
                long streamPosition = stream.Position;
                this.ReadIccProfile(stream, this.metadata, infoHeaderStart);
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

        // The masks give the layout of a 16-bit or 32-bit pixel. BI_RGB has fixed masks: 5-5-5 at 16 bits, and the bytes B, G, R at 32 bits. See
        // BITMAPINFOHEADER, biBitCount: https://learn.microsoft.com/en-us/previous-versions/dd183376(v=vs.85)
        // The other compressions use no masks. This switch sets the masks once. The color type below and the pixel decode (see ReadPixelData and ReadBitFields)
        // then read the same layout.
        switch (this.infoHeader.Compression)
        {
            case BmpInfoCompression.BitFields:
            case BmpInfoCompression.AlphaBitFields:
                break;

            // RLE8 data has no 16-bit pixels. But a 16-bit RLE8 header keeps the 16-bit depth of the 5-5-5 layout in the metadata, so a re-encode keeps it.
            case BmpInfoCompression.Rgb or BmpInfoCompression.Rle8 when bitsPerPixel is 16:
                this.infoHeader.RedMask = BmpConstants.Bgr555Masks.Red;
                this.infoHeader.GreenMask = BmpConstants.Bgr555Masks.Green;
                this.infoHeader.BlueMask = BmpConstants.Bgr555Masks.Blue;
                this.infoHeader.AlphaMask = 0;
                break;

            case BmpInfoCompression.Rgb when bitsPerPixel is 32:
                // BI_RGB does not use the high byte of a 32-bit pixel: "The high byte in each DWORD is not used." Many bitmaps hold values other than 0 in that
                // byte, and browsers ignore these values, so the bitmap is opaque. Two cases keep alpha in that byte. One is an icon bitmap, as icon readers
                // expect (see ReadRgb32Slow). The other is a V4 or V5 header whose alpha mask selects that byte.
                bool alpha32 = this.processedAlphaMask || this.infoHeader.AlphaMask == BmpConstants.Bgra32Masks.Alpha;
                this.infoHeader.RedMask = BmpConstants.Bgra32Masks.Red;
                this.infoHeader.GreenMask = BmpConstants.Bgra32Masks.Green;
                this.infoHeader.BlueMask = BmpConstants.Bgra32Masks.Blue;
                this.infoHeader.AlphaMask = alpha32 ? BmpConstants.Bgra32Masks.Alpha : 0;
                break;

            default:
                this.infoHeader.RedMask = 0;
                this.infoHeader.GreenMask = 0;
                this.infoHeader.BlueMask = 0;
                this.infoHeader.AlphaMask = 0;
                break;
        }

        bool alpha = this.infoHeader.AlphaMask != 0;
        bool rgb565 = this.infoHeader.RedMask is BmpConstants.Bgr565Masks.Red
            && this.infoHeader.GreenMask is BmpConstants.Bgr565Masks.Green
            && this.infoHeader.BlueMask is BmpConstants.Bgr565Masks.Blue;

        // The 16-bit layouts of the encoder keep each decoded value only for their own masks. A channel of other width
        // widens to 8 bits with values that a 5-bit or 6-bit channel cannot hold. For example, 2 bits give 0, 85, 170
        // and 255. 85 is 10 in 5 bits, and 10 widens to 82. So other 16-bit masks map to the 24-bit or 32-bit layout,
        // which keeps each decoded value.
        bool rgb555 = this.infoHeader.RedMask is BmpConstants.Bgr555Masks.Red
            && this.infoHeader.GreenMask is BmpConstants.Bgr555Masks.Green
            && this.infoHeader.BlueMask is BmpConstants.Bgr555Masks.Blue;
        bool rgba5551 = rgb555 && this.infoHeader.AlphaMask is BmpConstants.Bgr555Masks.Alpha;

        (this.bmpMetadata.ColorType, BmpBitsPerPixel colorDepth) = bitsPerPixel switch
        {
            <= 8 => (BmpColorType.Palette, (BmpBitsPerPixel)bitsPerPixel),
            16 when rgb565 && !alpha => (BmpColorType.Bgr565, BmpBitsPerPixel.Bit16),
            16 when alpha => rgba5551 ? (BmpColorType.Bgra5551, BmpBitsPerPixel.Bit16) : (BmpColorType.Bgra, BmpBitsPerPixel.Bit32),
            16 => rgb555 ? (BmpColorType.Bgr, BmpBitsPerPixel.Bit16) : (BmpColorType.Bgr, BmpBitsPerPixel.Bit24),
            _ => (alpha ? BmpColorType.Bgra : BmpColorType.Bgr, (BmpBitsPerPixel)bitsPerPixel)
        };

        this.bmpMetadata.BitsPerPixel = colorDepth;

        // The encoder writes RLE only for 4-bit and 8-bit palette bitmaps. It writes an RLE24 bitmap uncompressed.
        this.bmpMetadata.Compression = this.infoHeader.Compression switch
        {
            BmpInfoCompression.Rle4 or BmpInfoCompression.Rle8 or BmpInfoCompression.Rle24 => BmpCompression.RunLength,
            BmpInfoCompression.Huffman1D => BmpCompression.Huffman1D,
            _ => BmpCompression.None
        };

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
        // This method owns the profile checks. The caller checks only that the profile is embedded. A header with no profile offset or no profile size has no
        // profile, so the decode continues without one.
        if (this.infoHeader.ProfileData == 0 || this.infoHeader.ProfileSize == 0)
        {
            return;
        }

        long profileStart = infoHeaderStart + this.infoHeader.ProfileData;
        if (this.infoHeader.ProfileData < 0 ||
            this.infoHeader.ProfileSize < 0 ||
            !stream.IsReadRangeValid(profileStart, (uint)this.infoHeader.ProfileSize))
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read BMP ICC profile.");
            return;
        }

        stream.Position = profileStart;
        IccReader reader = new();
        try
        {
            if (!reader.TryReadSegment(stream, this.infoHeader.ProfileSize))
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read BMP ICC profile.");
                return;
            }

            imageMetadata.IccProfile = reader.ToProfile(this.Options.SegmentIntegrityHandling.IgnoresAncillaryErrors());
        }
        catch (InvalidIccProfileException ex)
        {
            // The ICC reader reports a damaged or oversized profile. The Strict mode throws. The other modes continue without the profile.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid BMP ICC profile.", ex);
        }
    }

    /// <summary>
    /// Reads the <see cref="BmpFileHeader"/> from the stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadFileHeader(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[BmpFileHeader.Size];
        ReadHeaderBytes(stream, buffer);

        short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(buffer);
        switch (fileTypeMarker)
        {
            case BmpConstants.TypeMarkers.Bitmap:
                this.fileHeader = BmpFileHeader.Parse(buffer);
                break;
            case BmpConstants.TypeMarkers.BitmapArray:
                // Because we only decode the first bitmap in the array, the array header will be ignored.
                // The bitmap file header of the first image follows the array header.
                ReadHeaderBytes(stream, buffer);
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
    /// Reads header bytes. If the header data ends early, the method throws before the decoder allocates the pixels.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <param name="buffer">The destination. The method fills all of it.</param>
    private static void ReadHeaderBytes(BufferedReadStream stream, Span<byte> buffer)
    {
        if (!stream.TryReadExactly(buffer))
        {
            BmpThrowHelper.ThrowInvalidImageContentException("The BMP header data ends before the header is complete.");
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
            BmpInfoCompression.Rle4 or BmpInfoCompression.Rle8 or BmpInfoCompression.Huffman1D => true,
            BmpInfoCompression.Rgb => this.infoHeader.BitsPerPixel <= 8,
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

        // Each compression is valid only with some bit depths. Huffman 1D is "valid for a bi-level (one bit per pel) bit map" only.
        // See the OS/2 Presentation Manager Programming Reference, BITMAPINFOHEADER2: http://www.edm2.com/index.php/BITMAPINFOHEADER2
        // The check runs before the pixel allocation, so Identify rejects each header that Decode rejects. The pixel decode (see ReadPixelData) has a path for
        // each combination that passes.
        ushort bitsPerPixel = this.infoHeader.BitsPerPixel;
        switch (this.infoHeader.Compression)
        {
            // RLE24 and Huffman 1D are valid only in OS/2 2.x headers. The OS/2 header parse maps them from the OS/2 values 4 and 3.
            // A Windows header has no compression 100 or 101.
            case BmpInfoCompression.Rle24 or BmpInfoCompression.Huffman1D when this.bmpMetadata.InfoHeaderType is not BmpInfoHeaderType.Os2Version2:
                BmpThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of bitmap files.");
                break;

            case BmpInfoCompression.Rgb when bitsPerPixel is 1 or 2 or 4 or 8 or 16 or 24 or 32:
            case BmpInfoCompression.Rle8:
            case BmpInfoCompression.Rle4 when bitsPerPixel is > 0 and <= 4:
            case BmpInfoCompression.Rle24 when bitsPerPixel is 24:
            case BmpInfoCompression.Huffman1D when bitsPerPixel is 1:
            case BmpInfoCompression.BitFields or BmpInfoCompression.AlphaBitFields when bitsPerPixel is 16 or 32:
                break;

            case BmpInfoCompression.Rgb:
            case BmpInfoCompression.Rle4:
            case BmpInfoCompression.Rle24:
            case BmpInfoCompression.Huffman1D:
            case BmpInfoCompression.BitFields:
            case BmpInfoCompression.AlphaBitFields:
                BmpThrowHelper.ThrowInvalidImageContentException($"Compression {this.infoHeader.Compression} does not allow {bitsPerPixel} bits per pixel.");
                break;

            default:
                BmpThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of bitmap files.");
                break;
        }

        // The pixel data starts after the headers and inside the stream. The check runs before the decoder allocates or reads the color table.
        if (this.fileHeader.HasValue && (this.fileHeader.Value.Offset < stream.Position || this.fileHeader.Value.Offset > stream.Length))
        {
            BmpThrowHelper.ThrowInvalidImageContentException(
                $"Pixel data offset {this.fileHeader.Value.Offset} is not between the end of the headers and the end of the file.");
        }

        // Core headers store RGBTRIPLE entries; later headers store RGBQUAD or RGB2 entries.
        int bytesPerColorMapEntry = this.infoHeader.HeaderSize == BmpInfoHeader.CoreSize
            ? Unsafe.SizeOf<Bgr24>()
            : Unsafe.SizeOf<Bgra32>();

        // ClrUsed is an unsigned DWORD on disk, so retain its full range while validating the palette count.
        uint colorCount = (uint)this.infoHeader.ClrUsed;

        // Indexed palettes cannot exceed the number of addressable colors; zero selects the full table.
        if (bitsPerPixel is 1 or 2 or 4 or 8)
        {
            uint maxColors = (uint)ColorNumerics.GetColorCountForBitDepth(bitsPerPixel);

            if (colorCount > maxColors)
            {
                BmpThrowHelper.ThrowInvalidImageContentException(
                    $"The color table contains {colorCount} entries, exceeding the maximum of {maxColors} for a {bitsPerPixel}-bit image.");
            }

            if (colorCount == 0)
            {
                colorCount = maxColors;
            }
        }

        // Widen before multiplying so a malicious count cannot wrap to a small read followed by a huge palette allocation.
        ulong colorMapByteCount = (ulong)colorCount * (uint)bytesPerColorMapEntry;

        // A palette in a file must fit before the pixel data. An embedded DIB palette with no file header uses the stream limit below. The offset check above
        // keeps the offset at or after the stream position.
        if (this.fileHeader.HasValue && colorMapByteCount > (ulong)(this.fileHeader.Value.Offset - stream.Position))
        {
            BmpThrowHelper.ThrowInvalidImageContentException(
                "Reading the color map would read beyond the bitmap offset. " +
                $"Either the color map size of '{colorMapByteCount}' is invalid or the bitmap offset.");
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

            // TryGetReadLength proves that the stream has these bytes.
            stream.ReadExactly(paletteBytes);

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

        // The checks above keep the offset at or after the end of the color table, and inside the stream.
        if (this.fileHeader.HasValue)
        {
            stream.Position = this.fileHeader.Value.Offset;
        }
    }

    /// <summary>
    /// Makes a palette pixel transparent: it takes the transparent form of the palette entry of the pixel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    private readonly ref struct PaletteMaskOperator<TPixel> : IMaskOperator
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Span<TPixel> row;
        private readonly ReadOnlySpan<byte> indices;
        private readonly ReadOnlySpan<TPixel> transparent;
        private readonly int bitsPerPixel;
        private readonly int indexMask;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteMaskOperator{TPixel}"/> struct.
        /// </summary>
        /// <param name="row">The image row.</param>
        /// <param name="indices">The palette indices of the row, packed, the first pixel in the most significant bits.</param>
        /// <param name="transparent">The transparent form of each palette entry, one for each index value.</param>
        /// <param name="bitsPerPixel">The bit count of one index: 1, 2, 4 or 8.</param>
        /// <param name="indexMask">The mask of the low <paramref name="bitsPerPixel"/> bits.</param>
        public PaletteMaskOperator(Span<TPixel> row, ReadOnlySpan<byte> indices, ReadOnlySpan<TPixel> transparent, int bitsPerPixel, int indexMask)
        {
            this.row = row;
            this.indices = indices;
            this.transparent = transparent;
            this.bitsPerPixel = bitsPerPixel;
            this.indexMask = indexMask;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTransparent(int x)
        {
            // The index of pixel x starts at bit x * bitsPerPixel of the row, the first pixel in the most significant bits of a byte. The code shifts the index
            // down to bit 0 and keeps its low bitsPerPixel bits.
            int bitPosition = x * this.bitsPerPixel;
            int shift = 8 - this.bitsPerPixel - (bitPosition & 7);
            int index = (this.indices[bitPosition >> 3] >> shift) & this.indexMask;
            this.row[x] = this.transparent[index];
        }
    }

    /// <summary>
    /// Makes a decoded pixel transparent: it keeps the color and sets alpha to 0.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    private readonly ref struct AlphaMaskOperator<TPixel> : IMaskOperator
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Span<TPixel> row;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaMaskOperator{TPixel}"/> struct.
        /// </summary>
        /// <param name="row">The image row.</param>
        public AlphaMaskOperator(Span<TPixel> row) => this.row = row;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTransparent(int x)
        {
            // The alpha changes on unassociated color, before the pixel type applies its alpha association, as for the transparent palette entries of the
            // palette path.
            Vector4 color = this.row[x].ToUnassociatedScaledVector4();
            color.W = 0F;
            this.row[x] = TPixel.FromUnassociatedScaledVector4(color);
        }
    }
}
