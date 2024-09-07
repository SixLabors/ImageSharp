// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Hsl"/> struct.
/// </summary>
public class HslTests
{
    [Fact]
    public void HslConstructorAssignsFields()
    {
        const float h = 275F;
        const float s = .64F;
        const float l = .87F;
        Hsl hsl = new(h, s, l);

        Assert.Equal(h, hsl.H);
        Assert.Equal(s, hsl.S);
        Assert.Equal(l, hsl.L);
    }

    [Fact]
    public void HslEquality()
    {
        Hsl x = default;
        Hsl y = new(Vector3.One);

        Assert.True(default == default(Hsl));
        Assert.False(default != default(Hsl));
        Assert.Equal(default, default(Hsl));
        Assert.Equal(new(1, 0, 1), new Hsl(1, 0, 1));
        Assert.Equal(new(Vector3.One), new Hsl(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
