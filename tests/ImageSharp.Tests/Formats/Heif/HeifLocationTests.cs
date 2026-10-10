// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
public class HeifLocationTests
{
    [Fact]
    public void LocationsFromDifferentOriginsResolveAndCompareByStreamPosition()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocationComparer comparer = new(dataPosition, itemPosition);
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 0, 10, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 0, 110, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 0, 60, 42);
        HeifLocation fromItemWithBase = new(HeifLocationOffsetOrigin.ItemOffset, 40, 10, 42);
        HeifLocation fromFileWithBase = new(HeifLocationOffsetOrigin.FileOffset, 40, 110, 42);
        HeifLocation fromDataWithBase = new(HeifLocationOffsetOrigin.ItemDataOffset, 40, 60, 42);

        // Act and assert
        Assert.Equal(110, fromItem.GetStreamPosition(dataPosition, itemPosition));
        Assert.Equal(110, fromFile.GetStreamPosition(dataPosition, itemPosition));
        Assert.Equal(110, fromData.GetStreamPosition(dataPosition, itemPosition));
        Assert.Equal(150, fromItemWithBase.GetStreamPosition(dataPosition, itemPosition));
        Assert.Equal(150, fromFileWithBase.GetStreamPosition(dataPosition, itemPosition));
        Assert.Equal(150, fromDataWithBase.GetStreamPosition(dataPosition, itemPosition));

        Assert.Equal(0, comparer.Compare(fromItem, fromData));
        Assert.Equal(0, comparer.Compare(fromItem, fromFile));
        Assert.Equal(0, comparer.Compare(fromFile, fromData));
        Assert.Equal(-1, comparer.Compare(fromItem, fromItemWithBase));
        Assert.Equal(-1, comparer.Compare(fromData, fromFileWithBase));
        Assert.Equal(1, comparer.Compare(fromDataWithBase, fromFile));
        Assert.Equal(1, comparer.Compare(fromFileWithBase, fromItem));
    }
}
