// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Represents a lzw string with a code word and a code length.
/// </summary>
public class LzwString
{
    private static readonly LzwString Empty = new(0, 0, 0, null);

    private readonly LzwString previous;
    private readonly byte value;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzwString"/> class.
    /// </summary>
    /// <param name="code">The code word.</param>
    public LzwString(byte code)
        : this(code, code, 1, null)
    {
    }

    private LzwString(byte value, byte firstChar, int length, LzwString previous)
    {
        this.value = value;
        this.FirstChar = firstChar;
        this.Length = length;
        this.previous = previous;
    }

    /// <summary>
    /// Gets the code length;
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the first character of the codeword.
    /// </summary>
    public byte FirstChar { get; }

    /// <summary>
    /// Concatenates two code words.
    /// </summary>
    /// <param name="other">The code word to concatenate.</param>
    /// <returns>A concatenated lzw string.</returns>
    public LzwString Concatenate(byte other)
    {
        if (this == Empty)
        {
            return new(other);
        }

        return new(other, this.FirstChar, this.Length + 1, this);
    }

    /// <summary>
    /// Writes decoded pixel to buffer at a given position.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="offset">The position to write to.</param>
    /// <returns>The number of bytes written.</returns>
    public int WriteTo(Span<byte> buffer, int offset)
    {
        if (this.Length == 0)
        {
            return 0;
        }

        if (this.Length == 1)
        {
            buffer[offset] = this.value;
            return 1;
        }

        LzwString e = this;
        int endIdx = this.Length - 1;
        if (endIdx >= buffer.Length)
        {
            TiffThrowHelper.ThrowImageFormatException("Error reading lzw compressed stream. Either pixel buffer to write to is to small or code length is invalid!");
        }

        for (int i = endIdx; i >= 0; i--)
        {
            buffer[offset + i] = e.value;
            e = e.previous;
        }

        return this.Length;
    }
}
