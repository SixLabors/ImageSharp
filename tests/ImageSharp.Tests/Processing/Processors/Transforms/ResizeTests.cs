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
    public class ResizeTests : FileTestBase
    {
        public static readonly string[] CommonTestImages = { TestImages.Png.CalliphoraPartial };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.07F);

        public static readonly string[] AllResamplerNames = TestUtils.GetAllResamplerNames();

        [Theory]
        [WithTestPatternImages(nameof(AllResamplerNames), 100, 100, DefaultPixelType, 0.5f)]
        [WithFileCollection(nameof(CommonTestImages), nameof(AllResamplerNames), DefaultPixelType, 0.5f)]
        [WithFileCollection(nameof(CommonTestImages), nameof(AllResamplerNames), DefaultPixelType, 0.3f)]
        public void Resize_WorksWithAllResamplers<TPixel>(TestImageProvider<TPixel> provider, string samplerName, float ratio)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler sampler = TestUtils.GetResampler(samplerName);

            using (Image<TPixel> image = provider.GetImage())
            {
                SizeF newSize = image.Size() * ratio;
                image.Mutate(x => x.Resize((Size)newSize, sampler, false));
                FormattableString details = $"{samplerName}-{ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

                image.DebugSave(provider, details);
                image.CompareToReferenceOutput(ImageComparer.TolerantPercentage(0.02f), provider, details);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 1)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 4)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, 8)]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType, -1)]
        public void Resize_WorksWithAllParallelismLevels<TPixel>(TestImageProvider<TPixel> provider, int maxDegreeOfParallelism)
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
                        () =>
                            {
                                image1.Mutate(x => x.Resize(image0.Width / 2, image0.Height / 2, true));
                            });
                }
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
                image.DebugSave(provider, extension: Extensions.Gif);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void ResizeFromSourceRectangle<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var sourceRectangle = new Rectangle(image.Width / 8, image.Height / 8, image.Width / 4, image.Height / 4);
                var destRectangle = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Resize(image.Width, image.Height, KnownResamplers.Bicubic, sourceRectangle, destRectangle, false));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
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
        public void ResizeWithCropWidthMode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new ResizeOptions
                {
                    Size = new Size(image.Width / 2, image.Height)
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
                var options = new ResizeOptions
                {
                    Size = new Size(image.Width, image.Height / 2)
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
                    Size = new Size(image.Width + 200, image.Height),
                    Mode = ResizeMode.Pad
                };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
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
                    Size = new Size(image.Width + 200, image.Height + 200),
                    Mode = ResizeMode.BoxPad
                };

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
                var options = new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                };

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
                    Size = new Size((int)Math.Round(image.Width * .75F), (int)Math.Round(image.Height * .95F)),
                    Mode = ResizeMode.Min
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
                    Size = new Size(image.Width / 2, image.Height),
                    Mode = ResizeMode.Stretch
                };

                image.Mutate(x => x.Resize(options));

                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

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
        public static void TriangleWindowOscillatesCorrectly(float x, float expected)
        {
            IResampler sampler = KnownResamplers.Triangle;
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
    }
}