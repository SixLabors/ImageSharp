// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

/// <summary>
/// Bitwriter for writing compressed CCITT T6 2D data.
/// </summary>
internal sealed class T6BitCompressor : TiffCcittCompressor
{
    /// <summary>
    /// Vertical codes from -3 to +3.
    /// </summary>
    private static readonly (uint Length, uint Code)[] VerticalCodes =
    [
        (7u, 3u),
        (6u, 3u),
        (3u, 3u),
        (1u, 1u),
        (3u, 2u),
        (6u, 2u),
        (7u, 2u)
    ];

    private IMemoryOwner<byte> referenceLineBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="T6BitCompressor"/> class.
    /// </summary>
    /// <param name="output">The output stream to write the compressed data.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    public T6BitCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel)
        : base(output, allocator, width, bitsPerPixel)
    {
    }

    /// <inheritdoc />
    public override TiffCompression Method => TiffCompression.CcittGroup4Fax;

    /// <inheritdoc/>
    protected override void EncodeStrip(ReadOnlySpan<byte> pixelsAsGray, int height)
    {
        CcittBitWriter writer = this.Writer;

        // Initial reference line is all white.
        Span<byte> referenceLine = this.referenceLineBuffer.GetSpan();
        referenceLine.Fill(0xff);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> row = pixelsAsGray.Slice(y * this.Width, this.Width);
            uint a0 = 0;
            uint a1 = row[0] == 0 ? 0 : FindRunEnd(row, 0);
            uint b1 = referenceLine[0] == 0 ? 0 : FindRunEnd(referenceLine, 0);

            while (true)
            {
                uint b2 = FindRunEnd(referenceLine, b1);
                if (b2 < a1)
                {
                    // Pass mode.
                    writer.WriteCode(1, 4);
                    a0 = b2;
                }
                else
                {
                    int d = int.MaxValue;
                    if ((b1 >= a1) && (b1 - a1 <= 3))
                    {
                        d = (int)(b1 - a1);
                    }
                    else if ((b1 < a1) && (a1 - b1 <= 3))
                    {
                        d = -(int)(a1 - b1);
                    }

                    if (d is >= -3 and <= 3)
                    {
                        // Vertical mode.
                        (uint length, uint code) = VerticalCodes[d + 3];
                        writer.WriteCode(code, (int)length);
                        a0 = a1;
                    }
                    else
                    {
                        // Horizontal mode.
                        writer.WriteCode(1, 3);

                        uint a2 = FindRunEnd(row, a1);
                        if ((a0 + a1 == 0) || (row[(int)a0] != 0))
                        {
                            writer.WriteRun((int)(a1 - a0), true);
                            writer.WriteRun((int)(a2 - a1), false);
                        }
                        else
                        {
                            writer.WriteRun((int)(a1 - a0), false);
                            writer.WriteRun((int)(a2 - a1), true);
                        }

                        a0 = a2;
                    }
                }

                if (a0 >= row.Length)
                {
                    break;
                }

                byte thisPixel = row[(int)a0];
                a1 = FindRunEnd(row, a0, thisPixel);
                b1 = FindRunEnd(referenceLine, a0, (byte)~thisPixel);
                b1 = FindRunEnd(referenceLine, b1, thisPixel);
            }

            // This row is now the reference line.
            row.CopyTo(referenceLine);
        }

        // EOFB: two EOL codes end the data.
        writer.WriteEol();
        writer.WriteEol();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.referenceLineBuffer?.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Finds the end of a pixel run.
    /// </summary>
    /// <param name="row">The row of pixels to examine.</param>
    /// <param name="startIndex">The index of the first pixel in <paramref name="row"/> to examine.</param>
    /// <param name="color">Color of pixels in the run.  If not specified, the color at
    /// <paramref name="startIndex"/> will be used.</param>
    /// <returns>The index of the first pixel at or after <paramref name="startIndex"/>
    /// that does not match <paramref name="color"/>, or the length of <paramref name="row"/>,
    /// whichever comes first.</returns>
    private static uint FindRunEnd(ReadOnlySpan<byte> row, uint startIndex, byte? color = null)
    {
        if (startIndex >= row.Length)
        {
            return (uint)row.Length;
        }

        // The vectorized search finds the first pixel of another color.
        ReadOnlySpan<byte> rest = row[(int)startIndex..];
        int end = rest.IndexOfAnyExcept(color ?? rest[0]);
        return end < 0 ? (uint)row.Length : startIndex + (uint)end;
    }

    /// <inheritdoc />
    public override void Initialize(int rowsPerStrip)
    {
        base.Initialize(rowsPerStrip);
        this.referenceLineBuffer = this.Allocator.Allocate<byte>(this.Width);
    }
}
