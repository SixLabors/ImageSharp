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
    private readonly IMemoryOwner<int> prefix;

    /// <summary>
    /// The suffix buffer.
    /// </summary>
    private readonly IMemoryOwner<int> suffix;

    /// <summary>
    /// The pixel stack buffer.
    /// </summary>
    private readonly IMemoryOwner<int> pixelStack;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzwDecoder"/> class
    /// and sets the stream, where the compressed data should be read from.
    /// </summary>
    /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public LzwDecoder(MemoryAllocator memoryAllocator, BufferedReadStream stream)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

        this.prefix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
        this.suffix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
        this.pixelStack = memoryAllocator.Allocate<int>(MaxStackSize + 1, AllocationOptions.Clean);
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices from the stream, assigning the pixel values to the buffer.
    /// </summary>
    /// <param name="minCodeSize">Minimum code size of the data.</param>
    /// <param name="pixels">The pixel array to decode to.</param>
    public void DecodePixels(int minCodeSize, Buffer2D<byte> pixels)
    {
        // Calculate the clear code. The value of the clear code is 2 ^ minCodeSize
        int clearCode = 1 << minCodeSize;

        // It is possible to specify a larger LZW minimum code size than the palette length in bits
        // which may leave a gap in the codes where no colors are assigned.
        // http://www.matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp#lzw_compression
        if (minCodeSize < 2 || minCodeSize > MaximumLzwBits || clearCode > MaxStackSize)
        {
            // Don't attempt to decode the frame indices.
            // Theoretically we could determine a min code size from the length of the provided
            // color palette but we won't bother since the image is most likely corrupted.
            return;
        }

        // The resulting index table length.
        int width = pixels.Width;
        int height = pixels.Height;
        int length = width * height;

        int codeSize = minCodeSize + 1;

        // Calculate the end code
        int endCode = clearCode + 1;

        // Calculate the available code.
        int availableCode = clearCode + 2;

        // Jillzhangs Code see: http://giflib.codeplex.com/
        // Adapted from John Cristy's ImageMagick.
        int code;
        int oldCode = NullCode;
        int codeMask = (1 << codeSize) - 1;
        int bits = 0;

        int top = 0;
        int count = 0;
        int bi = 0;
        int xyz = 0;

        int data = 0;
        int first = 0;

        ref int prefixRef = ref MemoryMarshal.GetReference(this.prefix.GetSpan());
        ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
        ref int pixelStackRef = ref MemoryMarshal.GetReference(this.pixelStack.GetSpan());

        for (code = 0; code < clearCode; code++)
        {
            Unsafe.Add(ref suffixRef, (uint)code) = (byte)code;
        }

        Span<byte> buffer = stackalloc byte[byte.MaxValue];

        int y = 0;
        int x = 0;
        int rowMax = width;
        ref byte pixelsRowRef = ref MemoryMarshal.GetReference(pixels.DangerousGetRowSpan(y));
        while (xyz < length)
        {
            // Reset row reference.
            if (xyz == rowMax)
            {
                x = 0;
                pixelsRowRef = ref MemoryMarshal.GetReference(pixels.DangerousGetRowSpan(++y));
                rowMax = (y * width) + width;
            }

            if (top == 0)
            {
                if (bits < codeSize)
                {
                    // Load bytes until there are enough bits for a code.
                    if (count == 0)
                    {
                        // Read a new data block.
                        count = this.ReadBlock(buffer);
                        if (count == 0)
                        {
                            break;
                        }

                        bi = 0;
                    }

                    data += buffer[bi] << bits;

                    bits += 8;
                    bi++;
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
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = Unsafe.Add(ref suffixRef, (uint)code);
                    oldCode = code;
                    first = code;
                    continue;
                }

                int inCode = code;
                if (code == availableCode)
                {
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = (byte)first;

                    code = oldCode;
                }

                while (code > clearCode)
                {
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = Unsafe.Add(ref suffixRef, (uint)code);
                    code = Unsafe.Add(ref prefixRef, (uint)code);
                }

                int suffixCode = Unsafe.Add(ref suffixRef, (uint)code);
                first = suffixCode;
                Unsafe.Add(ref pixelStackRef, (uint)top++) = suffixCode;

                // Fix for Gifs that have "deferred clear code" as per here :
                // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                if (availableCode < MaxStackSize)
                {
                    Unsafe.Add(ref prefixRef, (uint)availableCode) = oldCode;
                    Unsafe.Add(ref suffixRef, (uint)availableCode) = first;
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

            // Clear missing pixels
            xyz++;
            Unsafe.Add(ref pixelsRowRef, (uint)x++) = (byte)Unsafe.Add(ref pixelStackRef, (uint)top);
        }
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices from the stream allowing skipping of the data.
    /// </summary>
    /// <param name="minCodeSize">Minimum code size of the data.</param>
    /// <param name="length">The resulting index table length.</param>
    public void SkipIndices(int minCodeSize, int length)
    {
        // Calculate the clear code. The value of the clear code is 2 ^ minCodeSize
        int clearCode = 1 << minCodeSize;

        // It is possible to specify a larger LZW minimum code size than the palette length in bits
        // which may leave a gap in the codes where no colors are assigned.
        // http://www.matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp#lzw_compression
        if (minCodeSize < 2 || minCodeSize > MaximumLzwBits || clearCode > MaxStackSize)
        {
            // Don't attempt to decode the frame indices.
            // Theoretically we could determine a min code size from the length of the provided
            // color palette but we won't bother since the image is most likely corrupted.
            GifThrowHelper.ThrowInvalidImageContentException("Gif Image does not contain a valid LZW minimum code.");
        }

        int codeSize = minCodeSize + 1;

        // Calculate the end code
        int endCode = clearCode + 1;

        // Calculate the available code.
        int availableCode = clearCode + 2;

        // Jillzhangs Code see: http://giflib.codeplex.com/
        // Adapted from John Cristy's ImageMagick.
        int code;
        int oldCode = NullCode;
        int codeMask = (1 << codeSize) - 1;
        int bits = 0;

        int top = 0;
        int count = 0;
        int bi = 0;
        int xyz = 0;

        int data = 0;
        int first = 0;

        ref int prefixRef = ref MemoryMarshal.GetReference(this.prefix.GetSpan());
        ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
        ref int pixelStackRef = ref MemoryMarshal.GetReference(this.pixelStack.GetSpan());

        for (code = 0; code < clearCode; code++)
        {
            Unsafe.Add(ref suffixRef, (uint)code) = (byte)code;
        }

        Span<byte> buffer = stackalloc byte[byte.MaxValue];
        while (xyz < length)
        {
            if (top == 0)
            {
                if (bits < codeSize)
                {
                    // Load bytes until there are enough bits for a code.
                    if (count == 0)
                    {
                        // Read a new data block.
                        count = this.ReadBlock(buffer);
                        if (count == 0)
                        {
                            break;
                        }

                        bi = 0;
                    }

                    data += buffer[bi] << bits;

                    bits += 8;
                    bi++;
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
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = Unsafe.Add(ref suffixRef, (uint)code);
                    oldCode = code;
                    first = code;
                    continue;
                }

                int inCode = code;
                if (code == availableCode)
                {
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = (byte)first;

                    code = oldCode;
                }

                while (code > clearCode)
                {
                    Unsafe.Add(ref pixelStackRef, (uint)top++) = Unsafe.Add(ref suffixRef, (uint)code);
                    code = Unsafe.Add(ref prefixRef, (uint)code);
                }

                int suffixCode = Unsafe.Add(ref suffixRef, (uint)code);
                first = suffixCode;
                Unsafe.Add(ref pixelStackRef, (uint)top++) = suffixCode;

                // Fix for Gifs that have "deferred clear code" as per here :
                // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                if (availableCode < MaxStackSize)
                {
                    Unsafe.Add(ref prefixRef, (uint)availableCode) = oldCode;
                    Unsafe.Add(ref suffixRef, (uint)availableCode) = first;
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

            // Clear missing pixels
            xyz++;
        }
    }

    /// <summary>
    /// Reads the next data block from the stream. A data block begins with a byte,
    /// which defines the size of the block, followed by the block itself.
    /// </summary>
    /// <param name="buffer">The buffer to store the block in.</param>
    /// <returns>
    /// The <see cref="int"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadBlock(Span<byte> buffer)
    {
        int bufferSize = this.stream.ReadByte();

        if (bufferSize < 1)
        {
            return 0;
        }

        int count = this.stream.Read(buffer, 0, bufferSize);

        return count != bufferSize ? 0 : bufferSize;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.prefix.Dispose();
        this.suffix.Dispose();
        this.pixelStack.Dispose();
    }
}
