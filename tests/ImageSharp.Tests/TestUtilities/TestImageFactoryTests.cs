// <copyright file="TestImageFactoryTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.IO;

    using Xunit;
    using Xunit.Abstractions;

    public class TestImageFactoryTests
    {
      
        public TestImageFactoryTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }


        [Theory]
        [WithBlankImages(42, 666, PixelTypes.Color | PixelTypes.Argb | PixelTypes.HalfSingle, "hello")]
        public void Use_WithEmptyImageAttribute<TColor, TPacked>(
            TestImageFactory<TColor, TPacked> factory,
            string message) 
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var img = factory.Create();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.All, "hello")]
        public void Use_WithBlankImagesAttribute_WithAllPixelTypes<TColor, TPacked>(
            TestImageFactory<TColor, TPacked> factory,
            string message) 
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var img = factory.Create();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        // TODO: @dlemstra this works only with constant strings!
        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.All, 88)]
        [WithFile(TestImages.Bmp.F, PixelTypes.All, 88)]
        public void Use_WithFileAttribute<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory, int yo)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            Assert.NotNull(factory.Utility.SourceFileOrDescription);
            var img = factory.Create();
            Assert.True(img.Width * img.Height > 0);

            Assert.Equal(88, yo);

            string fn = factory.Utility.GetTestOutputFileName("jpg");
            this.Output.WriteLine(fn);
        }

        public static string[] AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.Argb)]
        public void Use_WithFileCollection<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            Assert.NotNull(factory.Utility.SourceFileOrDescription);
            var image = factory.Create();
            factory.Utility.SaveTestOutputFile(image, "png");
        }
        
        [Theory]
        [WithSolidFilledImages(10, 20, 255, 100, 50, 200, PixelTypes.Color | PixelTypes.Argb)]
        public void Use_WithSolidFilledImagesAttribute<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var img = factory.Create();
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

        public static Image<TColor, TPacked> TestMemberFactory<TColor, TPacked>()
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            return new Image<TColor, TPacked>(3, 3);
        }

        [Theory]
        [WithMemberFactory(nameof(TestMemberFactory), PixelTypes.All)]
        public void Use_WithMemberFactoryAttribute<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var img = factory.Create();
            Assert.Equal(img.Width, 3);
        }


        public static readonly TheoryData<ITestImageFactory> BasicData = new TheoryData<ITestImageFactory>()
                                                                             {
                                                                                 TestImageFactory<Color, uint>.Blank(10, 20),
                                                                                 TestImageFactory<HalfVector4, ulong>.Blank(
                                                                                     10,
                                                                                     20)
                                                                             };


        [Theory]
        [MemberData(nameof(BasicData))]
        public void Blank_MemberData<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var img = factory.Create();

            Assert.True(img.Width * img.Height > 0);
        }

        public static readonly TheoryData<ITestImageFactory> FileData = new TheoryData<ITestImageFactory>()
                                                                            {
                                                                                TestImageFactory<Color, uint>.File(
                                                                                    TestImages.Bmp.Car),
                                                                                TestImageFactory<HalfVector4, ulong>.File(
                                                                                    TestImages.Bmp.F)
                                                                            };

        [Theory]
        [MemberData(nameof(FileData))]
        public void File_MemberData<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            this.Output.WriteLine("SRC: " + factory.Utility.SourceFileOrDescription);
            this.Output.WriteLine("OUT: " + factory.Utility.GetTestOutputFileName());

            var img = factory.Create();

            Assert.True(img.Width * img.Height > 0);
        }
    }
}