// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Hsv"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class HsvTests
{
    [Fact]
    public void HsvConstructorAssignsFields()
    {
        const float h = 275F;
        const float s = .64F;
        const float v = .87F;
        Hsv hsv = new(h, s, v);

        Assert.Equal(h, hsv.H);
        Assert.Equal(s, hsv.S);
        Assert.Equal(v, hsv.V);
    }

    [Fact]
    public void HsvEquality()
    {
        Hsv x = default;
        Hsv y = new(Vector3.One);

        Assert.True(default == default(Hsv));
        Assert.False(default != default(Hsv));
        Assert.Equal(default, default(Hsv));
        Assert.Equal(new(1, 0, 1), new Hsv(1, 0, 1));
        Assert.Equal(new(Vector3.One), new Hsv(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
