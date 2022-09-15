// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Common;

public class ConstantsTests
{
    [Fact]
    public void Epsilon()
    {
        Assert.Equal(0.001f, Constants.Epsilon);
    }
}
