// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Bmp;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    using static TestImages.Bmp;

    public class BmpMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel24 };
            var clone = (BmpMetadata)meta.DeepClone();

            clone.BitsPerPixel = BmpBitsPerPixel.Pixel32;

            Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));
        }

        [Theory]
        [InlineData(WinBmpv2, BmpInfoHeaderType.WinVersion2)]
        [InlineData(WinBmpv3, BmpInfoHeaderType.WinVersion3)]
        [InlineData(WinBmpv4, BmpInfoHeaderType.WinVersion4)]
        [InlineData(WinBmpv5, BmpInfoHeaderType.WinVersion5)]
        [InlineData(Os2v2Short, BmpInfoHeaderType.Os2Version2Short)]
        [InlineData(Rgb32h52AdobeV3, BmpInfoHeaderType.AdobeVersion3)]
        [InlineData(Rgba32bf56AdobeV3, BmpInfoHeaderType.AdobeVersion3WithAlpha)]
        [InlineData(Os2v2, BmpInfoHeaderType.Os2Version2)]
        public void Identify_DetectsCorrectBitmapInfoHeaderType(string imagePath, BmpInfoHeaderType expectedInfoHeaderType)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                BmpMetadata bitmapMetaData = imageInfo.Metadata.GetFormatMetadata(BmpFormat.Instance);
                Assert.NotNull(bitmapMetaData);
                Assert.Equal(expectedInfoHeaderType, bitmapMetaData.InfoHeaderType);
            }
        }
    }
}
