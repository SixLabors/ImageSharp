// <copyright file="TestImageFactoryTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests.Tests
{
    using System;

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
        [WithBlankImages(42, 666, PixelTypes.Color | PixelTypes.Argb | PixelTypes.HalfSingle, "hello")]
        public void Use_WithEmptyImageAttribute<TColor>(TestImageProvider<TColor> provider, string message)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.All, "hello")]
        public void Use_WithBlankImagesAttribute_WithAllPixelTypes<TColor>(
            TestImageProvider<TColor> provider,
            string message) 
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Color, PixelTypes.Color)]
        [WithBlankImages(1, 1, PixelTypes.Alpha8, PixelTypes.Alpha8)]
        [WithBlankImages(1, 1, PixelTypes.ColorWithDefaultImageClass, PixelTypes.ColorWithDefaultImageClass)]
        public void PixelType_PropertyValueIsCorrect<TColor>(TestImageProvider<TColor> provider, PixelTypes expected)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Assert.Equal(expected, provider.PixelType);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.ColorWithDefaultImageClass)]
        public void PixelTypes_ColorWithDefaultImageClass_TriggersCreatingTheNonGenericDerivedImageClass<TColor>(
            TestImageProvider<TColor> provider) 
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();

            Assert.IsType<Image>(img);
        }

        // TODO: @dlemstra this works only with constant strings!
        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.All, 88)]
        [WithFile(TestImages.Bmp.F, PixelTypes.All, 88)]
        public void Use_WithFileAttribute<TColor>(TestImageProvider<TColor> provider, int yo)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            var img = provider.GetImage();
            Assert.True(img.Width * img.Height > 0);

            Assert.Equal(88, yo);

            string fn = provider.Utility.GetTestOutputFileName("jpg");
            this.Output.WriteLine(fn);
        }

        public static string[] AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb)]
        public void Use_WithFileCollection<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            var image = provider.GetImage();
            provider.Utility.SaveTestOutputFile(image, "png");
        }

        [Theory]
        [WithSolidFilledImages(10, 20, 255, 100, 50, 200, PixelTypes.Color | PixelTypes.Argb)]
        public void Use_WithSolidFilledImagesAttribute<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();
            Assert.Equal(img.Width, 10);
            Assert.Equal(img.Height, 20);

            byte[] colors = new byte[4];

            using (var pixels = img.Lock())
            {
                for (int y = 0; y < pixels.Height; y++)
                {
                    for (int x = 0; x < pixels.Width; x++)
                    {
                        pixels[x, y].ToBytes(colors, 0, ComponentOrder.XYZW);

                        Assert.Equal(colors[0], 255);
                        Assert.Equal(colors[1], 100);
                        Assert.Equal(colors[2], 50);
                        Assert.Equal(colors[3], 200);
                    }
                }
            }
        }

        /// <summary>
        /// Need to us <see cref="GenericFactory{TColor}"/> to create instance of <see cref="Image"/> when pixelType is ColorWithDefaultImageClass
        /// </summary>
        /// <typeparam name="TColor"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static Image<TColor> CreateTestImage<TColor>(GenericFactory<TColor> factory) 
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return factory.CreateImage(3, 3);
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All)]
        public void Use_WithMemberFactoryAttribute<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();
            Assert.Equal(img.Width, 3);
            if (provider.PixelType == PixelTypes.ColorWithDefaultImageClass)
            {
                Assert.IsType<Image>(img);
            }

        }

        public static readonly TheoryData<ITestImageFactory> BasicData = new TheoryData<ITestImageFactory>()
                                                                             {
                                                                                 TestImageProvider<Color>.Blank(10, 20),
                                                                                 TestImageProvider<HalfVector4>.Blank(
                                                                                     10,
                                                                                     20)
                                                                             };

        [Theory]
        [MemberData(nameof(BasicData))]
        public void Blank_MemberData<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }

        public static readonly TheoryData<ITestImageFactory> FileData = new TheoryData<ITestImageFactory>()
                                                                            {
                                                                                TestImageProvider<Color>.File(
                                                                                    TestImages.Bmp.Car),
                                                                                TestImageProvider<HalfVector4>.File(
                                                                                    TestImages.Bmp.F)
                                                                            };

        [Theory]
        [MemberData(nameof(FileData))]
        public void File_MemberData<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            this.Output.WriteLine("SRC: " + provider.Utility.SourceFileOrDescription);
            this.Output.WriteLine("OUT: " + provider.Utility.GetTestOutputFileName());

            var img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }
    }
}