// <copyright file="ImageEqualTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;

    public class ImageEqualTests
    {
        [Fact]
        public void TestsThatVimImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.VimImage1, TestImages.Png.VimImage2);
        }

        [Fact]
        public void TestsThatVersioningImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.VersioningImage1, TestImages.Png.VersioningImage2);
        }

        [Fact]
        public void TestsThatResizeFromSourceRectangleImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.ResizeFromSourceRectangle_Rgba32_CalliphoraPartial1, TestImages.Png.ResizeFromSourceRectangle_Rgba32_CalliphoraPartial2);
        }

        [Fact]
        public void TestsThatResizeWithBoxPadModeImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.ResizeWithBoxPadMode_Rgba32_CalliphoraPartial1, TestImages.Png.ResizeWithBoxPadMode_Rgba32_CalliphoraPartial2);
        }

        [Fact]
        public void TestsThatResizeWithPadModeImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.ResizeWithPadMode_Rgba32_CalliphoraPartial1, TestImages.Png.ResizeWithPadMode_Rgba32_CalliphoraPartial2);
        }

        [Fact]
        public void TestsThatSnakeImagesAreEqual()
        {
            AssertImagesEqual(TestImages.Png.Snake1, TestImages.Png.Snake2);
        }

        private void AssertImagesEqual(string img1string, string img2string)
        {
            var image1Provider = TestImageProvider<Rgba32>.File(img1string);
            var image2Provider = TestImageProvider<Rgba32>.File(img2string);

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
