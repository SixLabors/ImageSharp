// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Location within the file of an <see cref="HeifItem"/>.
/// </summary>
internal class HeifLocation(HeifLocationOffsetOrigin origin, long baseOffset, long offset, long length)
{
    /// <summary>
    /// Gets the origin of the offsets in this location.
    /// </summary>
    public HeifLocationOffsetOrigin Origin { get; } = origin;

    /// <summary>
    /// Gets the base offset of this location.
    /// </summary>
    public long BaseOffset { get; } = baseOffset;

    /// <summary>
    /// Gets the offset of this location.
    /// </summary>
    public long Offset { get; } = offset;

    /// <summary>
    /// Gets the length of this location.
    /// </summary>
    public long Length { get; } = length;

    /// <summary>
    /// Gets the stream position of this location.
    /// </summary>
    /// <param name="positionOfMediaData">Stream position of the MediaData box.</param>
    /// <param name="positionOfItem">Stream position of the previous box.</param>
    public long GetStreamPosition(long positionOfMediaData, long positionOfItem) => this.Origin switch
    {
        HeifLocationOffsetOrigin.FileOffset => this.BaseOffset + this.Offset,
        HeifLocationOffsetOrigin.ItemDataOffset => positionOfMediaData + this.BaseOffset + this.Offset,
        _ => positionOfItem + this.BaseOffset + this.Offset
    };

    public override int GetHashCode() => HashCode.Combine(this.Origin, this.Offset, this.Length, this.BaseOffset);

    public override bool Equals(object? obj)
    {
        if (obj is not HeifLocation other)
        {
            return false;
        }

        if (this.Origin != other.Origin || this.Length != other.Length)
        {
            return false;
        }

        return this.Offset == other.Offset && this.BaseOffset == other.BaseOffset;
    }
}
