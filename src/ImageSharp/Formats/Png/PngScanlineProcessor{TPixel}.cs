// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Converts the defiltered scanlines of one PNG frame to pixels.
/// </summary>
/// <remarks>
/// <para>
/// The processor holds the state that each row of a frame shares. This state is the sample layout, the transparent
/// color, a pixel table for palette and low bit depth samples, and the 16-bit sample buffer. Thus each row is one bulk
/// conversion with no per-row setup and no per-pixel color conversion.
/// </para>
/// <para>
/// Palette samples, gray samples of 1, 2 and 4 bits, and 8-bit gray samples with a tRNS color are table loads. The
/// table has one entry for each value that a sample can have, so <see cref="PaletteExpander"/> needs no bounds check.
/// The other layouts convert through <see cref="PixelOperations{TPixel}"/>.
/// </para>
/// <para>
/// 16-bit samples are big-endian. See PNG Third Edition, section 7.1 "Integers and byte order":
/// https://www.w3.org/TR/png-3/#7Integers-and-byte-order. The processor copies them in native byte order to a sample
/// buffer before the conversion. The scanline stays unchanged, because the filter of the next row reads it.
/// </para>
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal readonly struct PngScanlineProcessor<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly PngColorType colorType;
    private readonly int bitDepth;
    private readonly bool hasTransparentColor;
    private readonly Rgb24 transparentRgb24;
    private readonly Rgb48 transparentRgb48;
    private readonly ushort transparentL16;

    /// <summary>
    /// The pixel for each sample value of palette rows and of gray rows that use a table. Otherwise, <see langword="null"/>.
    /// </summary>
    private readonly IMemoryOwner<TPixel>? table;

    /// <summary>
    /// The native-order samples of one 16-bit row. Otherwise, <see langword="null"/>.
    /// </summary>
    private readonly IMemoryOwner<ushort>? samples;

    /// <summary>
    /// Initializes a new instance of the <see cref="PngScanlineProcessor{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration of the decode.</param>
    /// <param name="colorType">The color type of the image.</param>
    /// <param name="bitDepth">The bit depth of one sample: 1, 2, 4, 8 or 16.</param>
    /// <param name="width">The pixel count of the widest row of the frame.</param>
    /// <param name="palette">The palette of the image. A palette image must have one.</param>
    /// <param name="transparentColor">The tRNS color of a gray or RGB image, or <see langword="null"/>.</param>
    public PngScanlineProcessor(
        Configuration configuration,
        PngColorType colorType,
        int bitDepth,
        int width,
        ReadOnlyMemory<Color>? palette,
        Color? transparentColor)
    {
        MemoryAllocator allocator = configuration.MemoryAllocator;
        this.configuration = configuration;
        this.colorType = colorType;
        this.bitDepth = bitDepth;

        // Only gray and RGB images use a tRNS color. The tRNS chunk of a palette image holds palette alpha.
        if (transparentColor.HasValue && colorType is PngColorType.Grayscale or PngColorType.Rgb)
        {
            Color color = transparentColor.Value;
            this.hasTransparentColor = true;
            this.transparentRgb24 = color.ToPixel<Rgb24>();
            this.transparentRgb48 = color.ToPixel<Rgb48>();
            this.transparentL16 = color.ToPixel<L16>().PackedValue;
        }

        if (colorType is PngColorType.Palette)
        {
            if (palette is null)
            {
                PngThrowHelper.ThrowMissingPalette();
            }

            this.table = CreatePaletteTable(allocator, palette.Value.Span, bitDepth);
        }
        else if (colorType is PngColorType.Grayscale && (bitDepth < 8 || (bitDepth == 8 && this.hasTransparentColor)))
        {
            this.table = this.CreateGrayTable(allocator, transparentColor);
        }

        if (bitDepth == 16)
        {
            int channels = colorType switch
            {
                PngColorType.Grayscale => 1,
                PngColorType.GrayscaleWithAlpha => 2,
                PngColorType.Rgb => 3,
                _ => 4,
            };

            this.samples = allocator.Allocate<ushort>(width * channels);
        }
    }

    /// <summary>
    /// Converts one defiltered scanline to pixels.
    /// </summary>
    /// <param name="scanline">The scanline without its filter type byte. It holds the samples of each destination pixel.</param>
    /// <param name="destination">The destination pixels. The length is the pixel count of the scanline.</param>
    public void ProcessScanline(ReadOnlySpan<byte> scanline, Span<TPixel> destination)
    {
        int count = destination.Length;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        // The constructor creates the table for palette rows, gray rows of fewer than 8 bits, and 8-bit gray rows
        // with a tRNS color. Thus a table selects the table load.
        if (this.table is not null)
        {
            PaletteExpander.Expand(scanline, this.bitDepth, this.table.GetSpan(), destination);
            return;
        }

        // The constructor creates the sample buffer for each 16-bit row. Palette rows have no 16-bit samples.
        if (this.samples is not null)
        {
            Span<ushort> buffer = this.samples.GetSpan();
            switch (this.colorType)
            {
                case PngColorType.Grayscale:
                    this.ProcessGray16(ReadSamples(scanline, buffer[..count]), destination);
                    break;

                case PngColorType.GrayscaleWithAlpha:
                    operations.FromLa32(this.configuration, MemoryMarshal.Cast<ushort, La32>(ReadSamples(scanline, buffer[..(count * 2)])), destination);
                    break;

                case PngColorType.Rgb:
                    this.ProcessRgb48(ReadSamples(scanline, buffer[..(count * 3)]), destination);
                    break;

                default:
                    operations.FromRgba64(this.configuration, MemoryMarshal.Cast<ushort, Rgba64>(ReadSamples(scanline, buffer[..(count * 4)])), destination);
                    break;
            }

            return;
        }

        // The rows that remain have 8-bit samples. An 8-bit gray row here has no tRNS color, because a tRNS color
        // gives it a table.
        switch (this.colorType)
        {
            case PngColorType.Grayscale:
                operations.FromL8(this.configuration, MemoryMarshal.Cast<byte, L8>(scanline[..count]), destination);
                break;

            case PngColorType.GrayscaleWithAlpha:
                operations.FromLa16(this.configuration, MemoryMarshal.Cast<byte, La16>(scanline[..(count * 2)]), destination);
                break;

            case PngColorType.Rgb:
                this.ProcessRgb24(scanline[..(count * 3)], destination);
                break;

            default:
                operations.FromRgba32Bytes(this.configuration, scanline[..(count * 4)], destination, count);
                break;
        }
    }

    /// <summary>
    /// Converts one defiltered Adam7 pass scanline to pixels, and stores them at the columns of the pass.
    /// </summary>
    /// <param name="scanline">The pass scanline without its filter type byte.</param>
    /// <param name="columns">The pixel count of the pass scanline.</param>
    /// <param name="passRow">The buffer for the pixels of the pass scanline. It holds at least <paramref name="columns"/> pixels.</param>
    /// <param name="row">The destination row of the frame. Column 0 is the first frame column.</param>
    /// <param name="firstColumn">The first column of the pass.</param>
    /// <param name="columnIncrement">The column step of the pass.</param>
    public void ProcessInterlacedScanline(
        ReadOnlySpan<byte> scanline,
        int columns,
        Span<TPixel> passRow,
        Span<TPixel> row,
        int firstColumn,
        int columnIncrement)
    {
        // Adam7.ComputeColumns gives the column count of the pass, so the last column,
        // firstColumn + ((columns - 1) * columnIncrement), is inside the row.
        DebugGuard.MustBeLessThan(firstColumn + ((columns - 1) * columnIncrement), row.Length, nameof(columns));

        // A pass with a column step of 1 has contiguous pixels. Adam7 pass 7 is such a pass, and it holds half of the
        // pixels of the image. Its pixels convert straight into the row.
        if (columnIncrement == 1)
        {
            this.ProcessScanline(scanline, row.Slice(firstColumn, columns));
            return;
        }

        // The bulk conversion writes contiguous pixels. Thus the pass row converts to a buffer first, and one loop
        // then stores each pixel at its pass column.
        Span<TPixel> pass = passRow[..columns];
        this.ProcessScanline(scanline, pass);

        ref TPixel passRef = ref MemoryMarshal.GetReference(pass);
        ref TPixel rowRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(row), (uint)firstColumn);
        for (nuint x = 0, o = 0; x < (uint)columns; x++, o += (uint)columnIncrement)
        {
            Unsafe.Add(ref rowRef, o) = Unsafe.Add(ref passRef, x);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.table?.Dispose();
        this.samples?.Dispose();
    }

    /// <summary>
    /// Creates the palette table. It has one entry for each index that <paramref name="bitDepth"/> bits can hold.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="palette">The palette of the image. It has at least one entry.</param>
    /// <param name="bitDepth">The bit depth of one index: 1, 2, 4 or 8.</param>
    /// <returns>The table.</returns>
    private static IMemoryOwner<TPixel> CreatePaletteTable(MemoryAllocator allocator, ReadOnlySpan<Color> palette, int bitDepth)
    {
        IMemoryOwner<TPixel> owner = allocator.Allocate<TPixel>(1 << bitDepth);
        Span<TPixel> table = owner.GetSpan();

        // ICC-converted palette colors can exceed 8-bit precision. Each entry converts only to the destination
        // pixel format, so each pixel gets the same value as a conversion of its own palette color.
        int count = Math.Min(palette.Length, table.Length);
        Color.ToPixel(palette[..count], table);

        // An index past the palette selects the last palette color, as the decode did before it used a table.
        table[count..].Fill(table[count - 1]);
        return owner;
    }

    /// <summary>
    /// Creates the gray table of a gray image with 8 bits or fewer. It has one entry for each sample value.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="transparentColor">The tRNS color, or <see langword="null"/>.</param>
    /// <returns>The table.</returns>
    private IMemoryOwner<TPixel> CreateGrayTable(MemoryAllocator allocator, Color? transparentColor)
    {
        IMemoryOwner<TPixel> owner = allocator.Allocate<TPixel>(1 << this.bitDepth);
        Span<TPixel> table = owner.GetSpan();

        // A sample of n bits has 2^n - 1 as its largest value. The factor 255 / (2^n - 1) maps that value to 255.
        // The factor is 255, 85, 17 or 1 for 1, 2, 4 or 8 bits. The tRNS value is a sample value, so it gets the same
        // factor.
        int scale = 255 / (table.Length - 1);
        if (transparentColor is null)
        {
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = TPixel.FromL8(new L8((byte)(i * scale)));
            }
        }
        else
        {
            byte transparent = (byte)(transparentColor.Value.ToPixel<L8>().PackedValue * scale);
            for (int i = 0; i < table.Length; i++)
            {
                byte luminance = (byte)(i * scale);
                table[i] = TPixel.FromLa16(new La16(luminance, luminance == transparent ? byte.MinValue : byte.MaxValue));
            }
        }

        return owner;
    }

    /// <summary>
    /// Copies the big-endian 16-bit samples of a scanline to a sample buffer in native byte order.
    /// </summary>
    /// <param name="scanline">The scanline.</param>
    /// <param name="destination">The sample buffer. Its length is the sample count.</param>
    /// <returns>The native-order samples.</returns>
    private static Span<ushort> ReadSamples(ReadOnlySpan<byte> scanline, Span<ushort> destination)
    {
        ReadOnlySpan<ushort> source = MemoryMarshal.Cast<byte, ushort>(scanline[..(destination.Length * sizeof(ushort))]);

        // The byte order of the file is always big-endian. The check is for the byte order of the CPU, which the
        // ushort view of the bytes uses. A little-endian CPU must swap the two bytes of each sample. A big-endian
        // CPU reads the file order as its native order. The JIT treats the check as a constant.
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(source, destination);
        }
        else
        {
            source.CopyTo(destination);
        }

        return destination;
    }

    /// <summary>
    /// Converts one row of 16-bit gray samples.
    /// </summary>
    /// <param name="samples">The native-order samples, one for each pixel.</param>
    /// <param name="destination">The destination pixels.</param>
    private void ProcessGray16(ReadOnlySpan<ushort> samples, Span<TPixel> destination)
    {
        if (!this.hasTransparentColor)
        {
            PixelOperations<TPixel>.Instance.FromL16(this.configuration, MemoryMarshal.Cast<ushort, L16>(samples), destination);
            return;
        }

        // A pixel with the tRNS value is transparent. Each other pixel is opaque.
        ushort transparent = this.transparentL16;
        for (int x = 0; x < destination.Length; x++)
        {
            ushort luminance = samples[x];
            destination[x] = TPixel.FromLa32(new La32(luminance, luminance == transparent ? ushort.MinValue : ushort.MaxValue));
        }
    }

    /// <summary>
    /// Converts one row of 8-bit RGB samples.
    /// </summary>
    /// <param name="scanline">The samples, three for each pixel.</param>
    /// <param name="destination">The destination pixels.</param>
    private void ProcessRgb24(ReadOnlySpan<byte> scanline, Span<TPixel> destination)
    {
        if (!this.hasTransparentColor)
        {
            PixelOperations<TPixel>.Instance.FromRgb24Bytes(this.configuration, scanline, destination, destination.Length);
            return;
        }

        // A pixel with the tRNS color is transparent. Each other pixel is opaque.
        ReadOnlySpan<Rgb24> pixels = MemoryMarshal.Cast<byte, Rgb24>(scanline);
        Rgb24 transparent = this.transparentRgb24;
        for (int x = 0; x < destination.Length; x++)
        {
            Rgb24 rgb = pixels[x];
            destination[x] = TPixel.FromRgba32(new Rgba32(rgb.R, rgb.G, rgb.B, rgb.Equals(transparent) ? byte.MinValue : byte.MaxValue));
        }
    }

    /// <summary>
    /// Converts one row of 16-bit RGB samples.
    /// </summary>
    /// <param name="samples">The native-order samples, three for each pixel.</param>
    /// <param name="destination">The destination pixels.</param>
    private void ProcessRgb48(ReadOnlySpan<ushort> samples, Span<TPixel> destination)
    {
        ReadOnlySpan<Rgb48> pixels = MemoryMarshal.Cast<ushort, Rgb48>(samples);
        if (!this.hasTransparentColor)
        {
            PixelOperations<TPixel>.Instance.FromRgb48(this.configuration, pixels, destination);
            return;
        }

        // A pixel with the tRNS color is transparent. Each other pixel is opaque.
        Rgb48 transparent = this.transparentRgb48;
        for (int x = 0; x < destination.Length; x++)
        {
            Rgb48 rgb = pixels[x];
            destination[x] = TPixel.FromRgba64(new Rgba64(rgb.R, rgb.G, rgb.B, rgb.Equals(transparent) ? ushort.MinValue : ushort.MaxValue));
        }
    }
}
