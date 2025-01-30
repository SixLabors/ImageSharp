// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Cmyk"/> struct.
/// </summary>
public class CmykTests
{
    [Fact]
    public void CmykConstructorAssignsFields()
    {
        const float c = .75F;
        const float m = .64F;
        const float y = .87F;
        const float k = .334F;
        Cmyk cmyk = new(c, m, y, k);

        Assert.Equal(c, cmyk.C);
        Assert.Equal(m, cmyk.M);
        Assert.Equal(y, cmyk.Y);
        Assert.Equal(k, cmyk.K);
    }

    [Fact]
    public void CmykEquality()
    {
        Cmyk x = default;
        Cmyk y = new(Vector4.One);

        Assert.True(default == default(Cmyk));
        Assert.False(default != default(Cmyk));
        Assert.Equal(default, default(Cmyk));
        Assert.Equal(new(1, 0, 1, 0), new Cmyk(1, 0, 1, 0));
        Assert.Equal(new(Vector4.One), new Cmyk(Vector4.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
