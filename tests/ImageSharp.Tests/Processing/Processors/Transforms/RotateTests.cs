// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;

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
        => provider.RunValidatingProcessorTest(ctx => ctx.Rotate(value), value, appendPixelTypeToFileName: false);

    [Theory]
    [WithTestPatternImages(nameof(RotateEnumValues), 100, 50, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(RotateEnumValues), 50, 100, PixelTypes.Rgba32)]
    public void Rotate_WithRotateTypeEnum<TPixel>(TestImageProvider<TPixel> provider, RotateMode value)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunValidatingProcessorTest(ctx => ctx.Rotate(value), value, appendPixelTypeToFileName: false);

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void RotateUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 50, 50]);

        image.Mutate(ctx => ctx.Rotate(180));

        // A 180-degree rotation inverts both axes around the image center.
        // The subject location (5, 15) becomes (imageWidth - 5, imageHeight - 15) = (95, 85)
        Assert.Equal(
            [95, 85],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        // The subject area is also mirrored around the center.
        // New X = imageWidth - originalX - width
        // New Y = imageHeight - originalY - height
        // (5, 15, 50, 50) becomes (45, 35, 50, 50)
        Assert.Equal(
            [45, 35, 50, 50],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }
}
