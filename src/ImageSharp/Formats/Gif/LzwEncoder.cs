// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Encodes and compresses the image data using dynamic Lempel-Ziv compression.
/// </summary>
internal ref struct LzwEncoder
{
    private readonly int minCodeSize;
    private readonly int clearCode;
    private readonly int eoiCode;
    private readonly int codeMask;
    private int nextCode;
    private int curCodeSize;
    private int curShift;
    private int cur;
    private nint position;
    private readonly MemoryAllocator memoryAllocator;
    private readonly IMemoryOwner<byte> bufferOwner;
    private readonly Span<byte> buffer;
    private const int MaxStackSize = 1 << 12;
    private const int SubBlockLength = 255;

    public LzwEncoder(MemoryAllocator memoryAllocator, int colorDepth)
    {
        this.memoryAllocator = memoryAllocator;
        this.minCodeSize = Math.Max(2, colorDepth);
        this.clearCode = 1 << this.minCodeSize;
        this.codeMask = this.clearCode - 1;
        this.eoiCode = this.clearCode + 1;
        this.nextCode = this.eoiCode + 1;

        this.curCodeSize = this.minCodeSize + 1;
        this.curShift = 0;
        this.cur = 0;

        this.bufferOwner = memoryAllocator.Allocate<byte>(SubBlockLength);
        this.buffer = this.bufferOwner.GetSpan();
        this.position = 0;
    }

    public void Encode(Buffer2D<byte> indexedPixels, Stream stream)
    {
        // Write "initial code size" byte
        stream.WriteByte((byte)this.minCodeSize);

        this.position = 0; // Pointing at the length field.

        // Compress and write the pixel data
        this.Compress(indexedPixels, stream);

        // Write block terminator
        stream.WriteByte(GifConstants.Terminator);
    }

    private void Compress(Buffer2D<byte> indexedPixels, Stream stream)
    {
        // Output code for the current contents of the index buffer.
        int pixel = indexedPixels[0, 0] & this.codeMask;  // Load first input index.
        using HashLut codeTable = new(this.memoryAllocator, MaxStackSize);  // Key'd on our 20-bit "tuple".

        this.EmitCode(this.clearCode, stream);  // Spec says first code should be a clear code.

        for (int y = 0; y < indexedPixels.Height; y++)
        {
            // First index already loaded, process the rest of the stream.
            int offsetX = y == 0 ? 1 : 0;
            Span<byte> row = indexedPixels.DangerousGetRowSpan(y);
            for (int x = offsetX; x < indexedPixels.Width; x++)
            {
                int k = row[x] & this.codeMask;
                int cur_key = (pixel << 8) | k;  // (prev, k) unique tuple.

                // buffer + k.
                if (!codeTable.TryGetValue(cur_key, out int cur_code))
                {
                    // We don't have buffer + k.
                    // Emit index buffer (without k).
                    this.EmitCode(pixel, stream);

                    if (this.nextCode == codeTable.Capacity)
                    {
                        // We've filled up the code table, so we need to emit a clear code.
                        this.EmitCode(this.clearCode, stream);
                        this.nextCode = this.eoiCode + 1;
                        this.curCodeSize = this.minCodeSize + 1;
                        codeTable.Clear();
                    }
                    else
                    {
                        // Table not full, insert a new entry.
                        // Increase our variable bit code sizes if necessary.  This is a bit
                        // tricky as it is based on "timing" between the encoding and
                        // decoder.  From the encoders perspective this should happen after
                        // we've already emitted the index buffer and are about to create the
                        // first table entry that would overflow our current code bit size.
                        if (this.nextCode >= (1 << this.curCodeSize))
                        {
                            this.curCodeSize++;
                        }

                        codeTable.SetValue(cur_key, this.nextCode++);  // Insert into code table.
                    }

                    pixel = k;  // Index buffer to single input k.
                }
                else
                {
                    pixel = cur_code;  // Index buffer to sequence in code table.
                }
            }
        }

        this.EmitCode(pixel, stream);  // There will still be something in the index buffer.
        this.EmitCode(this.eoiCode, stream);  // End Of Information.

        // Flush / finalize the sub-blocks stream to the buffer.
        this.EmitBytesToBuffer(1, stream);

        // Finish the sub-blocks, writing out any unfinished lengths.
        if (this.position > 0)
        {
            // Write out the last sub-block length.
            stream.WriteByte((byte)this.position);
            stream.Write(this.buffer, 0, (int)this.position);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitCode(int c, Stream stream)
    {
        this.cur |= c << this.curShift;
        this.curShift += this.curCodeSize;
        this.EmitBytesToBuffer(8, stream);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBytesToBuffer(int bit_block_size, Stream stream)
    {
        ref byte b = ref MemoryMarshal.GetReference(this.buffer);
        while (this.curShift >= bit_block_size)
        {
            Unsafe.Add(ref b, this.position++) = (byte)(this.cur & 0xFF);
            this.cur >>= 8;
            this.curShift -= 8;
            if (this.position == SubBlockLength)
            {
                // Finished a sub-block.
                stream.WriteByte(SubBlockLength);
                stream.Write(this.buffer);
                this.position = 0;
            }
        }
    }

    public void Dispose() => this.bufferOwner.Dispose();
}

internal ref struct HashLut
{
    private readonly IMemoryOwner<int> keysOwner;
    private readonly IMemoryOwner<int> valuesOwner;
    private readonly Span<int> keys;
    private readonly Span<int> values;
    private nint index;

    public HashLut(MemoryAllocator allocator, int capacity)
    {
        this.Capacity = capacity;
        this.keysOwner = allocator.Allocate<int>(capacity, AllocationOptions.Clean);
        this.valuesOwner = allocator.Allocate<int>(capacity);
        this.keys = this.keysOwner.GetSpan();
        this.keys.Fill(-1);
        this.values = this.valuesOwner.GetSpan();
        this.index = 0;
    }

    public int Capacity { get; }

    public bool TryGetValue(int key, out int value)
    {
        int index = this.keys.IndexOf(key);
        if (index != -1)
        {
            ref int v = ref MemoryMarshal.GetReference(this.values);
            value = Unsafe.Add(ref v, (uint)index);
            return true;
        }

        value = -1;
        return false;
    }

    public void SetValue(int key, int value)
    {
        if ((uint)this.index < (uint)this.Capacity)
        {
            ref int k = ref MemoryMarshal.GetReference(this.keys);
            ref int v = ref MemoryMarshal.GetReference(this.values);
            Unsafe.Add(ref k, this.index) = key;
            Unsafe.Add(ref v, this.index) = value;
            this.index++;
            return;
        }

        ThrowInvalid();
    }

    public void Clear()
    {
        this.index = 0;
        this.keys.Fill(-1);
    }

    public void Dispose()
    {
        this.keysOwner.Dispose();
        this.valuesOwner.Dispose();
    }

    private static void ThrowInvalid() => throw new InvalidOperationException("HashLut is full");
}
