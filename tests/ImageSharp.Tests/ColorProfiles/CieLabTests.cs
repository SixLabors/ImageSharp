// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="CieLab"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class CieLabTests
{
    [Fact]
    public void CieLabConstructorAssignsFields()
    {
        const float l = 75F;
        const float a = -64F;
        const float b = 87F;
        CieLab cieLab = new(l, a, b);

        Assert.Equal(l, cieLab.L);
        Assert.Equal(a, cieLab.A);
        Assert.Equal(b, cieLab.B);
    }

    [Fact]
    public void CieLabEquality()
    {
        CieLab x = default;
        CieLab y = new(Vector3.One);

        Assert.True(default == default(CieLab));
        Assert.True(new CieLab(1, 0, 1) != default);
        Assert.False(new CieLab(1, 0, 1) == default);
        Assert.Equal(default, default(CieLab));
        Assert.Equal(new CieLab(1, 0, 1), new CieLab(1, 0, 1));
        Assert.Equal(new CieLab(Vector3.One), new CieLab(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(new CieLab(1, 0, 1) == default);
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
