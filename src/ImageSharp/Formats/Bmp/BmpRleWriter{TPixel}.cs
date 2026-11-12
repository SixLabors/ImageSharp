// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Writes decoded RLE runs straight into the image rows, in the order of the stored rows.
/// </summary>
/// <remarks>
/// <para>
/// An RLE bitmap is one sequence of pixels: the stored rows one after the other, each <c>width</c> pixels. A run
/// can continue past the end of a row into the next row. The writer keeps the position in that sequence, and splits
/// each run at the row ends. Thus the decoder needs no buffer for the full image.
/// </para>
/// <para>
/// The position only moves forward. Each pixel is written one time. A run writes it, or an end of line, a delta or an
/// end of bitmap writes it with the skipped pixel color. When the last pixel is written, the image is complete. A run or a skip
/// that continues past the last pixel has nothing more to change, so the writer discards the rest of it.
/// </para>
/// <para>
/// The writer keeps the span of the current image row, so a run inside a row writes with no row lookup.
/// </para>
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal ref struct BmpRleWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Buffer2D<TPixel> pixels;
    private readonly int width;
    private readonly int height;
    private readonly bool inverted;
    private readonly TPixel skipped;

    // The position: pixel x of stored row y. Stored row 0 is the bottom image row unless the bitmap is inverted.
    private int x;
    private int y;

    // The image row of stored row y. It is empty when the image is complete.
    private Span<TPixel> row;

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpRleWriter{TPixel}"/> struct.
    /// </summary>
    /// <param name="pixels">The image pixels.</param>
    /// <param name="inverted">Whether the first stored row is the top image row.</param>
    /// <param name="skipped">The color of the pixels that the data skips.</param>
    public BmpRleWriter(Buffer2D<TPixel> pixels, bool inverted, TPixel skipped)
    {
        this.pixels = pixels;
        this.width = pixels.Width;
        this.height = pixels.Height;
        this.inverted = inverted;
        this.skipped = skipped;
        this.row = this.RowOf(0);
    }

    /// <summary>
    /// Gets a value indicating whether each pixel of the image is written.
    /// </summary>
    public readonly bool IsComplete => this.y >= this.height || this.width == 0;

    /// <summary>
    /// Writes <paramref name="count"/> pixels of one color.
    /// </summary>
    /// <param name="value">The color.</param>
    /// <param name="count">The pixel count.</param>
    public void Fill(TPixel value, long count)
    {
        while (count > 0 && !this.IsComplete)
        {
            Span<TPixel> span = this.Take(count);
            span.Fill(value);
            count -= span.Length;
        }
    }

    /// <summary>
    /// Writes <paramref name="count"/> pixels that alternate between two colors, <paramref name="first"/> first.
    /// </summary>
    /// <remarks>
    /// Pixel i of the run is <paramref name="first"/> for an even i and <paramref name="second"/> for an odd i.
    /// A row end can split the run after an odd pixel count. The next part then starts with an odd i. Thus the two
    /// colors swap for that part, and the even and odd rule holds again from its first pixel.
    /// </remarks>
    /// <param name="first">The color of the even pixels.</param>
    /// <param name="second">The color of the odd pixels.</param>
    /// <param name="count">The pixel count.</param>
    public void FillPair(TPixel first, TPixel second, int count)
    {
        while (count > 0 && !this.IsComplete)
        {
            Span<TPixel> span = this.Take(count);
            ref TPixel destination = ref MemoryMarshal.GetReference(span);
            int length = span.Length;
            int i = 0;

            // Bounds: i + 1 < length keeps both stores of a pair inside the span.
            for (; i + 1 < length; i += 2)
            {
                Unsafe.Add(ref destination, (uint)i) = first;
                Unsafe.Add(ref destination, (uint)(i + 1)) = second;
            }

            if (i < length)
            {
                Unsafe.Add(ref destination, (uint)i) = first;
                (first, second) = (second, first);
            }

            count -= length;
        }
    }

    /// <summary>
    /// Writes one pixel for each 8-bit palette index.
    /// </summary>
    /// <param name="indices">The palette indices, one byte for each pixel.</param>
    /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>, with 256 entries.</param>
    public void Write(scoped ReadOnlySpan<byte> indices, scoped ReadOnlySpan<TPixel> palette)
    {
        while (indices.Length > 0 && !this.IsComplete)
        {
            Span<TPixel> span = this.Take(indices.Length);
            BmpPaletteExpander.Expand<TPixel, BmpPaletteExpander.Index8Operator>(indices[..span.Length], palette, span);
            indices = indices[span.Length..];
        }
    }

    /// <summary>
    /// Writes one pixel for each 4-bit palette index of packed data: two indices in each byte, high nibble first.
    /// </summary>
    /// <remarks>
    /// Pixel i of the run is in byte i / 2: the high nibble for an even i, the low nibble for an odd i. Each part of
    /// the run inside one row expands in one pass with the 4-bit index operator. A row end can split the run after an
    /// odd pixel count. The next part then starts on a low nibble. Thus that part writes its first pixel alone, and
    /// the rest of the part starts on a high nibble again.
    /// </remarks>
    /// <param name="packed">The packed indices. It holds at least (count + 1) / 2 bytes.</param>
    /// <param name="count">The pixel count.</param>
    /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>, with 256 entries.</param>
    public void Write4(scoped ReadOnlySpan<byte> packed, int count, scoped ReadOnlySpan<TPixel> palette)
    {
        // 'first' is the run index of the next pixel to write.
        int first = 0;
        while (first < count && !this.IsComplete)
        {
            Span<TPixel> span = this.Take(count - first);
            int i = 0;

            if ((first & 1) != 0)
            {
                // The part starts on the low nibble of byte first / 2. AND 0xF keeps the low nibble.
                span[0] = palette[packed[first >> 1] & 0xF];
                i = 1;
            }

            // Pixel first + i is even, so it is the high nibble of byte (first + i) / 2.
            BmpPaletteExpander.Expand<TPixel, BmpPaletteExpander.Index4Operator>(packed[((first + i) >> 1)..], palette, span[i..]);
            first += span.Length;
        }
    }

    /// <summary>
    /// Writes one pixel for each stored B, G, R triplet.
    /// </summary>
    /// <param name="configuration">The configuration of the bulk pixel conversion.</param>
    /// <param name="bgr">The triplets.</param>
    public void WriteBgr24(Configuration configuration, scoped ReadOnlySpan<byte> bgr)
    {
        int count = bgr.Length / 3;
        while (count > 0 && !this.IsComplete)
        {
            Span<TPixel> span = this.Take(count);
            PixelOperations<TPixel>.Instance.FromBgr24Bytes(configuration, bgr, span, span.Length);
            bgr = bgr[(span.Length * 3)..];
            count -= span.Length;
        }
    }

    /// <summary>
    /// Writes <paramref name="count"/> pixels with the skipped pixel color.
    /// </summary>
    /// <param name="count">The pixel count.</param>
    public void Skip(long count) => this.Fill(this.skipped, count);

    /// <summary>
    /// Skips the rest of the current row. At the start of a row, it skips no pixels.
    /// </summary>
    public void EndOfLine()
    {
        if (this.x > 0)
        {
            this.Skip(this.width - this.x);
        }
    }

    /// <summary>
    /// Skips all pixels that are not written.
    /// </summary>
    public void EndOfBitmap()
    {
        while (!this.IsComplete)
        {
            this.Skip(this.width - this.x);
        }
    }

    /// <summary>
    /// Takes the next pixels of the current row, at most <paramref name="count"/>, and moves the position past them.
    /// </summary>
    /// <remarks>
    /// The callers take pixels only while <see cref="IsComplete"/> is false, so the position is inside the image.
    /// </remarks>
    /// <param name="count">The pixel count that the caller has to write. It is more than 0.</param>
    /// <returns>The pixels to write. The span has at least one pixel.</returns>
    private Span<TPixel> Take(long count)
    {
        int length = (int)Math.Min(count, this.width - this.x);
        Span<TPixel> span = this.row.Slice(this.x, length);

        this.x += length;
        if (this.x == this.width)
        {
            this.x = 0;
            this.y++;
            this.row = this.RowOf(this.y);
        }

        return span;
    }

    /// <summary>
    /// Gets the image row of a stored row.
    /// </summary>
    /// <param name="storedRow">The stored row index.</param>
    /// <returns>The image row, or an empty span when <paramref name="storedRow"/> is past the last row.</returns>
    private readonly Span<TPixel> RowOf(int storedRow)
    {
        if (storedRow >= this.height || this.width == 0)
        {
            return default;
        }

        // Stored row 0 is the bottom image row, unless the bitmap is inverted (stored top down).
        int imageRow = this.inverted ? storedRow : this.height - storedRow - 1;
        return this.pixels.DangerousGetRowSpan(imageRow);
    }
}
