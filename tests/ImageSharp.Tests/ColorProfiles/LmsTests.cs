// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Lms"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class LmsTests
{
    [Fact]
    public void LmsConstructorAssignsFields()
    {
        const float l = 75F;
        const float m = -64F;
        const float s = 87F;
        Lms lms = new(l, m, s);

        Assert.Equal(l, lms.L);
        Assert.Equal(m, lms.M);
        Assert.Equal(s, lms.S);
    }

    [Fact]
    public void LmsEquality()
    {
        Lms x = default;
        Lms y = new(Vector3.One);

        Assert.True(default == default(Lms));
        Assert.True(new Lms(1, 0, 1) != default);
        Assert.False(new Lms(1, 0, 1) == default);
        Assert.Equal(default, default(Lms));
        Assert.Equal(new Lms(1, 0, 1), new Lms(1, 0, 1));
        Assert.Equal(new Lms(Vector3.One), new Lms(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
