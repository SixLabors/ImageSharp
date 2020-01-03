// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    [GroupOutput("Effects")]
    public class PixelShaderTest
    {
        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void FullImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(
                x => x.ApplyPixelShaderProcessor(
                span =>
                {
                    for (int i = 0; i < span.Length; i++)
                    {
                        Vector4 v4 = span[i];
                        float avg = (v4.X + v4.Y + v4.Z) / 3f;
                        span[i] = new Vector4(avg);
                    }
                }),
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void InBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.ApplyPixelShaderProcessor(
                    span =>
                    {
                        for (int i = 0; i < span.Length; i++)
                        {
                            Vector4 v4 = span[i];
                            float avg = (v4.X + v4.Y + v4.Z) / 3f;
                            span[i] = new Vector4(avg);
                        }
                    }, rect));
        }
    }
}
