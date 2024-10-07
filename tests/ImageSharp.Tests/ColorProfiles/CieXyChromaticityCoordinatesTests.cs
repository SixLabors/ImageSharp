// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieXyChromaticityCoordinates"/> struct.
/// </summary>
public class CieXyChromaticityCoordinatesTests
{
    [Fact]
    public void CieXyChromaticityCoordinatesConstructorAssignsFields()
    {
        const float x = .75F;
        const float y = .64F;
        CieXyChromaticityCoordinates coordinates = new(x, y);

        Assert.Equal(x, coordinates.X);
        Assert.Equal(y, coordinates.Y);
    }

    [Fact]
    public void CieXyChromaticityCoordinatesEquality()
    {
        CieXyChromaticityCoordinates x = default;
        CieXyChromaticityCoordinates y = new(1, 1);

        Assert.True(default == default(CieXyChromaticityCoordinates));
        Assert.True(new CieXyChromaticityCoordinates(1, 0) != default);
        Assert.False(new CieXyChromaticityCoordinates(1, 0) == default);
        Assert.Equal(default, default(CieXyChromaticityCoordinates));
        Assert.Equal(new(1, 0), new CieXyChromaticityCoordinates(1, 0));
        Assert.Equal(new(1, 1), new CieXyChromaticityCoordinates(1, 1));
        Assert.False(x.Equals(y));
        Assert.False(new CieXyChromaticityCoordinates(1, 0) == default);
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
