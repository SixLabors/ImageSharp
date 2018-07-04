// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData.Profiles.Exif;
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

            using (var img = new Image<Alpha8>(xy, xy))
            {
                var profile = new ExifProfile();
                img.MetaData.ExifProfile = profile;
                profile.SetValue(ExifTag.PixelXDimension, (uint)xy);
                profile.SetValue(ExifTag.PixelYDimension, (uint)xy);

                Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelXDimension).DataType);
                Assert.Equal(ExifDataType.Long, profile.GetValue(ExifTag.PixelYDimension).DataType);

                TransformHelpers.UpdateDimensionalMetData(img);

                Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelXDimension).DataType);
                Assert.Equal(ExifDataType.Short, profile.GetValue(ExifTag.PixelYDimension).DataType);
            }
        }
    }
}