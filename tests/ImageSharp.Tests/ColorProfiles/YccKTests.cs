// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="YccK"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class YccKTests
{
    [Fact]
    public void YccKConstructorAssignsFields()
    {
        const float y = .75F;
        const float cb = .5F;
        const float cr = .25F;
        const float k = .125F;

        YccK ycckValue = new(y, cb, cr, k);
        Assert.Equal(y, ycckValue.Y);
        Assert.Equal(cb, ycckValue.Cb);
        Assert.Equal(cr, ycckValue.Cr);
        Assert.Equal(k, ycckValue.K);
    }

    [Fact]
    public void YccKEquality()
    {
        YccK x = default;
        YccK y = new(1F, 1F, 1F, 1F);
        Assert.True(default == default(YccK));
        Assert.False(default != default(YccK));
        Assert.Equal(default, default(YccK));
        Assert.Equal(new YccK(1, 1, 1, 1), new YccK(1, 1, 1, 1));
        Assert.Equal(new YccK(.5F, .5F, .5F, .5F), new YccK(.5F, .5F, .5F, .5F));

        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
