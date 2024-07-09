// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieLchuv"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class CieLchuvTests
{
    [Fact]
    public void CieLchuvConstructorAssignsFields()
    {
        const float l = 75F;
        const float c = 64F;
        const float h = 287F;
        CieLchuv cieLchuv = new(l, c, h);

        Assert.Equal(l, cieLchuv.L);
        Assert.Equal(c, cieLchuv.C);
        Assert.Equal(h, cieLchuv.H);
    }

    [Fact]
    public void CieLchuvEquality()
    {
        CieLchuv x = default;
        CieLchuv y = new(Vector3.One);

        Assert.True(default == default(CieLchuv));
        Assert.False(default != default(CieLchuv));
        Assert.Equal(default, default(CieLchuv));
        Assert.Equal(new CieLchuv(1, 0, 1), new CieLchuv(1, 0, 1));
        Assert.Equal(new CieLchuv(Vector3.One), new CieLchuv(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
