// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
public class HeifLocationTests
{
    [Fact]
    public void CheckSameLocationFromDifferentOrigin()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 0, 10, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 0, 110, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 0, 60, 42);

        // Act
        long itemActual = fromItem.GetStreamPosition(dataPosition, itemPosition);
        long fileActual = fromFile.GetStreamPosition(dataPosition, itemPosition);
        long dataActual = fromData.GetStreamPosition(dataPosition, itemPosition);

        // Assert
        Assert.Equal(110, itemActual);
        Assert.Equal(110, fileActual);
        Assert.Equal(110, dataActual);
    }

    [Fact]
    public void CheckSameLocationFromDifferentOriginWithBaseOffset()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 40, 10, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 40, 110, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 40, 60, 42);

        // Act
        long itemActual = fromItem.GetStreamPosition(dataPosition, itemPosition);
        long fileActual = fromFile.GetStreamPosition(dataPosition, itemPosition);
        long dataActual = fromData.GetStreamPosition(dataPosition, itemPosition);

        // Assert
        Assert.Equal(150, itemActual);
        Assert.Equal(150, fileActual);
        Assert.Equal(150, dataActual);
    }

    [Fact]
    public void CheckComparerOnSameLocation()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 0, 50, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 30, 120, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 40, 60, 42);
        HeifLocationComparer comparer = new(dataPosition, itemPosition);

        // Act
        int item2Data = comparer.Compare(fromItem, fromData);
        int item2File = comparer.Compare(fromItem, fromFile);
        int file2Data = comparer.Compare(fromFile, fromData);
        int data2File = comparer.Compare(fromData, fromFile);

        // Assert
        Assert.Equal(0, item2Data);
        Assert.Equal(0, item2File);
        Assert.Equal(0, file2Data);
        Assert.Equal(0, data2File);
    }

    [Fact]
    public void CheckComparerOnLowerLocation()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 0, 50, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 30, 150, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 40, 80, 42);
        HeifLocationComparer comparer = new(dataPosition, itemPosition);

        // Act
        int item2Data = comparer.Compare(fromItem, fromData);
        int item2File = comparer.Compare(fromItem, fromFile);

        // Assert
        Assert.Equal(-1, item2Data);
        Assert.Equal(-1, item2File);
    }

    [Fact]
    public void CheckComparerOnHigherLocation()
    {
        // Arrange
        const int dataPosition = 50;
        const int itemPosition = 100;
        HeifLocation fromItem = new(HeifLocationOffsetOrigin.ItemOffset, 10, 50, 42);
        HeifLocation fromFile = new(HeifLocationOffsetOrigin.FileOffset, 30, 120, 42);
        HeifLocation fromData = new(HeifLocationOffsetOrigin.ItemDataOffset, 40, 60, 42);
        HeifLocationComparer comparer = new(dataPosition, itemPosition);

        // Act
        int item2Data = comparer.Compare(fromItem, fromData);
        int item2File = comparer.Compare(fromItem, fromFile);

        // Assert
        Assert.Equal(1, item2Data);
        Assert.Equal(1, item2File);
    }
}
