// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.


// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Y"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class YTests
{
    [Fact]
    public void YConstructorAssignsFields()
    {
        const float y = .75F;
        Y yValue = new(y);

        Assert.Equal(y, yValue.L);
    }

    [Fact]
    public void YEquality()
    {
        Y x = default;
        Y y = new(1F);
        Assert.True(default == default(Y));
        Assert.False(default != default(Y));
        Assert.Equal(default, default(Y));
        Assert.Equal(new Y(1), new Y(1));

        Assert.Equal(new Y(.5F), new Y(.5F));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }
}
