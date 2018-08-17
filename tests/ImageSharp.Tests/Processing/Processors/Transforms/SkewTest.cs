// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Reflection;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class SkewTest : FileTestBase
    {
        public static readonly TheoryData<float, float> SkewValues
            = new TheoryData<float, float>
        {
            { 20, 10 },
            { -20, -10 }
        };

        public static readonly List<string> ResamplerNames
            = new List<string>
        {
            nameof(KnownResamplers.Bicubic),
            nameof(KnownResamplers.Box),
            nameof(KnownResamplers.CatmullRom),
            nameof(KnownResamplers.Hermite),
            nameof(KnownResamplers.Lanczos2),
            nameof(KnownResamplers.Lanczos3),
            nameof(KnownResamplers.Lanczos5),
            nameof(KnownResamplers.Lanczos8),
            nameof(KnownResamplers.MitchellNetravali),
            nameof(KnownResamplers.NearestNeighbor),
            nameof(KnownResamplers.Robidoux),
            nameof(KnownResamplers.RobidouxSharp),
            nameof(KnownResamplers.Spline),
            nameof(KnownResamplers.Triangle),
            nameof(KnownResamplers.Welch),
        };

        [Theory]
        [WithTestPatternImages(nameof(SkewValues), 100, 50, DefaultPixelType)]
        public void ImageShouldSkew<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(i => i.Skew(x, y));
                image.DebugSave(provider, string.Join("_", x, y));
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(SkewValues), 100, 50, DefaultPixelType)]
        public void ImageShouldSkewWithSampler<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : struct, IPixel<TPixel>
        {
            foreach (string resamplerName in ResamplerNames)
            {
                IResampler sampler = GetResampler(resamplerName);
                using (Image<TPixel> image = provider.GetImage())
                {
                    image.Mutate(i => i.Skew(x, y, sampler));
                    image.DebugSave(provider, string.Join("_", x, y, resamplerName));
                }
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

            if (property is null)
            {
                throw new Exception($"No resampler named '{name}");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}