// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageEqualTests
    {
        [Fact]
        public void TestsThatVimImagesAreEqual()
        {
            var image1Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VimImage1);
            var image2Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VimImage2);

            using (Image<Rgba32> img1 = image1Provider.GetImage())
            using (Image<Rgba32> img2 = image2Provider.GetImage())
            {
                bool imagesEqual = AreImagesEqual(img1, img2);
                Assert.True(imagesEqual);
            }
        }

        [Fact]
        public void TestsThatVersioningImagesAreEqual()
        {
            var image1Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VersioningImage1);
            var image2Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VersioningImage2);

            using (Image<Rgba32> img1 = image1Provider.GetImage())
            using (Image<Rgba32> img2 = image2Provider.GetImage())
            {
                bool imagesEqual = AreImagesEqual(img1, img2);
                Assert.True(imagesEqual);
            }
        }

        private bool AreImagesEqual(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            Assert.Equal(img1.Width, img2.Width);
            Assert.Equal(img1.Height, img2.Height);

            for (int y = 0; y < img1.Height; y++)
            {
                for (int x = 0; x < img1.Width; x++)
                {
                    Rgba32 pixel1 = img1[x, y];
                    Rgba32 pixel2 = img2[x, y];

                    if (pixel1 != pixel2)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
