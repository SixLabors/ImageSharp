using System;
using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using System.Numerics;
    using System.Reflection;

    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.Primitives;

    using Xunit;

    public class TransformTests : FileTestBase
    {
        public static readonly TheoryData<float, float, float, float, float> TransformValues
            = new TheoryData<float, float, float, float, float>
                  {
                      { 45, 1, 1, 20, 10 },
                      { 45, 1, 1, -20, -10 },
                      { 45, 1.5f, 1.5f, 0, 0 },
                      { 0, 2f, 1f, 0, 0 },
                      { 0, 1f, 2f, 0, 0 },
                  };

        public static readonly TheoryData<string> ResamplerNames = 
            new TheoryData<string>
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
        [WithTestPatternImages(nameof(TransformValues), 100, 50, DefaultPixelType)]
        public void Transform_RotateScaleTranslate<TPixel>(
            TestImageProvider<TPixel> provider,
            float angleDeg,
            float sx, float sy,
            float tx, float ty)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(angleDeg);
                Matrix3x2 translate = Matrix3x2Extensions.CreateTranslation(new PointF(tx, ty));
                Matrix3x2 scale = Matrix3x2Extensions.CreateScale(new SizeF(sx, sy));
                
                image.Mutate(i => i.Transform(rotate * scale * translate));
                image.DebugSave(provider, $"R({angleDeg})_S({sx},{sy})_T({tx},{ty})");
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(ResamplerNames), 100, 200, DefaultPixelType)]
        public void Transform_WithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler sampler = GetResampler(resamplerName);
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(45);
                Matrix3x2 scale = Matrix3x2Extensions.CreateScale(new SizeF(.5F, .5F));

                image.Mutate(i => i.Transform(rotate * scale, sampler));
                image.DebugSave(provider, resamplerName);
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

            if (property == null)
            {
                throw new Exception("Invalid property name!");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}