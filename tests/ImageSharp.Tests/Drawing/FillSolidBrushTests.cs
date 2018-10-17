// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;
using SixLabors.Shapes;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class FillSolidBrushTests
    {
        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        [WithBlankImages(7, 4, PixelTypes.Rgba32)]
        [WithBlankImages(16, 7, PixelTypes.Rgba32)]
        [WithBlankImages(33, 32, PixelTypes.Rgba32)]
        [WithBlankImages(400, 500, PixelTypes.Rgba32)]
        public void DoesNotDependOnSize<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                TPixel color = NamedColors<TPixel>.HotPink;
                image.Mutate(c => c.Fill(color));

                image.DebugSave(provider, appendPixelTypeToFileName: false);
                image.ComparePixelBufferTo(color);
            }
        }

        [Theory]
        [WithBlankImages(16, 16, PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.RgbaVector)]
        public void DoesNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                TPixel color = NamedColors<TPixel>.HotPink;
                image.Mutate(c => c.Fill(color));

                image.DebugSave(provider, appendSourceFileOrDescription: false);
                image.ComparePixelBufferTo(color);
            }
        }

        [Theory]
        [WithSolidFilledImages(16, 16, "Red", PixelTypes.Rgba32, "Blue")]
        [WithSolidFilledImages(16, 16, "Yellow", PixelTypes.Rgba32, "Khaki")]
        public void WhenColorIsOpaque_OverridePreviousColor<TPixel>(
            TestImageProvider<TPixel> provider,
            string newColorName)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                TPixel color = TestUtils.GetPixelOfNamedColor<TPixel>(newColorName);
                image.Mutate(c => c.Fill(color));

                image.DebugSave(
                    provider,
                    newColorName,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);
                image.ComparePixelBufferTo(color);
            }
        }

        [Theory]
        [WithSolidFilledImages(16, 16, "Red", PixelTypes.Rgba32, 5, 7, 3, 8)]
        [WithSolidFilledImages(16, 16, "Red", PixelTypes.Rgba32, 8, 5, 6, 4)]
        public void FillRegion<TPixel>(TestImageProvider<TPixel> provider, int x0, int y0, int w, int h)
            where TPixel : struct, IPixel<TPixel>
        {
            FormattableString testDetails = $"(x{x0},y{y0},w{w},h{h})";
            var region = new RectangleF(x0, y0, w, h);
            TPixel color = TestUtils.GetPixelOfNamedColor<TPixel>("Blue");

            provider.RunValidatingProcessorTest(c => c.Fill(color, region), testDetails, ImageComparer.Exact);
        }

        [Theory]
        [WithSolidFilledImages(16, 16, "Red", PixelTypes.Rgba32, 5, 7, 3, 8)]
        [WithSolidFilledImages(16, 16, "Red", PixelTypes.Rgba32, 8, 5, 6, 4)]
        public void FillRegion_WorksOnWrappedMemoryImage<TPixel>(
            TestImageProvider<TPixel> provider,
            int x0,
            int y0,
            int w,
            int h)
            where TPixel : struct, IPixel<TPixel>
        {
            FormattableString testDetails = $"(x{x0},y{y0},w{w},h{h})";
            var region = new RectangleF(x0, y0, w, h);
            TPixel color = TestUtils.GetPixelOfNamedColor<TPixel>("Blue");

            provider.RunValidatingProcessorTestOnWrappedMemoryImage(
                c => c.Fill(color, region),
                testDetails,
                ImageComparer.Exact,
                useReferenceOutputFrom: nameof(this.FillRegion));
        }

        public static readonly TheoryData<bool, string, float, PixelColorBlendingMode, float> BlendData =
            new TheoryData<bool, string, float, PixelColorBlendingMode, float>()
                {
                    { false, "Blue", 0.5f, PixelColorBlendingMode.Normal, 1.0f },
                    { false, "Blue", 1.0f, PixelColorBlendingMode.Normal, 0.5f },
                    { false, "Green", 0.5f, PixelColorBlendingMode.Normal, 0.3f },
                    { false, "HotPink", 0.8f, PixelColorBlendingMode.Normal, 0.8f },
                    { false, "Blue", 0.5f, PixelColorBlendingMode.Multiply, 1.0f },
                    { false, "Blue", 1.0f, PixelColorBlendingMode.Multiply, 0.5f },
                    { false, "Green", 0.5f, PixelColorBlendingMode.Multiply, 0.3f },
                    { false, "HotPink", 0.8f, PixelColorBlendingMode.Multiply, 0.8f },
                    { false, "Blue", 0.5f, PixelColorBlendingMode.Add, 1.0f },
                    { false, "Blue", 1.0f, PixelColorBlendingMode.Add, 0.5f },
                    { false, "Green", 0.5f, PixelColorBlendingMode.Add, 0.3f },
                    { false, "HotPink", 0.8f, PixelColorBlendingMode.Add, 0.8f },
                    { true, "Blue", 0.5f, PixelColorBlendingMode.Normal, 1.0f },
                    { true, "Blue", 1.0f, PixelColorBlendingMode.Normal, 0.5f },
                    { true, "Green", 0.5f, PixelColorBlendingMode.Normal, 0.3f },
                    { true, "HotPink", 0.8f, PixelColorBlendingMode.Normal, 0.8f },
                    { true, "Blue", 0.5f, PixelColorBlendingMode.Multiply, 1.0f },
                    { true, "Blue", 1.0f, PixelColorBlendingMode.Multiply, 0.5f },
                    { true, "Green", 0.5f, PixelColorBlendingMode.Multiply, 0.3f },
                    { true, "HotPink", 0.8f, PixelColorBlendingMode.Multiply, 0.8f },
                    { true, "Blue", 0.5f, PixelColorBlendingMode.Add, 1.0f },
                    { true, "Blue", 1.0f, PixelColorBlendingMode.Add, 0.5f },
                    { true, "Green", 0.5f, PixelColorBlendingMode.Add, 0.3f },
                    { true, "HotPink", 0.8f, PixelColorBlendingMode.Add, 0.8f },
                };

        [Theory]
        [WithSolidFilledImages(nameof(BlendData), 16, 16, "Red", PixelTypes.Rgba32)]
        public void BlendFillColorOverBackround<TPixel>(
            TestImageProvider<TPixel> provider,
            bool triggerFillRegion,
            string newColorName,
            float alpha,
            PixelColorBlendingMode blenderMode,
            float blendPercentage)
            where TPixel : struct, IPixel<TPixel>
        {
            var vec = TestUtils.GetPixelOfNamedColor<RgbaVector>(newColorName).ToVector4();
            vec.W = alpha;

            TPixel fillColor = default;
            fillColor.PackFromVector4(vec);

            using (Image<TPixel> image = provider.GetImage())
            {
                TPixel bgColor = image[0, 0];

                var options = new GraphicsOptions(false)
                                  {
                                      ColorBlendingMode = blenderMode, BlendPercentage = blendPercentage
                                  };

                if (triggerFillRegion)
                {
                    var region = new ShapeRegion(new RectangularPolygon(0, 0, 16, 16));

                    image.Mutate(c => c.Fill(options, new SolidBrush<TPixel>(fillColor), region));
                }
                else
                {
                    image.Mutate(c => c.Fill(options, new SolidBrush<TPixel>(fillColor)));
                }

                var testOutputDetails = new
                                            {
                                                triggerFillRegion = triggerFillRegion,
                                                newColorName = newColorName,
                                                alpha = alpha,
                                                blenderMode = blenderMode,
                                                blendPercentage = blendPercentage
                                            };

                image.DebugSave(
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);

                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(
                    blenderMode,
                    PixelAlphaCompositionMode.SrcOver);
                TPixel expectedPixel = blender.Blend(bgColor, fillColor, blendPercentage);

                image.ComparePixelBufferTo(expectedPixel);
            }
        }
    }
}