using System;
using System.Collections.Generic;

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
        public static readonly TheoryData<float, float, float> TransformValues
            = new TheoryData<float, float, float>
                  {
                      { 20, 10, 50 },
                      { -20, -10, 50 }
                  };

        public static readonly List<string> ResamplerNames
            = new List<string>
                  {
                      nameof(KnownResamplers.Bicubic),
                      //nameof(KnownResamplers.Box),
                      //nameof(KnownResamplers.CatmullRom),
                      //nameof(KnownResamplers.Hermite),
                      //nameof(KnownResamplers.Lanczos2),
                      //nameof(KnownResamplers.Lanczos3),
                      //nameof(KnownResamplers.Lanczos5),
                      //nameof(KnownResamplers.Lanczos8),
                      //nameof(KnownResamplers.MitchellNetravali),
                      //nameof(KnownResamplers.NearestNeighbor),
                      //nameof(KnownResamplers.Robidoux),
                      //nameof(KnownResamplers.RobidouxSharp),
                      //nameof(KnownResamplers.Spline),
                      //nameof(KnownResamplers.Triangle),
                      //nameof(KnownResamplers.Welch),
                  };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(TransformValues), DefaultPixelType)]
        public void ImageShouldTransformWithSampler<TPixel>(TestImageProvider<TPixel> provider, float x, float y, float z)
            where TPixel : struct, IPixel<TPixel>
        {

            foreach (string resamplerName in ResamplerNames)
            {
                IResampler sampler = GetResampler(resamplerName);
                using (Image<TPixel> image = provider.GetImage())
                {
                    Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(-z);

                    // TODO, how does scale work? 2 means half just now,
                    Matrix3x2 scale = Matrix3x2Extensions.CreateScale(new SizeF(2F, 2F));


                    image.Mutate(i => i.Transform(scale * rotate, sampler));
                    image.DebugSave(provider, string.Join("_", x, y, resamplerName));
                }
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