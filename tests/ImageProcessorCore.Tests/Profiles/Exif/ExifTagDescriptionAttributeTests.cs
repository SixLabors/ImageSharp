// <copyright file="ExifProfileTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Xunit;

    public class ExifDescriptionAttributeTests
    {
        [Fact]
        public void Test_ExifTag()
        {
            var exifProfile = new ExifProfile();

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)1);
            ExifValue value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("None", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)2);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("Inches", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)3);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("Cm", value.ToString());

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)4);
            value = exifProfile.GetValue(ExifTag.ResolutionUnit);
            Assert.Equal("4", value.ToString());

            exifProfile.SetValue(ExifTag.ImageWidth, 123);
            value = exifProfile.GetValue(ExifTag.ImageWidth);
            Assert.Equal("123", value.ToString());
        }
    }
}
