// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Gives the stream range of one child box payload that <see cref="HeifBoxReader.IndexChildren"/> found.
/// </summary>
internal readonly struct HeifBoxRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifBoxRange"/> struct.
    /// </summary>
    /// <param name="offset">The absolute stream offset of the payload.</param>
    /// <param name="length">The payload length. The box reader validated this length against the parent box.</param>
    /// <param name="type">The box type.</param>
    public HeifBoxRange(long offset, long length, Heif4CharCode type)
    {
        this.Offset = offset;
        this.Length = length;
        this.Type = type;
    }

    /// <summary>
    /// Gets the absolute stream offset of the payload.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the payload length.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the box type, or zero when the parent has no box of the requested type.
    /// </summary>
    public Heif4CharCode Type { get; }

    /// <summary>
    /// Gets a value indicating whether the parent contains the box.
    /// </summary>
    public bool IsPresent => this.Type != 0;
}
