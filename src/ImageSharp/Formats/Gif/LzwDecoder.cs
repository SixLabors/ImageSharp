// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// The smallest LZW minimum code size that the decoder accepts.
    /// </summary>
    private const int MinimumLzwBits = 2;

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

    /// <summary>
    /// The segment integrity mode. It controls if damaged or truncated LZW data throws.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;
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
    /// Whether a call reached the end of the LZW data before the last index. Later calls do not decode more codes,
    /// because no valid index follows the end of the data.
    /// </summary>
    private bool isEndOfData;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzwDecoder"/> class
    /// and sets the stream, where the compressed data should be read from.
    /// </summary>
    /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="minCodeSize">The minimum code size that <see cref="TryReadMinCodeSize"/> read.</param>
    /// <param name="integrityHandling">The segment integrity mode. It controls if damaged or truncated LZW data throws.</param>
    public LzwDecoder(MemoryAllocator memoryAllocator, BufferedReadStream stream, int minCodeSize, SegmentIntegrityHandling integrityHandling)
    {
        DebugGuard.MustBeBetweenOrEqualTo(minCodeSize, MinimumLzwBits, MaximumLzwBits, nameof(minCodeSize));

        this.stream = stream;
        this.integrityHandling = integrityHandling;

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
    /// Receives each index that the LZW loop takes from the pixel stack.
    /// </summary>
    private interface IIndexSink
    {
        /// <summary>
        /// Stores or discards the index at the top of the pixel stack.
        /// </summary>
        /// <param name="indices">The destination indices.</param>
        /// <param name="position">The position of the index in <paramref name="indices"/>.</param>
        /// <param name="pixelStack">The pixel stack.</param>
        /// <param name="top">The position of the index in <paramref name="pixelStack"/>.</param>
        public static abstract void Store(Span<byte> indices, int position, Span<int> pixelStack, int top);
    }

    /// <summary>
    /// Reads the LZW minimum code size of a frame and checks it.
    /// </summary>
    /// <remarks>
    /// GIF89a section 22 puts the LZW minimum code size byte after the color table of a frame and before the data sub-blocks. The value must leave room for the
    /// clear code and the end code in a table of 4096 codes with 12-bit codes. A larger value is possible for a small palette, so the decoder does not compare
    /// it to the palette. See http://www.matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp#lzw_compression.
    /// </remarks>
    /// <param name="stream">The stream that is at the LZW minimum code size byte.</param>
    /// <param name="integrityHandling">The segment integrity mode. It controls if a missing or invalid value throws.</param>
    /// <param name="minCodeSize">The minimum code size, or the byte that is not valid.</param>
    /// <returns>
    /// <see langword="true"/> if the value is valid. <see langword="false"/> if the byte is missing or not valid, and the mode ignores image data errors. The
    /// caller then does not decode the frame indices.
    /// </returns>
    /// <exception cref="InvalidImageContentException">
    /// The byte is missing or not valid, and the integrity mode does not ignore image data errors.
    /// </exception>
    public static bool TryReadMinCodeSize(BufferedReadStream stream, SegmentIntegrityHandling integrityHandling, out int minCodeSize)
    {
        minCodeSize = stream.ReadByte();
        if (minCodeSize is < MinimumLzwBits or > MaximumLzwBits)
        {
            // The end of the stream gives -1, so the same check finds a missing byte.
            integrityHandling.ThrowOrIgnoreImageDataError("The GIF LZW minimum code size is missing or not valid.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices for a single row from the stream, assigning the pixel values to the buffer.
    /// </summary>
    /// <remarks>
    /// The method throws for damaged or truncated LZW data unless the integrity mode ignores image data errors.
    /// When the mode ignores them, an early end of the data stops the row, and the missing indices are zero.
    /// Each later call then gives only zero indices and reads no data.
    /// </remarks>
    /// <param name="indices">The pixel indices array to decode to.</param>
    /// <returns>
    /// <see langword="false"/> if a code is not valid. A code is not valid if it is larger than the next code to add
    /// to the table. The first code of the data or the first code after a clear code is not valid if it is outside the
    /// LZW code tables. The caller must then stop decoding the frame and must not use <paramref name="indices"/>.
    /// Otherwise <see langword="true"/>.
    /// </returns>
    /// <exception cref="InvalidImageContentException">
    /// The LZW data is damaged or truncated, and the integrity mode does not ignore image data errors.
    /// </exception>
    public bool DecodePixelRow(Span<byte> indices)
    {
        indices.Clear();
        return this.DecodeIndices<StoreIndices>(indices, indices.Length);
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices from the stream allowing skipping of the data.
    /// </summary>
    /// <remarks>
    /// The method throws for damaged or truncated LZW data unless the integrity mode ignores image data errors.
    /// When the mode ignores them, an early end of the data stops the skip. Each later call then reads no data.
    /// </remarks>
    /// <param name="length">The resulting index table length.</param>
    /// <returns>
    /// <see langword="false"/> if a code is not valid. A code is not valid if it is larger than the next code to add
    /// to the table. The first code of the data or the first code after a clear code is not valid if it is outside the
    /// LZW code tables. The caller must then stop decoding the frame. Otherwise <see langword="true"/>.
    /// </returns>
    /// <exception cref="InvalidImageContentException">
    /// The LZW data is damaged or truncated, and the integrity mode does not ignore image data errors.
    /// </exception>
    public bool SkipIndices(int length) => this.DecodeIndices<DiscardIndices>(default, length);

    /// <summary>
    /// Decodes the next indices from the stream. The sink stores each index in the destination or discards it.
    /// </summary>
    /// <remarks>
    /// The sink is a struct type argument, so the JIT compiles one copy of the loop for each sink and inlines the sink. The store copy writes each index to the
    /// row. The discard copy does not read the pixel stack.
    /// </remarks>
    /// <typeparam name="TSink">The sink that stores or discards each index.</typeparam>
    /// <param name="indices">The destination indices. The discard sink does not use them.</param>
    /// <param name="length">The number of indices to decode. For the store sink, it is the length of <paramref name="indices"/>.</param>
    /// <returns>The value that <see cref="DecodePixelRow"/> and <see cref="SkipIndices"/> document.</returns>
    private bool DecodeIndices<TSink>(Span<byte> indices, int length)
        where TSink : struct, IIndexSink
    {
        if (this.isEndOfData)
        {
            // An earlier call reached the end of the data. The row keeps zero indices, or the skip reads no data.
            return true;
        }

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
                            // A block terminator, a short block or the end of the stream came before the last index.
                            this.integrityHandling.ThrowOrIgnoreImageDataError("The GIF LZW data ends before the frame is complete.");
                            this.isEndOfData = true;
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
                if (code > availableCode)
                {
                    // The code is not in the table and is not the next code to add. The data is damaged at this code,
                    // so the decoder cannot decode a later code safely. The frame stops.
                    this.integrityHandling.ThrowOrIgnoreImageDataError("The GIF LZW data contains a code that is not valid.");
                    return false;
                }

                if (code == endCode)
                {
                    // The end code came before the last index.
                    this.integrityHandling.ThrowOrIgnoreImageDataError("The GIF LZW data ends before the frame is complete.");
                    this.isEndOfData = true;
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
                    // This first code has no previous string to extend. With a tolerated minimum size of 12,
                    // the next available code is 4098, beyond the 4096-entry tables. The decoder reports the damaged
                    // data before it reads a table entry. It cannot decode a later code safely, so the frame stops.
                    if (code >= MaxStackSize)
                    {
                        this.integrityHandling.ThrowOrIgnoreImageDataError("The GIF LZW code exceeds the code table bounds.");
                        return false;
                    }

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

            // The loop removes the index at the top of the pixel stack. Then the sink stores the index or discards it.
            top--;
            TSink.Store(indices, i, pixelStack, top);
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
        return true;
    }

    /// <summary>
    /// Reads the next data block from the stream. A data block begins with a byte,
    /// which defines the size of the block, followed by the block itself.
    /// </summary>
    /// <remarks>
    /// A block terminator before the last index stays in the stream. The frame reader reads it when it skips the
    /// remaining sub-blocks. Thus, the frame reader does not read the next block as a sub-block.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to store the block in.</param>
    /// <returns>
    /// The size of the block. The value is 0 for a block terminator, a short block or the end of the stream.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadBlock(BufferedReadStream stream, Span<byte> buffer)
    {
        int bufferSize = stream.ReadByte();

        if (bufferSize < 1)
        {
            if (bufferSize == 0)
            {
                // The stream position goes back to the block terminator.
                stream.Position--;
            }

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

    /// <summary>
    /// Writes each index to the destination row.
    /// </summary>
    private readonly struct StoreIndices : IIndexSink
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Span<byte> indices, int position, Span<int> pixelStack, int top)
        {
            // DecodePixelRow passes the row length as the loop length, so the position is always inside the row. The write does not need a bounds check.
            Unsafe.Add(ref MemoryMarshal.GetReference(indices), (uint)position) = (byte)pixelStack[top];
        }
    }

    /// <summary>
    /// Discards each index. The skip path does not read the pixel stack.
    /// </summary>
    private readonly struct DiscardIndices : IIndexSink
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Span<byte> indices, int position, Span<int> pixelStack, int top)
        {
        }
    }
}
