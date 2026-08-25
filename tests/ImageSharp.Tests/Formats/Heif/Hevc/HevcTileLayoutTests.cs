// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC coding-tree-block mappings for unequal tile dimensions.
/// </summary>
[Trait("Format", "Heic")]
public class HevcTileLayoutTests
{
    /// <summary>
    /// Verifies the normative tile-row, tile-column, and in-tile raster traversal order.
    /// </summary>
    [Fact]
    public void MapsEveryAddressBetweenRasterAndTileScanOrder()
    {
        HevcTileLayout layout = new(new[] { 2, 1 }, new[] { 1, 2 });

        ReadOnlySpan<int> expectedRasterAddresses = [0, 1, 2, 3, 4, 6, 7, 5, 8];
        for (int tileScanAddress = 0; tileScanAddress < expectedRasterAddresses.Length; tileScanAddress++)
        {
            int rasterAddress = expectedRasterAddresses[tileScanAddress];
            Assert.Equal(rasterAddress, layout.GetRasterAddress(tileScanAddress));
            Assert.Equal(tileScanAddress, layout.GetTileScanAddress(rasterAddress));
        }
    }

    /// <summary>
    /// Verifies tile identity, local coordinates, and dimensions on both sides of each tile boundary.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0, 0, 2, 1)]
    [InlineData(2, 1, 0, 0, 1, 1)]
    [InlineData(3, 2, 0, 0, 2, 2)]
    [InlineData(7, 2, 1, 1, 2, 2)]
    [InlineData(5, 3, 0, 0, 1, 2)]
    [InlineData(8, 3, 0, 1, 1, 2)]
    public void ResolvesTileLocalPosition(
        int rasterAddress,
        int expectedTileIndex,
        int expectedColumn,
        int expectedRow,
        int expectedWidth,
        int expectedHeight)
    {
        HevcTileLayout layout = new(new[] { 2, 1 }, new[] { 1, 2 });

        layout.GetTilePosition(
            rasterAddress,
            out int tileIndex,
            out int column,
            out int row,
            out int width,
            out int height);

        Assert.Equal(expectedTileIndex, tileIndex);
        Assert.Equal(expectedColumn, column);
        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }
}
