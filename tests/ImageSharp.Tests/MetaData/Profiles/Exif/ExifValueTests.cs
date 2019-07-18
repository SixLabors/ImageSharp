// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifValueTests
    {
        private static ExifValue GetExifValue()
        {
            ExifProfile profile;
            using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image())
            {
                profile = image.Metadata.ExifProfile;
            }

            Assert.NotNull(profile);

            return profile.Values.First();
        }

        [Fact]
        public void IEquatable()
        {
            ExifValue first = GetExifValue();
            ExifValue second = GetExifValue();

            Assert.True(first == second);
            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object)second));
        }

        [Fact]
        public void Properties()
        {
            ExifValue value = GetExifValue();

            Assert.Equal(ExifDataType.Ascii, value.DataType);
            Assert.Equal(ExifTag.GPSDOP, value.Tag);
            Assert.False(value.IsArray);
            Assert.Equal("Windows Photo Editor 10.0.10011.16384", value.ToString());
            Assert.Equal("Windows Photo Editor 10.0.10011.16384", value.Value);
        }
    }
}
