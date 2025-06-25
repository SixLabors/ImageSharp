// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="YCbCr"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class YCbCrTests
{
    [Fact]
    public void YCbCrConstructorAssignsFields()
    {
        const float y = .75F;
        const float cb = .64F;
        const float cr = .87F;
        YCbCr yCbCr = new(y, cb, cr);

        Assert.Equal(y, yCbCr.Y);
        Assert.Equal(cb, yCbCr.Cb);
        Assert.Equal(cr, yCbCr.Cr);
    }

    [Fact]
    public void YCbCrEquality()
    {
        YCbCr x = default;
        YCbCr y = new(Vector3.One);

        Assert.True(default == default(YCbCr));
        Assert.False(default != default(YCbCr));
        Assert.Equal(default, default(YCbCr));
        Assert.Equal(new YCbCr(1, 0, 1), new YCbCr(1, 0, 1));
        Assert.Equal(new YCbCr(Vector3.One), new YCbCr(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
