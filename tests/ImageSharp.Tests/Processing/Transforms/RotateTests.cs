// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class RotateTests : BaseImageOperationsExtensionTest
{
    [Theory]
    [InlineData(85.6f)]
    [InlineData(21)]
    public void RotateDegreesFloatRotateProcessorWithAnglesSet(float angle)
    {
        this.operations.Rotate(angle);
        RotateProcessor processor = this.Verify<RotateProcessor>();

        Assert.Equal(angle, processor.Degrees);
    }

    [Theory]
    [InlineData(RotateMode.None, 0)]
    [InlineData(RotateMode.Rotate90, 90)]
    [InlineData(RotateMode.Rotate180, 180)]
    [InlineData(RotateMode.Rotate270, 270)]
    public void RotateRotateTypeRotateProcessorWithAnglesConvertedFromEnum(RotateMode angle, float expectedAngle)
    {
        this.operations.Rotate(angle); // is this api needed ???
        RotateProcessor processor = this.Verify<RotateProcessor>();

        Assert.Equal(expectedAngle, processor.Degrees);
    }
}
