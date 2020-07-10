// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class TransformsHelpersTest
    {
        [Fact]
        public void HelperCanChangeExifDataType()
        {
            int xy = 1;

            using (var img = new Image<A8>(xy, xy))
            {
                var profile = new ExifProfile();
                img.Metadata.ExifProfile = profile;
                profile.SetValue(ExifTag.PixelXDimension, xy + ushort.MaxValue);
                profile.SetValue(ExifTag.PixelYDimension, xy + ushort.MaxValue);

                Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelXDimension).DataType);
                Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelYDimension).DataType);

                TransformProcessorHelpers.UpdateDimensionalMetadata(img);

                Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelXDimension).DataType);
                Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelYDimension).DataType);
            }
        }
    }
}
