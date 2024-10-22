// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Location within the file of an <see cref="HeifItem"/>.
/// </summary>
internal class HeifLocation(HeifLocationOffsetOrigin method, long baseOffset, uint dataReferenceIndex, long offset, long length, uint extentIndex)
{
    /// <summary>
    /// Gets the origin of the offsets in this location.
    /// </summary>
    public HeifLocationOffsetOrigin Origin { get; } = method;

    /// <summary>
    /// Gets the base offset of this location.
    /// </summary>
    public long BaseOffset { get; } = baseOffset;

    /// <summary>
    /// Gets the data reference index of this location.
    /// </summary>
    public uint DataReferenceInxdex { get; } = dataReferenceIndex;

    /// <summary>
    /// Gets the offset of this location.
    /// </summary>
    public long Offset { get; } = offset;

    /// <summary>
    /// Gets the length of this location.
    /// </summary>
    public long Length { get; } = length;

    /// <summary>
    /// Gets the extent index of this location.
    /// </summary>
    public uint ExtentInxdex { get; } = extentIndex;

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
}
