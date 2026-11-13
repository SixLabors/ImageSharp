// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Provides methods to read ICC data types
/// </summary>
internal sealed partial class IccDataReader
{
    /// <summary>
    /// The data that is read
    /// </summary>
    private readonly byte[] data;

    /// <summary>
    /// The current reading position
    /// </summary>
    private int currentIndex;
    private int tagStart;
    private int tagEnd;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccDataReader"/> class.
    /// </summary>
    /// <param name="data">The data to read</param>
    public IccDataReader(byte[] data)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.tagEnd = data.Length;
    }

    /// <summary>
    /// Gets the length in bytes of the raw data
    /// </summary>
    public int DataLength => this.data.Length;

    /// <summary>
    /// Sets the reading position to the given value
    /// </summary>
    /// <param name="index">The new index position</param>
    public void SetIndex(int index)
        => this.currentIndex = Math.Clamp(index, 0, this.data.Length);

    /// <summary>
    /// Returns the current <see cref="currentIndex"/> without increment and adds the given increment
    /// </summary>
    /// <param name="increment">The value to increment <see cref="currentIndex"/></param>
    /// <returns>The current <see cref="currentIndex"/> without the increment</returns>
    private int AddIndex(int increment)
    {
        this.EnsureAvailable((uint)increment, 1);

        int tmp = this.currentIndex;
        this.currentIndex += increment;
        return tmp;
    }

    /// <summary>
    /// Checks that the remaining tag data can hold the requested items.
    /// </summary>
    /// <param name="count">The number of encoded items.</param>
    /// <param name="bytesPerItem">The encoded size or minimum size of each item.</param>
    private void EnsureAvailable(ulong count, ulong bytesPerItem)
    {
        // Divide the available bytes so the count cannot overflow a size calculation.
        if (this.currentIndex < this.tagStart || this.currentIndex > this.tagEnd ||
            count > (ulong)(this.tagEnd - this.currentIndex) / bytesPerItem)
        {
            throw new InvalidIccProfileException("ICC tag data exceeds the available bytes.");
        }
    }

    /// <summary>
    /// Moves to a relative offset inside the current tag.
    /// </summary>
    /// <param name="origin">The base position of the offset.</param>
    /// <param name="offset">The byte offset from the base position.</param>
    private void Seek(int origin, uint offset)
    {
        long position = (long)origin + offset;
        if (position < this.tagStart || position > this.tagEnd)
        {
            throw new InvalidIccProfileException("ICC offset exceeds the tag data.");
        }

        this.currentIndex = (int)position;
    }

    /// <summary>
    /// Calculates the 4 byte padding and adds it to the <see cref="currentIndex"/> variable
    /// </summary>
    private void AddPadding()
        => this.currentIndex += this.CalcPadding();

    /// <summary>
    /// Calculates the 4 byte padding
    /// </summary>
    /// <returns>the number of bytes to pad</returns>
    private int CalcPadding()
    {
        int p = 4 - (this.currentIndex % 4);
        return p >= 4 ? 0 : p;
    }

    /// <summary>
    /// Gets the bit value at a specified position
    /// </summary>
    /// <param name="value">The value from where the bit will be extracted</param>
    /// <param name="position">Position of the bit. Zero based index from left to right.</param>
    /// <returns>The bit value at specified position</returns>
    private static bool GetBit(byte value, int position)
        => ((value >> (7 - position)) & 1) == 1;
}
