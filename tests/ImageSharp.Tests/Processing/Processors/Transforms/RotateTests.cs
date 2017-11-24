// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    using System;
    using System.Reflection;

    public class RotateTests : FileTestBase
    {
        public static readonly TheoryData<float> RotateFloatValues
            = new TheoryData<float>
        {
             170,
            -170
        };

        public static readonly TheoryData<RotateType> RotateEnumValues
            = new TheoryData<RotateType>
        {
            RotateType.None,
            RotateType.Rotate90,
            RotateType.Rotate180,
            RotateType.Rotate270
        };

        public static readonly TheoryData<string> ResmplerNames = new TheoryData<string>
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
        [WithTestPatternImages(nameof(RotateFloatValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateFloatValues), 50, 100, DefaultPixelType)]
        public void Rotate<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Rotate(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(ResmplerNames), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(ResmplerNames), 50, 100, DefaultPixelType)]
        public void RotateWithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler resampler = GetResampler(resamplerName);

            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Rotate(50, resampler));
                image.DebugSave(provider, resamplerName);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(RotateEnumValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateEnumValues), 50, 100, DefaultPixelType)]
        public void Rotate_WithRotateTypeEnum<TPixel>(TestImageProvider<TPixel> provider, RotateType value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Rotate(value));
                image.DebugSave(provider, value);
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

            if (property == null)
            {
                throw new Exception("Invalid property name!");
            }

            return (IResampler) property.GetValue(null);
        }
    }
}