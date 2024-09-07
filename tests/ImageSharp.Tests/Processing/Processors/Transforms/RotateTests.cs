// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class RotateTests
{
    public static readonly TheoryData<float> RotateAngles
        = new()
        {
        50, -50, 170, -170
    };

    public static readonly TheoryData<RotateMode> RotateEnumValues
        = new()
        {
        RotateMode.None,
        RotateMode.Rotate90,
        RotateMode.Rotate180,
        RotateMode.Rotate270
    };

    [Theory]
    [WithTestPatternImages(nameof(RotateAngles), 100, 50, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(RotateAngles), 50, 100, PixelTypes.Rgba32)]
    public void Rotate_WithAngle<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(ctx => ctx.Rotate(value), value, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithTestPatternImages(nameof(RotateEnumValues), 100, 50, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(RotateEnumValues), 50, 100, PixelTypes.Rgba32)]
    public void Rotate_WithRotateTypeEnum<TPixel>(TestImageProvider<TPixel> provider, RotateMode value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(ctx => ctx.Rotate(value), value, appendPixelTypeToFileName: false);
    }
}
