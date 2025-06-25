// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
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

    /// <summary>
    /// Writes a image compressed with CCITT T6 to the output buffer.
    /// </summary>
    /// <param name="pixelsAsGray">The pixels as 8-bit gray array.</param>
    /// <param name="height">The strip height.</param>
    /// <param name="compressedData">The destination for the compressed data.</param>
    protected override void CompressStrip(Span<byte> pixelsAsGray, int height, Span<byte> compressedData)
    {
        // Initial reference line is all white.
        Span<byte> referenceLine = this.referenceLineBuffer.GetSpan();
        referenceLine.Fill(0xff);

        for (int y = 0; y < height; y++)
        {
            Span<byte> row = pixelsAsGray.Slice(y * this.Width, this.Width);
            uint a0 = 0;
            uint a1 = row[0] == 0 ? 0 : FindRunEnd(row, 0);
            uint b1 = referenceLine[0] == 0 ? 0 : FindRunEnd(referenceLine, 0);

            while (true)
            {
                uint b2 = FindRunEnd(referenceLine, b1);
                if (b2 < a1)
                {
                    // Pass mode.
                    this.WriteCode(4, 1, compressedData);
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
                        this.WriteCode(length, code, compressedData);
                        a0 = a1;
                    }
                    else
                    {
                        // Horizontal mode.
                        this.WriteCode(3, 1, compressedData);

                        uint a2 = FindRunEnd(row, a1);
                        if ((a0 + a1 == 0) || (row[(int)a0] != 0))
                        {
                            this.WriteRun(a1 - a0, true, compressedData);
                            this.WriteRun(a2 - a1, false, compressedData);
                        }
                        else
                        {
                            this.WriteRun(a1 - a0, false, compressedData);
                            this.WriteRun(a2 - a1, true, compressedData);
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

        this.WriteCode(12, 1, compressedData);
        this.WriteCode(12, 1, compressedData);
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
    private static uint FindRunEnd(Span<byte> row, uint startIndex, byte? color = null)
    {
        if (startIndex >= row.Length)
        {
            return (uint)row.Length;
        }

        byte colorValue = color ?? row[(int)startIndex];
        for (int i = (int)startIndex; i < row.Length; i++)
        {
            if (row[i] != colorValue)
            {
                return (uint)i;
            }
        }

        return (uint)row.Length;
    }

    /// <inheritdoc />
    public override void Initialize(int rowsPerStrip)
    {
        base.Initialize(rowsPerStrip);
        this.referenceLineBuffer = this.Allocator.Allocate<byte>(this.Width);
    }

    /// <summary>
    /// Writes a run to the output buffer.
    /// </summary>
    /// <param name="runLength">The length of the run.</param>
    /// <param name="isWhiteRun">If <c>true</c> the run is white pixels,
    /// if <c>false</c> the run is black pixels.</param>
    /// <param name="compressedData">The destination to write the run to.</param>
    private void WriteRun(uint runLength, bool isWhiteRun, Span<byte> compressedData)
    {
        uint code;
        uint codeLength;
        while (runLength > 63)
        {
            uint makeupLength = GetBestFittingMakeupRunLength(runLength);
            code = GetMakeupCode(makeupLength, out codeLength, isWhiteRun);
            this.WriteCode(codeLength, code, compressedData);
            runLength -= makeupLength;
        }

        code = GetTermCode(runLength, out codeLength, isWhiteRun);
        this.WriteCode(codeLength, code, compressedData);
    }
}
