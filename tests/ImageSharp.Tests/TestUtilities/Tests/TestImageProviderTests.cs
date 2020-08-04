// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public class TestImageProviderTests
    {
        public static readonly TheoryData<object> BasicData = new TheoryData<object>
        {
            TestImageProvider<Rgba32>.Blank(10, 20),
            TestImageProvider<HalfVector4>.Blank(10, 20),
        };

        public static readonly TheoryData<object> FileData = new TheoryData<object>
        {
            TestImageProvider<Rgba32>.File(TestImages.Bmp.Car),
            TestImageProvider<HalfVector4>.File(TestImages.Bmp.F)
        };

        public static string[] AllBmpFiles = { TestImages.Bmp.F, TestImages.Bmp.Bit8 };

        public TestImageProviderTests(ITestOutputHelper output) => this.Output = output;

        private ITestOutputHelper Output { get; }

        /// <summary>
        /// Need to us <see cref="GenericFactory{TPixel}"/> to create instance of <see cref="Image"/> when pixelType is StandardImageClass
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <returns>A test image.</returns>
        public static Image<TPixel> CreateTestImage<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel> =>
            new Image<TPixel>(3, 3);

        [Theory]
        [MemberData(nameof(BasicData))]
        public void Blank_MemberData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }

        [Theory]
        [MemberData(nameof(FileData))]
        public void File_MemberData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.Output.WriteLine("SRC: " + provider.Utility.SourceFileOrDescription);
            this.Output.WriteLine("OUT: " + provider.Utility.GetTestOutputFileName());

            Image<TPixel> img = provider.GetImage();

            Assert.True(img.Width * img.Height > 0);
        }

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParameterlessDecoder_ShouldUtilizeCache<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // We don't cache with the 32 bit build.
                return;
            }

            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoder.DoTestThreadSafe(
                () =>
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

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParametricDecoder_ShouldNotUtilizeCache_WhenParametersAreNotEqual<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoderWithParameters.DoTestThreadSafe(
                () =>
                    {
                        string testName = nameof(this
                            .GetImage_WithCustomParametricDecoder_ShouldNotUtilizeCache_WhenParametersAreNotEqual);

                        var decoder1 = new TestDecoderWithParameters { Param1 = "Lol", Param2 = 42 };
                        decoder1.InitCaller(testName);

                        var decoder2 = new TestDecoderWithParameters { Param1 = "LoL", Param2 = 42 };
                        decoder2.InitCaller(testName);

                        provider.GetImage(decoder1);
                        Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));

                        provider.GetImage(decoder2);
                        Assert.Equal(2, TestDecoderWithParameters.GetInvocationCount(testName));
                    });
        }

        [Theory]
        [WithFile(TestImages.Bmp.F, PixelTypes.Rgba32)]
        public void GetImage_WithCustomParametricDecoder_ShouldUtilizeCache_WhenParametersAreEqual<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // We don't cache with the 32 bit build.
                return;
            }

            Assert.NotNull(provider.Utility.SourceFileOrDescription);

            TestDecoderWithParameters.DoTestThreadSafe(
                () =>
                    {
                        string testName = nameof(this
                            .GetImage_WithCustomParametricDecoder_ShouldUtilizeCache_WhenParametersAreEqual);

                        var decoder1 = new TestDecoderWithParameters { Param1 = "Lol", Param2 = 666 };
                        decoder1.InitCaller(testName);

                        var decoder2 = new TestDecoderWithParameters { Param1 = "Lol", Param2 = 666 };
                        decoder2.InitCaller(testName);

                        provider.GetImage(decoder1);
                        Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));

                        provider.GetImage(decoder2);
                        Assert.Equal(1, TestDecoderWithParameters.GetInvocationCount(testName));
                    });
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        public void NoOutputSubfolderIsPresentByDefault<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            Assert.Empty(provider.Utility.OutputSubfolderName);

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32, PixelTypes.Rgba32)]
        [WithBlankImages(1, 1, PixelTypes.A8, PixelTypes.A8)]
        [WithBlankImages(1, 1, PixelTypes.Argb32, PixelTypes.Argb32)]
        public void PixelType_PropertyValueIsCorrect<TPixel>(TestImageProvider<TPixel> provider, PixelTypes expected)
            where TPixel : unmanaged, IPixel<TPixel> =>
            Assert.Equal(expected, provider.PixelType);

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void SaveTestOutputFileMultiFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
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
        [WithBasicTestPatternImages(50, 100, PixelTypes.Rgba32)]
        [WithBasicTestPatternImages(49, 17, PixelTypes.Rgba32)]
        [WithBasicTestPatternImages(20, 10, PixelTypes.Rgba32)]
        public void Use_WithBasicTestPatternImages<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                img.DebugSave(provider);
            }
        }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.All, "hello")]
        public void Use_WithBlankImagesAttribute_WithAllPixelTypes<TPixel>(
            TestImageProvider<TPixel> provider,
            string message)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithBlankImages(42, 666, PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.HalfSingle, "hello")]
        public void Use_WithEmptyImageAttribute<TPixel>(TestImageProvider<TPixel> provider, string message)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();

            Assert.Equal(42, img.Width);
            Assert.Equal(666, img.Height);
            Assert.Equal("hello", message);
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.All, 123)]
        [WithFile(TestImages.Bmp.F, PixelTypes.All, 123)]
        public void Use_WithFileAttribute<TPixel>(TestImageProvider<TPixel> provider, int yo)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            using (Image<TPixel> img = provider.GetImage())
            {
                Assert.True(img.Width * img.Height > 0);

                Assert.Equal(123, yo);

                string fn = provider.Utility.GetTestOutputFileName("jpg");
                this.Output.WriteLine(fn);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Testorig420, PixelTypes.Rgba32)]
        public void Use_WithFileAttribute_CustomConfig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            EnsureCustomConfigurationIsApplied(provider);
        }

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithFileCollection<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.NotNull(provider.Utility.SourceFileOrDescription);
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "png");
            }
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All)]
        public void Use_WithMemberFactoryAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(3, img.Width);
            if (provider.PixelType == PixelTypes.Rgba32)
            {
                Assert.IsType<Image<Rgba32>>(img);
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 20, 255, 100, 50, 200, PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void Use_WithSolidFilledImagesAttribute<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> img = provider.GetImage();
            Assert.Equal(10, img.Width);
            Assert.Equal(20, img.Height);

            Buffer2D<TPixel> pixels = img.GetRootFramePixelBuffer();
            Rgba32 rgba = default;
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

        [Theory]
        [WithTestPatternImages(49, 20, PixelTypes.Rgba32)]
        public void Use_WithTestPatternImages<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                img.DebugSave(provider);
            }
        }

        [Theory]
        [WithTestPatternImages(20, 20, PixelTypes.Rgba32)]
        public void Use_WithTestPatternImages_CustomConfiguration<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            EnsureCustomConfigurationIsApplied(provider);
        }

        private static void EnsureCustomConfigurationIsApplied<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (provider.GetImage())
            {
                var customConfiguration = Configuration.CreateDefaultInstance();
                provider.Configuration = customConfiguration;

                using (Image<TPixel> image2 = provider.GetImage())
                using (Image<TPixel> image3 = provider.GetImage())
                {
                    Assert.Same(customConfiguration, image2.GetConfiguration());
                    Assert.Same(customConfiguration, image3.GetConfiguration());
                }
            }
        }

        private class TestDecoder : IImageDecoder
        {
            // Couldn't make xUnit happy without this hackery:
            private static readonly ConcurrentDictionary<string, int> InvocationCounts =
                new ConcurrentDictionary<string, int>();

            private static readonly ConcurrentDictionary<string, int> InvocationCountsAsync =
                new ConcurrentDictionary<string, int>();

            private static readonly object Monitor = new object();

            private string callerName;

            public static void DoTestThreadSafe(Action action)
            {
                lock (Monitor)
                {
                    action();
                }
            }

            public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                InvocationCounts[this.callerName]++;
                return new Image<TPixel>(42, 42);
            }

            public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                InvocationCountsAsync[this.callerName]++;
                return Task.FromResult(new Image<TPixel>(42, 42));
            }

            internal static int GetInvocationCount(string callerName) => InvocationCounts[callerName];

            internal static int GetInvocationCountAsync(string callerName) => InvocationCountsAsync[callerName];

            internal void InitCaller(string name)
            {
                this.callerName = name;
                InvocationCounts[name] = 0;
                InvocationCountsAsync[name] = 0;
            }

            public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

            public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken);
        }

        private class TestDecoderWithParameters : IImageDecoder
        {
            private static readonly ConcurrentDictionary<string, int> InvocationCounts =
                new ConcurrentDictionary<string, int>();

            private static readonly ConcurrentDictionary<string, int> InvocationCountsAsync =
                new ConcurrentDictionary<string, int>();

            private static readonly object Monitor = new object();

            private string callerName;

            public string Param1 { get; set; }

            public int Param2 { get; set; }

            public static void DoTestThreadSafe(Action action)
            {
                lock (Monitor)
                {
                    action();
                }
            }

            public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                InvocationCounts[this.callerName]++;
                return new Image<TPixel>(42, 42);
            }

            public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                InvocationCountsAsync[this.callerName]++;
                return Task.FromResult(new Image<TPixel>(42, 42));
            }

            internal static int GetInvocationCount(string callerName) => InvocationCounts[callerName];

            internal static int GetInvocationCountAsync(string callerName) => InvocationCountsAsync[callerName];

            internal void InitCaller(string name)
            {
                this.callerName = name;
                InvocationCounts[name] = 0;
                InvocationCountsAsync[name] = 0;
            }

            public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

            public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken);
        }
    }
}
