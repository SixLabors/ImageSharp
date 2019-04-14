// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class ResizeTests
    {
        private const PixelTypes CommonNonDefaultPixelTypes =
            PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        private const PixelTypes DefaultPixelType = PixelTypes.Rgba32;

        public static readonly string[] AllResamplerNames = TestUtils.GetAllResamplerNames();

        public static readonly string[] CommonTestImages = { TestImages.Png.CalliphoraPartial };

        public static readonly string[] SmokeTestResamplerNames =
            {
                nameof(KnownResamplers.NearestNeighbor), nameof(KnownResamplers.Bicubic), nameof(KnownResamplers.Box),
                nameof(KnownResamplers.Lanczos5),
            };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.07F);

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void BicubicWindowOscillatesCorrectly(float x, float expected)
        {
            IResampler sampler = KnownResamplers.Bicubic;
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void Lanczos3WindowOscillatesCorrectly(float x, float expected)
        {
            IResampler sampler = KnownResamplers.Lanczos3;
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-4, 0)]
        [InlineData(-2, 0)]
        [InlineData(0, 1)]
        [InlineData(2, 0)]
        [InlineData(4, 0)]
        public static void Lanczos5WindowOscillatesCorrectly(float x, float expected)
        {
            IResampler sampler = KnownResamplers.Lanczos5;
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void TriangleWindowOscillatesCorrectly(float x, float expected)
        {
            IResampler sampler = KnownResamplers.Triangle;
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [WithBasicTestPatternImages(15, 12, PixelTypes.Rgba32, 2, 3, 1, 2)]
        [WithBasicTestPatternImages(2, 256, PixelTypes.Rgba32, 1, 1, 1, 8)]
        [WithBasicTestPatternImages(2, 32, PixelTypes.Rgba32, 1, 1, 1, 2)]
        public void Resize_BasicSmall<TPixel>(TestImageProvider<TPixel> provider, int wN, int wD, int hN, int hD)
            where TPixel : struct, IPixel<TPixel>
        {
            // Basic test case, very helpful for debugging
            // [WithBasicTestPatternImages(15, 12, PixelTypes.Rgba32, 2, 3, 1, 2)] means:
            // resizing: (15, 12) -> (10, 6)
            // kernel dimensions: (3, 4)
            

            using (Image<TPixel> image = provider.GetImage())
            {
                var destSize = new Size(image.Width * wN / wD, image.Height * hN / hD);
                image.Mutate(x => x.Resize(destSize, KnownResamplers.Bicubic, false));
                FormattableString outputInfo = $"({wN}÷{wD},{hN}÷{hD})";
                image.DebugSave(provider, outputInfo, appendPixelTypeToFileName: false);
                image.CompareToReferenceOutput(provider, outputInfo, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, DefaultPixelType)]
        public void Resize_Compand<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(image.Size() / 2, true));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Kaboom, DefaultPixelType, false)]
        [WithFile(TestImages.Png.Kaboom, DefaultPixelType, true)]
        public void Resize_DoesNotBleedAlphaPixels<TPixel>(TestImageProvider<TPixel> provider, bool compand)
            where TPixel : struct, IPixel<TPixel>
        {
            string details = compand ? "Compand" : "";

            provider.RunValidatingProcessorTest(
                x => x.Resize(x.GetCurrentSize() / 2, compand),
                details,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, DefaultPixelType)]
        public void Resize_IsAppliedToAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2, KnownResamplers.Bicubic));

                // Comparer fights decoder with gif-s. Could not use CompareToReferenceOutput here :(
                image.DebugSave(provider, extension: "gif");
            }
        }

        [Theory]
        [WithTestPatternImages(50, 50, CommonNonDefaultPixelTypes)]
        public void Resize_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Resize(x.GetCurrentSize() / 2), comparer: ValidatorComparer);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void Resize_ThrowsForWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image0 = provider.GetImage())
            {
                var mmg = TestMemoryManager<TPixel>.CreateAsCopyOf(image0.GetPixelSpan());

                using (var image1 = Image.WrapMemory(mmg.Memory, image0.Width, image0.Height))
                {
                    Assert.ThrowsAny<Exception>(
                        () => { image1.Mutate(x => x.Resize(image0.Width / 2, image0.Height / 2, true)); });
                }
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 1)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 4)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 8)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, -1)]
        public void Resize_WorksWithAllParallelismLevels<TPixel>(
            TestImageProvider<TPixel> provider,
            int maxDegreeOfParallelism)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.Configuration.MaxDegreeOfParallelism =
                maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;

            FormattableString details = $"MDP{maxDegreeOfParallelism}";

            provider.RunValidatingProcessorTest(
                x => x.Resize(x.GetCurrentSize() / 2),
                details,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(AllResamplerNames), DefaultPixelType, 0.5f, null, null)]
        [WithFileCollection(
            nameof(CommonTestImages),
            nameof(SmokeTestResamplerNames),
            DefaultPixelType,
            0.3f,
            null,
            null)]
        [WithFileCollection(
            nameof(CommonTestImages),
            nameof(SmokeTestResamplerNames),
            DefaultPixelType,
            1.8f,
            null,
            null)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 100, 100, DefaultPixelType, 0.5f, null, null)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 100, 100, DefaultPixelType, 1f, null, null)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 50, 50, DefaultPixelType, 8f, null, null)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 201, 199, DefaultPixelType, null, 100, 99)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 301, 1180, DefaultPixelType, null, 300, 480)]
        [WithTestPatternImages(nameof(SmokeTestResamplerNames), 49, 80, DefaultPixelType, null, 301, 100)]
        public void Resize_WorksWithAllResamplers<TPixel>(
            TestImageProvider<TPixel> provider,
            string samplerName,
            float? ratio,
            int? specificDestWidth,
            int? specificDestHeight)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler sampler = TestUtils.GetResampler(samplerName);

            // NeirestNeighbourResampler is producing slightly different results With classic .NET framework on 32bit
            // most likely because of differences in numeric behavior.
            // The difference is well visible when comparing output for
            // Resize_WorksWithAllResamplers_TestPattern301x1180_NearestNeighbor-300x480.png
            // TODO: Should we investigate this?
            bool allowHigherInaccuracy = !TestEnvironment.Is64BitProcess
                                         && string.IsNullOrEmpty(TestEnvironment.NetCoreVersion)
                                         && sampler is NearestNeighborResampler;

            var comparer = ImageComparer.TolerantPercentage(allowHigherInaccuracy ? 0.3f : 0.017f);

            provider.RunValidatingProcessorTest(
                ctx =>
                    {
                        SizeF newSize;
                        string destSizeInfo;
                        if (ratio.HasValue)
                        {
                            newSize = ctx.GetCurrentSize() * ratio.Value;
                            destSizeInfo = ratio.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            if (!specificDestWidth.HasValue || !specificDestHeight.HasValue)
                            {
                                throw new InvalidOperationException(
                                    "invalid dimensional input for Resize_WorksWithAllResamplers!");
                            }

                            newSize = new SizeF(specificDestWidth.Value, specificDestHeight.Value);
                            destSizeInfo = $"{newSize.Width}x{newSize.Height}";
                        }

                        FormattableString testOutputDetails = $"{samplerName}-{destSizeInfo}";
                        ctx.Apply(
                            img => img.DebugSave(
                                provider,
                                $"{testOutputDetails}-ORIGINAL",
                                appendPixelTypeToFileName: false));
                        ctx.Resize((Size)newSize, sampler, false);
                        return testOutputDetails;
                    },
                comparer,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeFromSourceRectangle<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var sourceRectangle = new Rectangle(
                    image.Width / 8,
                    image.Height / 8,
                    image.Width / 4,
                    image.Height / 4);
                var destRectangle = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(
                    x => x.Resize(
                        image.Width,
                        image.Height,
                        KnownResamplers.Bicubic,
                        sourceRectangle,
                        destRectangle,
                        false));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeHeightAndKeepAspect<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(0, image.Height / 3, false));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithTestPatternImages(10, 100, DefaultPixelType)]
        public void ResizeHeightCannotKeepAspectKeepsOnePixel<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(0, 5));
                Assert.Equal(1, image.Width);
                Assert.Equal(5, image.Height);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWidthAndKeepAspect<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(image.Width / 3, 0, false));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithTestPatternImages(100, 10, DefaultPixelType)]
        public void ResizeWidthCannotKeepAspectKeepsOnePixel<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Resize(5, 0));
                Assert.Equal(5, image.Width);
                Assert.Equal(1, image.Height);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithBoxPadMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions
                                  {
                                      Size = new Size(image.Width + 200, image.Height + 200), Mode = ResizeMode.BoxPad
                                  };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithCropHeightMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions { Size = new Size(image.Width, image.Height / 2) };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithCropWidthMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions { Size = new Size(image.Width / 2, image.Height) };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithMaxMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions { Size = new Size(300, 300), Mode = ResizeMode.Max };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithMinMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions
                                  {
                                      Size = new Size(
                                          (int)Math.Round(image.Width * .75F),
                                          (int)Math.Round(image.Height * .95F)),
                                      Mode = ResizeMode.Min
                                  };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithPadMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions
                                  {
                                      Size = new Size(image.Width + 200, image.Height), Mode = ResizeMode.Pad
                                  };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeWithStretchMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions
                                  {
                                      Size = new Size(image.Width / 2, image.Height), Mode = ResizeMode.Stretch
                                  };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }
    }
}