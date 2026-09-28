// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1LevelBufferTests
{
    [Fact]
    public void InitializeStoresAbsoluteSaturatedLevels()
    {
        // Arrange
        using Av1LevelBuffer levels = new(Configuration.Default, new Size(2, 2));
        Span<int> coefficients = [-300, -1, 1, 300];

        // Act
        levels.Initialize(coefficients);

        // Assert
        Assert.Equal([127, 1], levels.GetRow(0)[..2].ToArray());
        Assert.Equal([1, 127], levels.GetRow(1)[..2].ToArray());
    }
}
