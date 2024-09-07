// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieLuv"/> struct.
/// </summary>
public class CieLuvTests
{
    [Fact]
    public void CieLuvConstructorAssignsFields()
    {
        const float l = 75F;
        const float c = -64F;
        const float h = 87F;
        CieLuv cieLuv = new(l, c, h);

        Assert.Equal(l, cieLuv.L);
        Assert.Equal(c, cieLuv.U);
        Assert.Equal(h, cieLuv.V);
    }

    [Fact]
    public void CieLuvEquality()
    {
        CieLuv x = default;
        CieLuv y = new(Vector3.One);

        Assert.True(default == default(CieLuv));
        Assert.False(default != default(CieLuv));
        Assert.Equal(default, default(CieLuv));
        Assert.Equal(new(1, 0, 1), new CieLuv(1, 0, 1));
        Assert.Equal(new(Vector3.One), new CieLuv(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
