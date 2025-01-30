// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieXyz"/> struct.
/// </summary>
public class CieXyzTests
{
    [Fact]
    public void CieXyzConstructorAssignsFields()
    {
        const float x = 75F;
        const float y = 64F;
        const float z = 287F;
        CieXyz cieXyz = new(x, y, z);

        Assert.Equal(x, cieXyz.X);
        Assert.Equal(y, cieXyz.Y);
        Assert.Equal(z, cieXyz.Z);
    }

    [Fact]
    public void CieXyzEquality()
    {
        CieXyz x = default;
        CieXyz y = new(Vector3.One);

        Assert.True(default == default(CieXyz));
        Assert.False(default != default(CieXyz));
        Assert.Equal(default, default(CieXyz));
        Assert.Equal(new(1, 0, 1), new CieXyz(1, 0, 1));
        Assert.Equal(new(Vector3.One), new CieXyz(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
