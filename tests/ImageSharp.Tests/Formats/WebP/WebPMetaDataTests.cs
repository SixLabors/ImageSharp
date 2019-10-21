// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.WebP;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using static SixLabors.ImageSharp.Tests.TestImages.WebP;
    using static TestImages.Bmp;

    public class WebPMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            /*TODO:
            var meta = new WebPMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel24 };
            var clone = (WebPMetadata)meta.DeepClone();

            clone.BitsPerPixel = BmpBitsPerPixel.Pixel32;

            Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));*/
        }

        [Theory]
        [InlineData(Lossless.Lossless1, BmpInfoHeaderType.WinVersion2)]
        public void Identify_DetectsCorrectBitmapInfoHeaderType(string imagePath, BmpInfoHeaderType expectedInfoHeaderType)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(24, imageInfo.PixelType.BitsPerPixel);
                //var webpMetaData = imageInfo.Metadata.GetFormatMetadata(WebPFormat.Instance);
                //Assert.NotNull(webpMetaData);
                //Assert.Equal(expectedInfoHeaderType, webpMetaData.InfoHeaderType);
            }
        }
    }
}
