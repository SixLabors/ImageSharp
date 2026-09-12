// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless;

/// <summary>
/// Simple MSB-first bit-stream writer implementation built on top
/// of a stream.
/// </summary>
/// <param name="stream">Output bytes are written here.</param>
internal sealed class FjxlBitWriter(Stream stream) : IDisposable
{
    /// <summary>
    /// Temporary cache used to store pending written bits
    /// before they're written to the output stream.
    /// </summary>
    private ulong buffer;

    /// <summary>
    /// Gets the total number of bytes written to the output buffer so far.
    /// </summary>
    public long BytesWritten { get; private set; }

    /// <summary>
    /// Gets the number of bits actively in the bit cache.
    /// This is used to track how many bits were written into
    /// the cache prior to sending the cache to the stream.
    /// </summary>
    public int BitsInBuffer { get; private set; }

    /// <summary>
    /// Writes the specified bits in the Most Significant Byte (MSB)
    /// order.
    /// </summary>
    /// <param name="count">Represents the number of bits to write to the bit-stream.</param>
    /// <param name="bits">Represents the value to write to the bit-stream.</param>
    public void Write(int count, ulong bits)
    {
        DebugGuard.MustBeBetweenOrEqualTo(count, 0, 56, nameof(count));

        if (count < 64)
        {
            bits &= (1UL << count) - 1;
        }

        this.buffer |= bits << this.BitsInBuffer;
        this.BitsInBuffer += count;

        this.FlushBytes();
    }

    /// <summary>
    /// Internal method used to flush bytes from the cache
    /// (<see cref="buffer"/>) into the output stream.
    /// </summary>
    private void FlushBytes()
    {
        int bytes = this.BitsInBuffer / 8;

        for (int i = 0; i < bytes; i++)
        {
            stream.WriteByte((byte)this.buffer);
            this.BytesWritten++;
            this.buffer >>= 8;
        }

        this.BitsInBuffer -= bytes * 8;
    }

    /// <summary>
    /// Used by the dispose method to flush the remaining bits
    /// that are not byte-aligned. F.e. if we dispose this reader
    /// and we have 5 bits left, those final 5 bits are set to all 0
    /// and the byte is written to the stream.
    /// </summary>
    public void ZeroPadToByte()
    {
        if (this.BitsInBuffer != 0)
        {
            this.Write(8 - this.BitsInBuffer, 0);
        }
    }

    /// <summary>
    /// Flushes out the final bytes.
    /// </summary>
    public void Dispose() => this.ZeroPadToByte();
}
