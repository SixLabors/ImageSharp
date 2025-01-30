// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="HunterLab"/> struct.
/// </summary>
public class HunterLabTests
{
    [Fact]
    public void HunterLabConstructorAssignsFields()
    {
        const float l = 75F;
        const float a = -64F;
        const float b = 87F;
        HunterLab hunterLab = new(l, a, b);

        Assert.Equal(l, hunterLab.L);
        Assert.Equal(a, hunterLab.A);
        Assert.Equal(b, hunterLab.B);
    }

    [Fact]
    public void HunterLabEquality()
    {
        HunterLab x = default;
        HunterLab y = new(Vector3.One);

        Assert.True(default == default(HunterLab));
        Assert.True(new HunterLab(1, 0, 1) != default);
        Assert.False(new HunterLab(1, 0, 1) == default);
        Assert.Equal(default, default(HunterLab));
        Assert.Equal(new(1, 0, 1), new HunterLab(1, 0, 1));
        Assert.Equal(new(Vector3.One), new HunterLab(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
