// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Webp.Lossless;

namespace SixLabors.ImageSharp.Formats.Webp.BitWriter;

/// <summary>
/// A bit writer for writing lossless webp streams.
/// </summary>
internal class Vp8LBitWriter : BitWriterBase
{
    /// <summary>
    /// This is the minimum amount of size the memory buffer is guaranteed to grow when extra space is needed.
    /// </summary>
    private const int MinExtraSize = 32768;

    private const int WriterBytes = 4;

    private const int WriterBits = 32;

    /// <summary>
    /// Bit accumulator.
    /// </summary>
    private ulong bits;

    /// <summary>
    /// Number of bits used in accumulator.
    /// </summary>
    private int used;

    /// <summary>
    /// Current write position.
    /// </summary>
    private int cur;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
    /// </summary>
    /// <param name="expectedSize">The expected size in bytes.</param>
    public Vp8LBitWriter(int expectedSize)
        : base(expectedSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
    /// Used internally for cloning.
    /// </summary>
    private Vp8LBitWriter(byte[] buffer, ulong bits, int used, int cur)
        : base(buffer)
    {
        this.bits = bits;
        this.used = used;
        this.cur = cur;
    }

    /// <inheritdoc/>
    public override int NumBytes => this.cur + ((this.used + 7) >> 3);

    /// <summary>
    /// This function writes bits into bytes in increasing addresses (little endian),
    /// and within a byte least-significant-bit first. This function can write up to 32 bits in one go.
    /// </summary>
    public void PutBits(uint bits, int nBits)
    {
        if (nBits > 0)
        {
            if (this.used >= 32)
            {
                this.PutBitsFlushBits();
            }

            this.bits |= (ulong)bits << this.used;
            this.used += nBits;
        }
    }

    public void Reset(Vp8LBitWriter bwInit)
    {
        this.bits = bwInit.bits;
        this.used = bwInit.used;
        this.cur = bwInit.cur;
    }

    public void WriteHuffmanCode(HuffmanTreeCode code, int codeIndex)
    {
        int depth = code.CodeLengths[codeIndex];
        int symbol = code.Codes[codeIndex];
        this.PutBits((uint)symbol, depth);
    }

    public void WriteHuffmanCodeWithExtraBits(HuffmanTreeCode code, int codeIndex, int bits, int nBits)
    {
        int depth = code.CodeLengths[codeIndex];
        int symbol = code.Codes[codeIndex];
        this.PutBits((uint)((bits << depth) | symbol), depth + nBits);
    }

    public Vp8LBitWriter Clone()
    {
        byte[] clonedBuffer = new byte[this.Buffer.Length];
        System.Buffer.BlockCopy(this.Buffer, 0, clonedBuffer, 0, this.cur);
        return new Vp8LBitWriter(clonedBuffer, this.bits, this.used, this.cur);
    }

    /// <inheritdoc/>
    public override void Finish()
    {
        this.BitWriterResize((this.used + 7) >> 3);
        while (this.used > 0)
        {
            this.Buffer[this.cur++] = (byte)this.bits;
            this.bits >>= 8;
            this.used -= 8;
        }

        this.used = 0;
    }

    /// <inheritdoc />
    public override void WriteEncodedImageToStream(Stream stream)
    {
        uint size = (uint)this.NumBytes + 1; // One byte extra for the VP8L signature
        uint pad = size & 1;

        // Write magic bytes indicating its a lossless webp.
        Span<byte> scratchBuffer = stackalloc byte[WebpConstants.TagSize];
        BinaryPrimitives.WriteUInt32BigEndian(scratchBuffer, (uint)WebpChunkType.Vp8L);
        stream.Write(scratchBuffer);

        // Write Vp8 Header.
        BinaryPrimitives.WriteUInt32LittleEndian(scratchBuffer, size);
        stream.Write(scratchBuffer);
        stream.WriteByte(WebpConstants.Vp8LHeaderMagicByte);

        // Write the encoded bytes of the image to the stream.
        this.WriteToStream(stream);
        if (pad == 1)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Internal function for PutBits flushing 32 bits from the written state.
    /// </summary>
    private void PutBitsFlushBits()
    {
        // If needed, make some room by flushing some bits out.
        if (this.cur + WriterBytes > this.Buffer.Length)
        {
            int extraSize = this.Buffer.Length - this.cur + MinExtraSize;
            this.BitWriterResize(extraSize);
        }

        Span<byte> scratchBuffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(scratchBuffer, this.bits);
        scratchBuffer[..4].CopyTo(this.Buffer.AsSpan(this.cur));

        this.cur += WriterBytes;
        this.bits >>= WriterBits;
        this.used -= WriterBits;
    }

    /// <summary>
    /// Resizes the buffer to write to.
    /// </summary>
    /// <param name="extraSize">The extra size in bytes needed.</param>
    public override void BitWriterResize(int extraSize)
    {
        int maxBytes = this.Buffer.Length + this.Buffer.Length;
        int sizeRequired = this.cur + extraSize;
        this.ResizeBuffer(maxBytes, sizeRequired);
    }
}
