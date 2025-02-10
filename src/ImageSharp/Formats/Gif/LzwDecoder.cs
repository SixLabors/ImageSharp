// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Decompresses and decodes data using the dynamic LZW algorithms.
/// </summary>
internal sealed class LzwDecoder : IDisposable
{
    /// <summary>
    /// The max decoder pixel stack size.
    /// </summary>
    private const int MaxStackSize = 4096;

    /// <summary>
    /// The maximum bits for a lzw code.
    /// </summary>
    private const int MaximumLzwBits = 12;

    /// <summary>
    /// The null code.
    /// </summary>
    private const int NullCode = -1;

    /// <summary>
    /// The stream to decode.
    /// </summary>
    private readonly BufferedReadStream stream;

    /// <summary>
    /// The prefix buffer.
    /// </summary>
    private readonly IMemoryOwner<int> prefixOwner;

    /// <summary>
    /// The suffix buffer.
    /// </summary>
    private readonly IMemoryOwner<int> suffixOwner;

    /// <summary>
    /// The scratch buffer for reading data blocks.
    /// </summary>
    private readonly IMemoryOwner<byte> bufferOwner;

    /// <summary>
    /// The pixel stack buffer.
    /// </summary>
    private readonly IMemoryOwner<int> pixelStackOwner;
    private readonly int minCodeSize;
    private readonly int clearCode;
    private readonly int endCode;
    private int code;
    private int codeSize;
    private int codeMask;
    private int availableCode;
    private int oldCode = NullCode;
    private int bits;
    private int top;
    private int count;
    private int bufferIndex;
    private int data;
    private int first;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzwDecoder"/> class
    /// and sets the stream, where the compressed data should be read from.
    /// </summary>
    /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="minCodeSize">The minimum code size.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public LzwDecoder(MemoryAllocator memoryAllocator, BufferedReadStream stream, int minCodeSize)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Guard.IsTrue(IsValidMinCodeSize(minCodeSize), nameof(minCodeSize), "Invalid minimum code size.");

        this.prefixOwner = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
        this.suffixOwner = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
        this.pixelStackOwner = memoryAllocator.Allocate<int>(MaxStackSize + 1, AllocationOptions.Clean);
        this.bufferOwner = memoryAllocator.Allocate<byte>(byte.MaxValue, AllocationOptions.None);
        this.minCodeSize = minCodeSize;

        // Calculate the clear code. The value of the clear code is 2 ^ minCodeSize
        this.clearCode = 1 << minCodeSize;
        this.codeSize = minCodeSize + 1;
        this.codeMask = (1 << this.codeSize) - 1;
        this.endCode = this.clearCode + 1;
        this.availableCode = this.clearCode + 2;

        // Fill the suffix buffer with the initial values represented by the number of colors.
        Span<int> suffix = this.suffixOwner.GetSpan()[..this.clearCode];
        int i;
        for (i = 0; i < suffix.Length; i++)
        {
            suffix[i] = i;
        }

        this.code = i;
    }

    /// <summary>
    /// Gets a value indicating whether the minimum code size is valid.
    /// </summary>
    /// <param name="minCodeSize">The minimum code size.</param>
    /// <returns>
    /// <see langword="true"/> if the minimum code size is valid; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsValidMinCodeSize(int minCodeSize)
    {
        // It is possible to specify a larger LZW minimum code size than the palette length in bits
        // which may leave a gap in the codes where no colors are assigned.
        // http://www.matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp#lzw_compression
        if (minCodeSize < 2 || minCodeSize > MaximumLzwBits || 1 << minCodeSize > MaxStackSize)
        {
            // Don't attempt to decode the frame indices.
            // Theoretically we could determine a min code size from the length of the provided
            // color palette but we won't bother since the image is most likely corrupted.
            return false;
        }

        return true;
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices for a single row from the stream, assigning the pixel values to the buffer.
    /// </summary>
    /// <param name="indices">The pixel indices array to decode to.</param>
    public void DecodePixelRow(Span<byte> indices)
    {
        indices.Clear();

        // Get span values from the owners.
        Span<int> prefix = this.prefixOwner.GetSpan();
        Span<int> suffix = this.suffixOwner.GetSpan();
        Span<int> pixelStack = this.pixelStackOwner.GetSpan();
        Span<byte> buffer = this.bufferOwner.GetSpan();

        // Cache frequently accessed instance fields into locals.
        // This helps avoid repeated field loads inside the tight loop.
        BufferedReadStream stream = this.stream;
        int top = this.top;
        int bits = this.bits;
        int codeSize = this.codeSize;
        int codeMask = this.codeMask;
        int minCodeSize = this.minCodeSize;
        int availableCode = this.availableCode;
        int oldCode = this.oldCode;
        int first = this.first;
        int data = this.data;
        int count = this.count;
        int bufferIndex = this.bufferIndex;
        int code = this.code;
        int clearCode = this.clearCode;
        int endCode = this.endCode;

        int i = 0;
        while (i < indices.Length)
        {
            if (top == 0)
            {
                if (bits < codeSize)
                {
                    // Load bytes until there are enough bits for a code.
                    if (count == 0)
                    {
                        // Read a new data block.
                        count = ReadBlock(stream, buffer);
                        if (count == 0)
                        {
                            break;
                        }

                        bufferIndex = 0;
                    }

                    data += buffer[bufferIndex] << bits;
                    bits += 8;
                    bufferIndex++;
                    count--;
                    continue;
                }

                // Get the next code
                code = data & codeMask;
                data >>= codeSize;
                bits -= codeSize;

                // Interpret the code
                if (code > availableCode || code == endCode)
                {
                    break;
                }

                if (code == clearCode)
                {
                    // Reset the decoder
                    codeSize = minCodeSize + 1;
                    codeMask = (1 << codeSize) - 1;
                    availableCode = clearCode + 2;
                    oldCode = NullCode;
                    continue;
                }

                if (oldCode == NullCode)
                {
                    pixelStack[top++] = suffix[code];
                    oldCode = code;
                    first = code;
                    continue;
                }

                int inCode = code;
                if (code == availableCode)
                {
                    pixelStack[top++] = first;
                    code = oldCode;
                }

                while (code > clearCode && top < MaxStackSize)
                {
                    pixelStack[top++] = suffix[code];
                    code = prefix[code];
                }

                int suffixCode = suffix[code];
                first = suffixCode;
                pixelStack[top++] = suffixCode;

                // Fix for GIFs that have "deferred clear code" as per:
                // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                if (availableCode < MaxStackSize)
                {
                    prefix[availableCode] = oldCode;
                    suffix[availableCode] = first;
                    availableCode++;
                    if (availableCode == codeMask + 1 && availableCode < MaxStackSize)
                    {
                        codeSize++;
                        codeMask = (1 << codeSize) - 1;
                    }
                }

                oldCode = inCode;
            }

            // Pop a pixel off the pixel stack.
            top--;

            // Clear missing pixels.
            indices[i++] = (byte)pixelStack[top];
        }

        // Write back the local values to the instance fields.
        this.top = top;
        this.bits = bits;
        this.codeSize = codeSize;
        this.codeMask = codeMask;
        this.availableCode = availableCode;
        this.oldCode = oldCode;
        this.first = first;
        this.data = data;
        this.count = count;
        this.bufferIndex = bufferIndex;
        this.code = code;
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices from the stream allowing skipping of the data.
    /// </summary>
    /// <param name="length">The resulting index table length.</param>
    public void SkipIndices(int length)
    {
        // Get span values from the owners.
        Span<int> prefix = this.prefixOwner.GetSpan();
        Span<int> suffix = this.suffixOwner.GetSpan();
        Span<int> pixelStack = this.pixelStackOwner.GetSpan();
        Span<byte> buffer = this.bufferOwner.GetSpan();

        // Cache frequently accessed instance fields into locals.
        // This helps avoid repeated field loads inside the tight loop.
        BufferedReadStream stream = this.stream;
        int top = this.top;
        int bits = this.bits;
        int codeSize = this.codeSize;
        int codeMask = this.codeMask;
        int minCodeSize = this.minCodeSize;
        int availableCode = this.availableCode;
        int oldCode = this.oldCode;
        int first = this.first;
        int data = this.data;
        int count = this.count;
        int bufferIndex = this.bufferIndex;
        int code = this.code;
        int clearCode = this.clearCode;
        int endCode = this.endCode;

        int i = 0;
        while (i < length)
        {
            if (top == 0)
            {
                if (bits < codeSize)
                {
                    // Load bytes until there are enough bits for a code.
                    if (count == 0)
                    {
                        // Read a new data block.
                        count = ReadBlock(stream, buffer);
                        if (count == 0)
                        {
                            break;
                        }

                        bufferIndex = 0;
                    }

                    data += buffer[bufferIndex] << bits;
                    bits += 8;
                    bufferIndex++;
                    count--;
                    continue;
                }

                // Get the next code
                code = data & codeMask;
                data >>= codeSize;
                bits -= codeSize;

                // Interpret the code
                if (code > availableCode || code == endCode)
                {
                    break;
                }

                if (code == clearCode)
                {
                    // Reset the decoder
                    codeSize = minCodeSize + 1;
                    codeMask = (1 << codeSize) - 1;
                    availableCode = clearCode + 2;
                    oldCode = NullCode;
                    continue;
                }

                if (oldCode == NullCode)
                {
                    pixelStack[top++] = suffix[code];
                    oldCode = code;
                    first = code;
                    continue;
                }

                int inCode = code;
                if (code == availableCode)
                {
                    pixelStack[top++] = first;
                    code = oldCode;
                }

                while (code > clearCode && top < MaxStackSize)
                {
                    pixelStack[top++] = suffix[code];
                    code = prefix[code];
                }

                int suffixCode = suffix[code];
                first = suffixCode;
                pixelStack[top++] = suffixCode;

                // Fix for GIFs that have "deferred clear code" as per:
                // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                if (availableCode < MaxStackSize)
                {
                    prefix[availableCode] = oldCode;
                    suffix[availableCode] = first;
                    availableCode++;
                    if (availableCode == codeMask + 1 && availableCode < MaxStackSize)
                    {
                        codeSize++;
                        codeMask = (1 << codeSize) - 1;
                    }
                }

                oldCode = inCode;
            }

            // Pop a pixel off the pixel stack.
            top--;

            // Skip missing pixels.
            i++;
        }

        // Write back the local values to the instance fields.
        this.top = top;
        this.bits = bits;
        this.codeSize = codeSize;
        this.codeMask = codeMask;
        this.availableCode = availableCode;
        this.oldCode = oldCode;
        this.first = first;
        this.data = data;
        this.count = count;
        this.bufferIndex = bufferIndex;
        this.code = code;
    }

    /// <summary>
    /// Reads the next data block from the stream. A data block begins with a byte,
    /// which defines the size of the block, followed by the block itself.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to store the block in.</param>
    /// <returns>
    /// The <see cref="int"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadBlock(BufferedReadStream stream, Span<byte> buffer)
    {
        int bufferSize = stream.ReadByte();

        if (bufferSize < 1)
        {
            return 0;
        }

        int count = stream.Read(buffer, 0, bufferSize);

        return count != bufferSize ? 0 : bufferSize;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.prefixOwner.Dispose();
        this.suffixOwner.Dispose();
        this.pixelStackOwner.Dispose();
        this.bufferOwner.Dispose();
    }
}
