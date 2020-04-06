// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    public partial class ProcessPixelRowsAsVector4Test
    {
        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void FullImageWithValueDelegate<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(
                x => x.ProcessPixelRowsAsVector4<PixelAverageProcessor>(),
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void InBoxWithValueDelegate<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.ProcessPixelRowsAsVector4<PixelAverageProcessor>(rect));
        }

        private readonly struct PixelAverageProcessor : IPixelRowDelegate
        {
            public void Invoke(Span<Vector4> span)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    Vector4 v4 = span[i];
                    float avg = (v4.X + v4.Y + v4.Z) / 3f;
                    span[i] = new Vector4(avg);
                }
            }
        }
    }
}
