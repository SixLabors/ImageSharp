// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.Various;

[Trait("Profile", "Icc")]
public class IccProfileIdTests
{
    [Fact]
    public void ZeroIsEqualToDefault()
    {
        Assert.True(IccProfileId.Zero.Equals(default));

        Assert.False(default(IccProfileId).IsSet);
    }

    [Fact]
    public void SetIsTrueWhenNonDefaultValue()
    {
        IccProfileId id = new IccProfileId(1, 2, 3, 4);

        Assert.True(id.IsSet);

        Assert.Equal(1u, id.Part1);
        Assert.Equal(2u, id.Part2);
        Assert.Equal(3u, id.Part3);
        Assert.Equal(4u, id.Part4);
    }
}
