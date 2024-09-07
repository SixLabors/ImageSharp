// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieLch"/> struct.
/// </summary>
public class CieLchTests
{
    [Fact]
    public void CieLchConstructorAssignsFields()
    {
        const float l = 75F;
        const float c = 64F;
        const float h = 287F;
        CieLch cieLch = new(l, c, h);

        Assert.Equal(l, cieLch.L);
        Assert.Equal(c, cieLch.C);
        Assert.Equal(h, cieLch.H);
    }

    [Fact]
    public void CieLchEquality()
    {
        CieLch x = default;
        CieLch y = new(Vector3.One);

        Assert.True(default == default(CieLch));
        Assert.False(default != default(CieLch));
        Assert.Equal(default, default(CieLch));
        Assert.Equal(new(1, 0, 1), new CieLch(1, 0, 1));
        Assert.Equal(new(Vector3.One), new CieLch(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
