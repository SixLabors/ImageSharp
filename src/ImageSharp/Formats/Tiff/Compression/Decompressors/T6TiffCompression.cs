// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using CCITT T6 compression.
/// </summary>
internal sealed class T6TiffCompression : TiffBaseDecompressor
{
    private readonly bool isWhiteZero;

    private readonly int width;

    private readonly byte white;

    /// <summary>
    /// Initializes a new instance of the <see cref="T6TiffCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="fillOrder">The logical order of bits within a byte.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="photometricInterpretation">The photometric interpretation.</param>
    public T6TiffCompression(
        MemoryAllocator allocator,
        TiffFillOrder fillOrder,
        int width,
        int bitsPerPixel,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(allocator, width, bitsPerPixel)
    {
        this.FillOrder = fillOrder;
        this.width = width;
        this.isWhiteZero = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero;
        this.white = (byte)(this.isWhiteZero ? 0 : 255);
    }

    /// <summary>
    /// Gets the logical order of bits within a byte.
    /// </summary>
    private TiffFillOrder FillOrder { get; }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        int height = stripHeight;
        buffer.Clear();

        using System.Buffers.IMemoryOwner<byte> scanLineBuffer = this.Allocator.Allocate<byte>(this.width * 2);
        Span<byte> scanLine = scanLineBuffer.GetSpan()[..this.width];
        Span<byte> referenceScanLineSpan = scanLineBuffer.GetSpan().Slice(this.width, this.width);

        T6BitReader? bitReader = new T6BitReader(stream, this.FillOrder, byteCount);

        CcittReferenceScanline referenceScanLine = new CcittReferenceScanline(this.isWhiteZero, this.width);
        nint bitsWritten = 0;
        for (int y = 0; y < height; y++)
        {
            scanLine.Clear();
            Decode2DScanline(bitReader, this.isWhiteZero, referenceScanLine, scanLine);

            bitsWritten = this.WriteScanLine(buffer, scanLine, bitsWritten);

            scanLine.CopyTo(referenceScanLineSpan);
            referenceScanLine = new CcittReferenceScanline(this.isWhiteZero, referenceScanLineSpan);
        }
    }

    private nint WriteScanLine(Span<byte> buffer, Span<byte> scanLine, nint bitsWritten)
    {
        nint bitPos = Numerics.Modulo8(bitsWritten);
        nint bufferPos = bitsWritten / 8;
        ref byte scanLineRef = ref MemoryMarshal.GetReference(scanLine);
        for (nuint i = 0; i < (uint)scanLine.Length; i++)
        {
            if (Unsafe.Add(ref scanLineRef, i) != this.white)
            {
                BitWriterUtils.WriteBit(buffer, bufferPos, bitPos);
            }

            bitPos++;
            bitsWritten++;

            if (bitPos >= 8)
            {
                bitPos = 0;
                bufferPos++;
            }
        }

        // Write padding bytes, if necessary.
        nint remainder = Numerics.Modulo8(bitsWritten);
        if (remainder != 0)
        {
            nint padding = 8 - remainder;
            BitWriterUtils.WriteBits(buffer, bitsWritten, padding, 0);
            bitsWritten += padding;
        }

        return bitsWritten;
    }

    private static void Decode2DScanline(T6BitReader bitReader, bool whiteIsZero, CcittReferenceScanline referenceScanline, Span<byte> scanline)
    {
        int width = scanline.Length;
        bitReader.StartNewRow();

        // 2D Encoding variables.
        int a0 = -1;
        byte fillByte = whiteIsZero ? (byte)0 : (byte)255;

        // Process every code word in this scanline.
        int unpacked = 0;
        while (true)
        {
            // Read next code word and advance pass it.
            bool isEol = bitReader.ReadNextCodeWord();

            // Special case handling for EOL.
            if (isEol)
            {
                // If a TIFF reader encounters EOFB before the expected number of lines has been extracted,
                // it is appropriate to assume that the missing rows consist entirely of white pixels.
                if (whiteIsZero)
                {
                    scanline.Clear();
                }
                else
                {
                    scanline.Fill(255);
                }

                break;
            }

            // Update 2D Encoding variables.
            int b1 = referenceScanline.FindB1(a0, fillByte);

            // Switch on the code word.
            int a1;
            switch (bitReader.Code.Type)
            {
                case CcittTwoDimensionalCodeType.None:
                    TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error, could not read a valid code word.");
                    break;

                case CcittTwoDimensionalCodeType.Pass:
                    int b2 = referenceScanline.FindB2(b1);
                    scanline[unpacked..b2].Fill(fillByte);
                    unpacked = b2;
                    a0 = b2;
                    break;
                case CcittTwoDimensionalCodeType.Horizontal:
                    // Decode M(a0a1)
                    bitReader.ReadNextRun();
                    int runLength = (int)bitReader.RunLength;
                    if (runLength > (uint)(scanline.Length - unpacked))
                    {
                        TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error");
                    }

                    scanline.Slice(unpacked, runLength).Fill(fillByte);
                    unpacked += runLength;
                    fillByte = (byte)~fillByte;

                    // Decode M(a1a2)
                    bitReader.ReadNextRun();
                    runLength = (int)bitReader.RunLength;
                    if (runLength > (uint)(scanline.Length - unpacked))
                    {
                        TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error");
                    }

                    scanline.Slice(unpacked, runLength).Fill(fillByte);
                    unpacked += runLength;
                    fillByte = (byte)~fillByte;

                    // Prepare next a0
                    a0 = unpacked;
                    break;

                case CcittTwoDimensionalCodeType.Vertical0:
                    a1 = b1;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalR1:
                    a1 = b1 + 1;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalR2:
                    a1 = b1 + 2;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalR3:
                    a1 = b1 + 3;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalL1:
                    a1 = b1 - 1;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalL2:
                    a1 = b1 - 2;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                case CcittTwoDimensionalCodeType.VerticalL3:
                    a1 = b1 - 3;
                    scanline[unpacked..a1].Fill(fillByte);
                    unpacked = a1;
                    a0 = a1;
                    fillByte = (byte)~fillByte;
                    bitReader.SwapColor();
                    break;

                default:
                    throw new NotSupportedException("ccitt extensions are not supported.");
            }

            // This line is fully unpacked. Should exit and process next line.
            if (unpacked == width)
            {
                break;
            }

            if (unpacked > width)
            {
                TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error, unpacked data > width");
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
