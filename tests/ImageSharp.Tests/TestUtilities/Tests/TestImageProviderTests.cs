// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

using System.Collections.Concurrent;
using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.Memory;

    public class TestImageProviderTests
    {
        public TestImageProviderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        public void NoOutputSubfolderIsPresentByDefault<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Empty(provider.Utility.OutputSubfolderName);
        }

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
        [WithBlankImages(1, 1, PixelTypes.Argb32, PixelTypes.Argb32)]
        public void PixelType_PropertyValueIsCorrect<TPixel>(TestImageProvider<TPixel> provider, PixelTypes expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Equal(expected, provider.PixelType);
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

        private class TestDecoder : IImageDecoder
        {
            public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
                where TPixel : struct, IPixel<TPixel>
            {
                invocationCounts[this.callerName]++;
                return new Image<TPixel>(42, 42);
            }

            // Couldn't make xUnit happy without this hackery:

            private static ConcurrentDictionary<string, int> invocationCounts = new ConcurrentDictionary<string, int>();

            private string callerName = null;

            internal void InitCaller(string name)
            {
                this.callerName = name;
                invocationCounts[name] = 0;
            }

            internal static int GetInvocationCount(string callerName) => invocationCounts[callerName];

            private static readonly object Monitor = new object();

            public static void DoTestThreadSafe(Action action)
            {
                lock (Monitor)
                {
                    action();
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParameterlessDecoder_ShouldUtilizeCache<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // We don't cache with the 32 bit build.
                return;
            }

            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoder.DoTestThreadSafe(() =>
            {
                string testName = nameof(this.GetImage_WithCustomParameterlessDecoder_ShouldUtilizeCache);

                var decoder = new TestDecoder();
                decoder.InitCaller(testName);

                provider.GetImage(decoder);
                Assert.Equal(1, TestDecoder.GetInvocationCount(testName));

                provider.GetImage(decoder);
                Assert.Equal(1, TestDecoder.GetInvocationCount(testName));
            });
        }

        private class TestDecoderWithParameters : IImageDecoder
        {
            public string Param1 { get; set; }

            public int Param2 { get; set; }

            public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
                where TPixel : struct, IPixel<TPixel>
            {
                invocationCounts[this.callerName]++;
                return new Image<TPixel>(42, 42);
            }

            private static ConcurrentDictionary<string, int> invocationCounts = new ConcurrentDictionary<string, int>();

            private string callerName = null;

            internal void InitCaller(string name)
            {
                this.callerName = name;
                invocationCounts[name] = 0;
            }

            internal static int GetInvocationCount(string callerName) => invocationCounts[callerName];

            private static readonly object Monitor = new object();

            public static void DoTestThreadSafe(Action action)
            {
                lock (Monitor)
                {
                    action();
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParametricDecoder_ShouldUtilizeCache_WhenParametersAreEqual<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // We don't cache with the 32 bit build.
                return;
            }

            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoderWithParameters.DoTestThreadSafe(() =>
            {
                string testName =
                    nameof(this.GetImage_WithCustomParametricDecoder_ShouldUtilizeCache_WhenParametersAreEqual);

                var decoder1 = new TestDecoderWithParameters() { Param1 = "Lol", Param2 = 666 };
                decoder1.InitCaller(testName);

                var decoder2 = new TestDecoderWithParameters() { Param1 = "Lol", Param2 = 666 };
                decoder2.InitCaller(testName);

                provider.GetImage(decoder1);
                Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));

                provider.GetImage(decoder2);
                Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));
            });
        }

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParametricDecoder_ShouldNotUtilizeCache_WhenParametersAreNotEqual<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoderWithParameters.DoTestThreadSafe(() =>
            {
                string testName =
                    nameof(this.GetImage_WithCustomParametricDecoder_ShouldNotUtilizeCache_WhenParametersAreNotEqual);

                var decoder1 = new TestDecoderWithParameters() { Param1 = "Lol", Param2 = 42 };
                decoder1.InitCaller(testName);

                var decoder2 = new TestDecoderWithParameters() { Param1 = "LoL", Param2 = 42 };
                decoder2.InitCaller(testName);

                provider.GetImage(decoder1);
                Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));

                provider.GetImage(decoder2);
                Assert.Equal(2, TestDecoderWithParameters.GetInvocationCount(testName));
            });
        }


        public static string[] AllBmpFiles =
            {
                TestImages.Bmp.F,
                TestImages.Bmp.Bit8
            };

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithFileCollection<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "png");
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void SaveTestOutputFileMultiFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                string[] files = provider.Utility.SaveTestOutputFileMultiFrame(image);

                Assert.True(files.Length > 2);
                foreach (string path in files)
                {
                    this.Output.WriteLine(path);
                    Assert.True(File.Exists(path));
                }
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 20, 255, 100, 50, 200, PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithSolidFilledImagesAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(10, img.Width);
            Assert.Equal(20, img.Height);

            var rgba = default(Rgba32);

            Buffer2D<TPixel> pixels = img.GetRootFramePixelBuffer();
            for (int y = 0; y < pixels.Height; y++)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    pixels[x, y].ToRgba32(ref rgba);

                    Assert.Equal(255, rgba.R);
                    Assert.Equal(100, rgba.G);
                    Assert.Equal(50, rgba.B);
                    Assert.Equal(200, rgba.A);
                }
            }
        }

        /// <summary>
        /// Need to us <see cref="GenericFactory{TPixel}"/> to create instance of <see cref="Image"/> when pixelType is StandardImageClass
        /// </summary>
        /// <typeparam name="TPixel"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static Image<TPixel> CreateTestImage<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new Image<TPixel>(3, 3);
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All)]
        public void Use_WithMemberFactoryAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(3, img.Width);
            if (provider.PixelType == PixelTypes.Rgba32)
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
            TestImageProvider<Rgba32>.File(TestImages.Bmp.Car),
            TestImageProvider<HalfVector4>.File(TestImages.Bmp.F)
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
