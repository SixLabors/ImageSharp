// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class TransformsHelpersTest
{
    [Fact]
    public void HelperCanChangeExifDataType()
    {
        int xy = 1;

        using (var img = new Image<A8>(xy, xy))
        {
            // The metadata setter rejects an empty profile, so add the values before assigning it.
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.PixelXDimension, xy + ushort.MaxValue);
            profile.SetValue(ExifTag.PixelYDimension, xy + ushort.MaxValue);
            img.Metadata.ExifProfile = profile;

            Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelXDimension).DataType);
            Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelYDimension).DataType);

            TransformProcessorHelpers.UpdateDimensionalMetadata(img);

            Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelXDimension).DataType);
            Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelYDimension).DataType);
        }
    }
}
