// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes one contiguous extent of an item's encoded data.
/// </summary>
/// <param name="origin">The origin from which the base and extent offsets are measured.</param>
/// <param name="baseOffset">The item-location base offset.</param>
/// <param name="offset">The extent offset relative to the base offset.</param>
/// <param name="length">The length of the extent in bytes.</param>
internal sealed class HeifLocation(HeifLocationOffsetOrigin origin, long baseOffset, long offset, long length)
{
    /// <summary>
    /// Gets the origin of the offsets in this location.
    /// </summary>
    public HeifLocationOffsetOrigin Origin { get; } = origin;

    /// <summary>
    /// Gets the item-location base offset in bytes.
    /// </summary>
    public long BaseOffset { get; } = baseOffset;

    /// <summary>
    /// Gets the extent offset relative to <see cref="BaseOffset"/> in bytes.
    /// </summary>
    public long Offset { get; } = offset;

    /// <summary>
    /// Gets the extent length in bytes.
    /// </summary>
    public long Length { get; } = length;

    /// <summary>
    /// Resolves the absolute stream position of this extent.
    /// </summary>
    /// <param name="positionOfMediaData">The absolute origin of the item-data payload.</param>
    /// <param name="positionOfItem">The absolute origin of the referenced item payload.</param>
    /// <returns>The absolute byte position of the extent in the input stream.</returns>
    public long GetStreamPosition(long positionOfMediaData, long positionOfItem) => this.Origin switch
    {
        HeifLocationOffsetOrigin.FileOffset => this.BaseOffset + this.Offset,
        HeifLocationOffsetOrigin.ItemDataOffset => positionOfMediaData + this.BaseOffset + this.Offset,
        _ => positionOfItem + this.BaseOffset + this.Offset
    };

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Origin, this.Offset, this.Length, this.BaseOffset);

    /// <inheritdoc/>
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
