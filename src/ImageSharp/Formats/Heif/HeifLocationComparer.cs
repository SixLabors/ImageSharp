// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Orders item extents by their resolved absolute stream position.
/// </summary>
internal sealed class HeifLocationComparer : IComparer<HeifLocation>
{
    /// <summary>
    /// The absolute origin of item-data-relative extents.
    /// </summary>
    private readonly long positionOfMediaData;

    /// <summary>
    /// The absolute origin of item-relative extents.
    /// </summary>
    private readonly long positionOfItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifLocationComparer"/> class.
    /// </summary>
    /// <param name="positionOfMediaData">The absolute origin of the item-data payload.</param>
    /// <param name="positionOfItem">The absolute origin of the referenced item payload.</param>
    public HeifLocationComparer(long positionOfMediaData, long positionOfItem)
    {
        this.positionOfMediaData = positionOfMediaData;
        this.positionOfItem = positionOfItem;
    }

    /// <summary>
    /// Compares two extents by their resolved absolute stream positions.
    /// </summary>
    /// <param name="x">The first extent.</param>
    /// <param name="y">The second extent.</param>
    /// <returns>A negative value when <paramref name="x"/> precedes <paramref name="y"/>, zero when their positions match, or a positive value otherwise.</returns>
    public int Compare(HeifLocation? x, HeifLocation? y)
    {
        if (x is null)
        {
            if (y is null)
            {
                return 0;
            }

            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        long xPos = x.GetStreamPosition(this.positionOfMediaData, this.positionOfItem);
        long yPos = y.GetStreamPosition(this.positionOfMediaData, this.positionOfItem);

        // CompareTo avoids overflowing when valid 64-bit offsets lie near opposite numeric limits.
        return xPos.CompareTo(yPos);
    }
}
