// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

internal class HeifLocationComparer : IComparer<HeifLocation>
{
    private readonly long positionOfMediaData;
    private readonly long positionOfItem;

    public HeifLocationComparer(long positionOfMediaData, long positionOfItem)
    {
        this.positionOfMediaData = positionOfMediaData;
        this.positionOfItem = positionOfItem;
    }

    public int Compare(HeifLocation? x, HeifLocation? y)
    {
        if (x == null)
        {
            if (y == null)
            {
                return 0;
            }

            return 1;
        }

        if (y == null)
        {
            return -1;
        }

        long xPos = x.GetStreamPosition(this.positionOfMediaData, this.positionOfItem);
        long yPos = y.GetStreamPosition(this.positionOfMediaData, this.positionOfItem);

        return Math.Sign(xPos - yPos);
    }
}
