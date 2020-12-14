// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class BokehBlurTest
    {
        private static readonly string Components10x2 = @"
        [[ 0.00451261+0.0165137j   0.02161237-0.00299122j  0.00387479-0.02682816j
          -0.02752798-0.01788438j -0.03553877+0.0154543j  -0.01428268+0.04224722j
           0.01747482+0.04687464j  0.04243676+0.03451751j  0.05564306+0.01742537j
           0.06040984+0.00459225j  0.06136251+0.0j         0.06040984+0.00459225j
           0.05564306+0.01742537j  0.04243676+0.03451751j  0.01747482+0.04687464j
          -0.01428268+0.04224722j -0.03553877+0.0154543j  -0.02752798-0.01788438j
           0.00387479-0.02682816j  0.02161237-0.00299122j  0.00451261+0.0165137j ]]
        [[-0.00227282+0.002851j   -0.00152245+0.00604545j  0.00135338+0.00998296j
           0.00698622+0.01370844j  0.0153483+0.01605112j   0.02565295+0.01611732j
           0.03656958+0.01372368j  0.04662725+0.00954624j  0.05458942+0.00491277j
           0.05963937+0.00133843j  0.06136251+0.0j         0.05963937+0.00133843j
           0.05458942+0.00491277j  0.04662725+0.00954624j  0.03656958+0.01372368j
           0.02565295+0.01611732j  0.0153483+0.01605112j   0.00698622+0.01370844j
           0.00135338+0.00998296j -0.00152245+0.00604545j -0.00227282+0.002851j  ]]";

        [Theory]
        [InlineData(-10, 2, 3f)]
        [InlineData(-1, 2, 3f)]
        [InlineData(0, 2, 3f)]
        [InlineData(20, -1, 3f)]
        [InlineData(20, -0, 3f)]
        [InlineData(20, 4, -10f)]
        [InlineData(20, 4, 0f)]
        public void VerifyBokehBlurProcessorArguments_Fail(int radius, int components, float gamma)
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => new BokehBlurProcessor(radius, components, gamma));

        [Fact]
        public void VerifyComplexComponents()
        {
            // Get the saved components
            var components = new List<Complex64[]>();
            foreach (Match match in Regex.Matches(Components10x2, @"\[\[(.*?)\]\]", RegexOptions.Singleline))
            {
                string[] values = match.Groups[1].Value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Complex64[] component = values.Select(
                    value =>
                        {
                            Match pair = Regex.Match(value, @"([+-]?\d+\.\d+)([+-]?\d+\.\d+)j");
                            return new Complex64(
                                float.Parse(pair.Groups[1].Value, CultureInfo.InvariantCulture),
                                float.Parse(pair.Groups[2].Value, CultureInfo.InvariantCulture));
                        }).ToArray();
                components.Add(component);
            }

            // Make sure the kernel components are the same
            using (var image = new Image<Rgb24>(1, 1))
            {
                Configuration configuration = image.GetConfiguration();
                var definition = new BokehBlurProcessor(10, BokehBlurProcessor.DefaultComponents, BokehBlurProcessor.DefaultGamma);
                using (var processor = (BokehBlurProcessor<Rgb24>)definition.CreatePixelSpecificProcessor(configuration, image, image.Bounds()))
                {
                    Assert.Equal(components.Count, processor.Kernels.Count);
                    foreach ((Complex64[] a, Complex64[] b) in components.Zip(processor.Kernels, (a, b) => (a, b)))
                    {
                        Span<Complex64> spanA = a.AsSpan(), spanB = b.AsSpan();
                        Assert.Equal(spanA.Length, spanB.Length);
                        for (int i = 0; i < spanA.Length; i++)
                        {
                            Assert.True(Math.Abs(Math.Abs(spanA[i].Real) - Math.Abs(spanB[i].Real)) < 0.0001f);
                            Assert.True(Math.Abs(Math.Abs(spanA[i].Imaginary) - Math.Abs(spanB[i].Imaginary)) < 0.0001f);
                        }
                    }
                }
            }
        }

        public sealed class BokehBlurInfo : IXunitSerializable
        {
            public int Radius { get; set; }

            public int Components { get; set; }

            public float Gamma { get; set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                this.Radius = info.GetValue<int>(nameof(this.Radius));
                this.Components = info.GetValue<int>(nameof(this.Components));
                this.Gamma = info.GetValue<float>(nameof(this.Gamma));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(this.Radius), this.Radius, typeof(int));
                info.AddValue(nameof(this.Components), this.Components, typeof(int));
                info.AddValue(nameof(this.Gamma), this.Gamma, typeof(float));
            }

            public override string ToString() => $"R{this.Radius}_C{this.Components}_G{this.Gamma}";
        }

        public static readonly TheoryData<BokehBlurInfo> BokehBlurValues = new TheoryData<BokehBlurInfo>
        {
            new BokehBlurInfo { Radius = 8, Components = 1, Gamma = 1 },
            new BokehBlurInfo { Radius = 16, Components = 1, Gamma = 3 },
            new BokehBlurInfo { Radius = 16, Components = 2, Gamma = 3 }
        };

        public static readonly string[] TestFiles =
            {
                TestImages.Png.CalliphoraPartial,
                TestImages.Png.Bike,
                TestImages.Png.BikeGrayscale,
                TestImages.Png.Cross,
            };

        [Theory]
        [WithFileCollection(nameof(TestFiles), nameof(BokehBlurValues), PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BokehBlurValues), 50, 50, "Red", PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BokehBlurValues), 200, 100, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BokehBlurValues), 23, 31, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BokehBlurValues), 30, 20, PixelTypes.Rgba32)]
        public void BokehBlurFilterProcessor<TPixel>(TestImageProvider<TPixel> provider, BokehBlurInfo value)
            where TPixel : unmanaged, IPixel<TPixel>
            => provider.RunValidatingProcessorTest(
                x => x.BokehBlur(value.Radius, value.Components, value.Gamma),
                testOutputDetails: value.ToString(),
                appendPixelTypeToFileName: false);

        [Theory]
        /*
         TODO: Re-enable L8 when we update the reference images.
         [WithTestPatternImages(200, 200, PixelTypes.Bgr24 | PixelTypes.Bgra32 | PixelTypes.L8)]
        */
        [WithTestPatternImages(200, 200, PixelTypes.Bgr24 | PixelTypes.Bgra32)]
        public void BokehBlurFilterProcessor_WorksWithAllPixelTypes<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
            => provider.RunValidatingProcessorTest(
                x => x.BokehBlur(8, 2, 3),
                appendSourceFileOrDescription: false);

        [Theory]
        [WithFileCollection(nameof(TestFiles), nameof(BokehBlurValues), PixelTypes.Rgba32)]
        public void BokehBlurFilterProcessor_Bounded(TestImageProvider<Rgba32> provider, BokehBlurInfo value)
        {
            static void RunTest(string arg1, string arg2)
            {
                TestImageProvider<Rgba32> provider =
                    FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(arg1);

                BokehBlurInfo value =
                    FeatureTestRunner.DeserializeForXunit<BokehBlurInfo>(arg2);

                provider.RunValidatingProcessorTest(
                x =>
                {
                    Size size = x.GetCurrentSize();
                    var bounds = new Rectangle(10, 10, size.Width / 2, size.Height / 2);
                    x.BokehBlur(value.Radius, value.Components, value.Gamma, bounds);
                },
                testOutputDetails: value.ToString(),
                appendPixelTypeToFileName: false);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.DisableSSE41,
                provider,
                value);
        }

        [Theory]
        [WithTestPatternImages(100, 300, PixelTypes.Bgr24)]
        public void WorksWithDiscoBuffers<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
            => provider.RunBufferCapacityLimitProcessorTest(260, c => c.BokehBlur());
    }
}
