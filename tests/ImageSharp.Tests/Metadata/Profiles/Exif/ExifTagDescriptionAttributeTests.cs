// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifTagDescriptionAttributeTests
    {
        [Fact]
        public void TestExifTag()
        {
            var exifProfile = new ExifProfile();

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)1);
            IExifValue value = exifProfile.GetValue(ExifTag.ResolutionUnit);
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

            exifProfile.SetValue(ExifTag.ImageWidth, 123U);
            value = exifProfile.GetValue(ExifTag.ImageWidth);
            Assert.Equal("123", value.ToString());
        }
    }
}
