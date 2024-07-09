// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieXyy"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class CieXyyTests
{
    [Fact]
    public void CieXyyConstructorAssignsFields()
    {
        const float x = 75F;
        const float y = 64F;
        const float yl = 287F;
        CieXyy cieXyy = new(x, y, yl);

        Assert.Equal(x, cieXyy.X);
        Assert.Equal(y, cieXyy.Y);
        Assert.Equal(y, cieXyy.Y);
    }

    [Fact]
    public void CieXyyEquality()
    {
        CieXyy x = default;
        CieXyy y = new(Vector3.One);

        Assert.True(default == default(CieXyy));
        Assert.False(default != default(CieXyy));
        Assert.Equal(default, default(CieXyy));
        Assert.Equal(new CieXyy(1, 0, 1), new CieXyy(1, 0, 1));
        Assert.Equal(new CieXyy(Vector3.One), new CieXyy(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
