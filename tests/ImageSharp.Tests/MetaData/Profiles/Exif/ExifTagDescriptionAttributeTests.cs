// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifDescriptionAttributeTests
    {
        [Fact]
        public void TestExifTag()
        {
            ExifProfile exifProfile = new ExifProfile();

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)1);
            ExifValue value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("None", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)2);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("Inches", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)3);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("Centimeter", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)4);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("4", value.ToString());

            exifProfile.SetValue(ExifTag.ImageWidth, 123);
            value = exifProfile.GetValue(ExifTag.ImageWidth);
            Assert.Equal("123", value.ToString());
        }
    }
}
