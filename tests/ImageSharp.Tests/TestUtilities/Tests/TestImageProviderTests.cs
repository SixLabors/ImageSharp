// <copyright file="TestImageFactoryTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    using ImageSharp.PixelFormats;

    using Xunit;
    using Xunit.Abstractions;

    public class TestImageProviderTests
    {
        public TestImageProviderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.HalfSingle, "hello")]
        public void Use_WithEmptyImageAttribute<TPixel>(TestImageProvider<TPixel> provider, string message)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.All, "hello")]
        public void Use_WithBlankImagesAttribute_WithAllPixelTypes<TPixel>(
            TestImageProvider<TPixel> provider,
            string message)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32, PixelTypes.Rgba32)]
        [WithBlankImages(1, 1, PixelTypes.Alpha8, PixelTypes.Alpha8)]
        [WithBlankImages(1, 1, PixelTypes.StandardImageClass, PixelTypes.StandardImageClass)]
        public void PixelType_PropertyValueIsCorrect<TPixel>(TestImageProvider<TPixel> provider, PixelTypes expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Equal(expected, provider.PixelType);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.StandardImageClass)]
        [WithFile(TestImages.Bmp.F, PixelTypes.StandardImageClass)]
        public void PixelTypes_ColorWithDefaultImageClass_TriggersCreatingTheNonGenericDerivedImageClass<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.IsType<Image<Rgba32>>(img);
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.All, 88)]
        [WithFile(TestImages.Bmp.F, PixelTypes.All, 88)]
        public void Use_WithFileAttribute<TPixel>(TestImageProvider<TPixel> provider, int yo)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            Image<TPixel> img = provider.GetImage();
            Assert.True(img.Width * img.Height > 0);

            Assert.Equal(88, yo);

            string fn = provider.Utility.GetTestOutputFileName("jpg");
            this.Output.WriteLine(fn);
        }

        public static string[] AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithFileCollection<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            Image<TPixel> image = provider.GetImage();
            provider.Utility.SaveTestOutputFile(image, "png");
        }

        [Theory]
        [WithSolidFilledImages(10, 20, 255, 100, 50, 200, PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithSolidFilledImagesAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(img.Width, 10);
            Assert.Equal(img.Height, 20);

            byte[] colors = new byte[4];

            using (PixelAccessor<TPixel> pixels = img.Lock())
            {
                for (int y = 0; y < pixels.Height; y++)
                {
                    for (int x = 0; x < pixels.Width; x++)
                    {
                        pixels[x, y].ToXyzwBytes(colors, 0);

                        Assert.Equal(colors[0], 255);
                        Assert.Equal(colors[1], 100);
                        Assert.Equal(colors[2], 50);
                        Assert.Equal(colors[3], 200);
                    }
                }
            }
        }

        /// <summary>
        /// Need to us <see cref="GenericFactory{TPixel}"/> to create instance of <see cref="Image"/> when pixelType is StandardImageClass
        /// </summary>
        /// <typeparam name="TPixel"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static Image<TPixel> CreateTestImage<TPixel>(GenericFactory<TPixel> factory)
            where TPixel : struct, IPixel<TPixel>
        {
            return factory.CreateImage(3, 3);
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All)]
        public void Use_WithMemberFactoryAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(img.Width, 3);
            if (provider.PixelType == PixelTypes.StandardImageClass)
            {
                Assert.IsType<Image<Rgba32>>(img);
            }

        }

        public static readonly TheoryData<object> BasicData = new TheoryData<object>()
                                                                             {
                                                                                 TestImageProvider<Rgba32>.Blank(10, 20),
                                                                                 TestImageProvider<HalfVector4>.Blank(
                                                                                     10,
                                                                                     20),
                                                                             };

        [Theory]
        [MemberData(nameof(BasicData))]
        public void Blank_MemberData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }

        public static readonly TheoryData<object> FileData = new TheoryData<object>()
                                                                            {
                                                                                TestImageProvider<Rgba32>.File(
                                                                                    TestImages.Bmp.Car),
                                                                                TestImageProvider<HalfVector4>.File(
                                                                                    TestImages.Bmp.F)
                                                                            };

        [Theory]
        [MemberData(nameof(FileData))]
        public void File_MemberData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.Output.WriteLine("SRC: " + provider.Utility.SourceFileOrDescription);
            this.Output.WriteLine("OUT: " + provider.Utility.GetTestOutputFileName());

            Image<TPixel> img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }
    }
}