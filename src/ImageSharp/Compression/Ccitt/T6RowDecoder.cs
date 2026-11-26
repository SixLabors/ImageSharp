// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Decodes rows of 1-bit pixels from data in the two-dimensional coding scheme of ITU-T T.6.
/// </summary>
/// <remarks>
/// The encoder codes each row with the changes from the row above it, and the first row with the changes from an imaginary white row. See ITU-T Recommendation
/// T.6 (11/88), section 2.2.1, page 2: https://www.itu.int/rec/T-REC-T.6
/// <para>
/// A row bit is 1 for a black pixel of the code and 0 for a white pixel. The photometric interpretation of the format gives the displayed colors. For example,
/// TIFF reverses the meaning of white and black for BlackIsZero data. See TIFF 6.0 specification, section 10, page 44:
/// https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
/// </para>
/// <para>
/// In the Strict and IgnoreAncillary modes, the decoder throws for damaged data and for data that ends before a row. In the IgnoreImageData mode, the rows keep
/// their decoded pixels.
/// </para>
/// </remarks>
internal sealed class T6RowDecoder : IDisposable
{
    /// <summary>
    /// The message for damaged data.
    /// </summary>
    private const string DamagedMessage = "The CCITT T.6 data is damaged or ends before the last row of the strip, tile or image.";

    private CcittBitReader reader;

    private readonly int width;

    private readonly SegmentIntegrityHandling integrityHandling;

    /// <summary>
    /// Two rows of one byte for each pixel: the row that is decoded and the reference row above it.
    /// </summary>
    private readonly IMemoryOwner<byte> scanLines;

    /// <summary>
    /// The index (0 or 1) of the row in <see cref="scanLines"/> that the next row decodes into.
    /// </summary>
    private int current;

    private bool isFirstRow = true;

    /// <summary>
    /// Whether the data had an EOL code. In T.6 data, an EOL code is the start of EOFB, the end of the data. TIFF 6.0
    /// lets a reader treat the rows after an early EOFB as white, so the rows after it are not damaged. See TIFF 6.0
    /// specification, section 11, page 53: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
    /// </summary>
    private bool endOfBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="T6RowDecoder"/> class.
    /// </summary>
    /// <param name="stream">The compressed input stream.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="byteCount">The number of bytes to read from the stream.</param>
    /// <param name="width">The number of pixels in each row.</param>
    /// <param name="allocator">The memory allocator for the decoded and the reference row.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode. It selects what happens with damaged data.</param>
    public T6RowDecoder(
        BufferedReadStream stream,
        CcittBitOrder bitOrder,
        int byteCount,
        int width,
        MemoryAllocator allocator,
        SegmentIntegrityHandling integrityHandling)
    {
        this.reader = new CcittBitReader(stream, bitOrder, byteCount);
        this.width = width;
        this.integrityHandling = integrityHandling;
        this.scanLines = allocator.Allocate<byte>(width * 2);
    }

    /// <summary>
    /// Gets the mode codes of ITU-T Recommendation T.6 (11/88), Table 1/T.6, page 4. The index is the next 7 code bits,
    /// and each index starts with one mode code. Each entry is (<see cref="CcittTwoDimensionalCodeType"/> &lt;&lt; 3) |
    /// length.
    /// </summary>
    private static ReadOnlySpan<byte> ModeTable =>
    [
        87, 95, 79, 55, 70, 70, 46, 46, 12, 12, 12, 12, 12, 12, 12, 12,
        19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19,
        59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59, 59,
        35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35, 35,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25
    ];

    /// <summary>
    /// Decodes the next row. The first pixel goes to the most significant bit of the first byte.
    /// </summary>
    /// <param name="row">The destination row. It holds at least (width + 7) / 8 bytes, and all of its bits are 0.</param>
    public void DecodeRow(Span<byte> row)
    {
        Span<byte> scanLines = this.scanLines.GetSpan();
        Span<byte> scanLine = scanLines.Slice(this.current * this.width, this.width);

        // The row above is the reference row. The first row has the imaginary white row as its reference.
        CcittReferenceScanline referenceScanLine = this.isFirstRow
            ? new CcittReferenceScanline(this.width)
            : new CcittReferenceScanline(scanLines.Slice((this.current ^ 1) * this.width, this.width));

        // The decoded row holds one byte for each pixel: 0 for white and 1 for black. It starts white.
        scanLine.Clear();
        if (!Decode2DScanline(ref this.reader, referenceScanLine, scanLine, ref this.endOfBlock) && !this.endOfBlock)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(DamagedMessage);
        }

        // Each pixel byte is its bit, so the decoded row is a row of 1-bit indices. A black pixel is a 1 bit.
        EncodingUtilities.PackIndices(scanLine, row, 1);

        // The decoded row is the reference row of the next row, so the two rows change places without a copy.
        this.current ^= 1;
        this.isFirstRow = false;
    }

    /// <inheritdoc/>
    public void Dispose() => this.scanLines.Dispose();

    /// <summary>
    /// Decodes one row of two-dimensional codes into one byte for each pixel.
    /// </summary>
    /// <remarks>
    /// T.6 data has no code that starts a new row, so the decoder cannot find the next row after a code that is not
    /// valid. Such a code, a run longer than the row, or a changing element outside the row ends the data. The pixels
    /// decoded before it stay, and the rows after it are white, as after EOFB.
    /// </remarks>
    /// <param name="reader">The bit reader.</param>
    /// <param name="referenceScanline">The reference row.</param>
    /// <param name="scanline">The destination row. It is white: all bytes are 0.</param>
    /// <param name="endOfBlock">Set to <see langword="true"/> when the row ends at an EOL code.</param>
    /// <returns>
    /// <see langword="true"/> if the row has all of its pixels, or if the row ends at an EOL code. Else
    /// <see langword="false"/>: the data ends before the row is complete, or has a code that the decoder cannot use.
    /// </returns>
    private static bool Decode2DScanline(ref CcittBitReader reader, CcittReferenceScanline referenceScanline, Span<byte> scanline, ref bool endOfBlock)
    {
        int width = scanline.Length;

        // 2D decoding variables. A white pixel is 0 and a black pixel is 1.
        int a0 = -1;
        const byte white = 0;
        byte fillByte = white;

        // Process every code word in this scanline.
        int unpacked = 0;
        while (unpacked < width)
        {
            // After the end of the data, the rest of the row stays white, and the row is not complete.
            if (!reader.HasData)
            {
                return false;
            }

            // The mode codes have 7 bits or less, so the next 7 bits select one code.
            reader.EnsureBits();
            int mode = ModeTable[(int)reader.Peek(7)];
            CcittTwoDimensionalCodeType type = (CcittTwoDimensionalCodeType)(mode >> 3);

            // The extension codes start with 6 zero bits, as an EOL code (11 zero bits, then a 1 bit) does. EOFB is two
            // EOL codes at the end of the data.
            if (type is CcittTwoDimensionalCodeType.Extensions1D or CcittTwoDimensionalCodeType.Extensions2D)
            {
                // The decoder does not support the extensions, so an extension code ends the data. An EOL code ends only
                // the row. The reader skips it, because it can have more zero bits than the window holds.
                if (reader.LeadingZeros() < 11 || !reader.SkipToEol())
                {
                    reader.End();
                    return false;
                }

                // TIFF 6.0 lets a reader treat the rows after an early EOFB as white. See TIFF 6.0 specification,
                // section 11, page 53: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
                // The pixels of this row before the code stay, and the rest of the row stays white.
                endOfBlock = true;
                return true;
            }

            // A code that ends after the last data bit is not complete, so the data ends here.
            int length = mode & 7;
            if (!reader.HasBits(length))
            {
                reader.End();
                return false;
            }

            reader.Consume(length);

            // Update the 2D decoding variables.
            int b1 = referenceScanline.FindB1(a0, fillByte);

            // a1 is the next changing element: the end of the run of fillByte that starts at a0.
            int a1;
            switch (type)
            {
                case CcittTwoDimensionalCodeType.Pass:
                    // The run continues to b2, and a0 moves to b2. The color does not change.
                    // b2 is after b1, and b1 is after a0, so b2 is in the row after the decoded pixels.
                    int b2 = referenceScanline.FindB2(b1);
                    scanline[unpacked..b2].Fill(fillByte);
                    unpacked = b2;
                    a0 = b2;
                    continue;

                case CcittTwoDimensionalCodeType.Horizontal:
                    // Two runs follow: M(a0a1) has the color of a0, and M(a1a2) has the other color.
                    for (int i = 0; i < 2; i++)
                    {
                        int run = reader.ReadRun(fillByte == white, width - unpacked);
                        if (run < 0)
                        {
                            reader.End();
                            return false;
                        }

                        scanline.Slice(unpacked, run).Fill(fillByte);
                        unpacked += run;
                        fillByte ^= 1;
                    }

                    a0 = unpacked;
                    continue;

                // The vertical modes give a1 relative to b1, from 3 pixels to the left to 3 pixels to the right.
                case CcittTwoDimensionalCodeType.Vertical0:
                    a1 = b1;
                    break;
                case CcittTwoDimensionalCodeType.VerticalR1:
                    a1 = b1 + 1;
                    break;
                case CcittTwoDimensionalCodeType.VerticalR2:
                    a1 = b1 + 2;
                    break;
                case CcittTwoDimensionalCodeType.VerticalR3:
                    a1 = b1 + 3;
                    break;
                case CcittTwoDimensionalCodeType.VerticalL1:
                    a1 = b1 - 1;
                    break;
                case CcittTwoDimensionalCodeType.VerticalL2:
                    a1 = b1 - 2;
                    break;
                default:
                    // The mode table holds no other code type, so this is VerticalL3.
                    a1 = b1 - 3;
                    break;
            }

            if (a1 < unpacked || a1 > width)
            {
                reader.End();
                return false;
            }

            scanline[unpacked..a1].Fill(fillByte);
            unpacked = a1;
            a0 = a1;
            fillByte ^= 1;
        }

        return true;
    }
}
