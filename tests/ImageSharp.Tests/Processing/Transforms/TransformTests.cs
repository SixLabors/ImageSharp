using System;
using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Helpers;

    public class TransformTests
    {
        private readonly ITestOutputHelper Output;

        /// <summary>
        /// angleDeg, sx, sy, tx, ty
        /// </summary>
        public static readonly TheoryData<float, float, float, float, float> TransformValues
            = new TheoryData<float, float, float, float, float>
                  {
                      { 0, 1, 1, 0, 0 },
                      { 50, 1, 1, 0, 0 },
                      { 0, 1, 1, 20, 10 },
                      { 50, 1, 1, 20, 10 },
                      { 0, 1, 1, -20, -10 },
                      { 50, 1, 1, -20, -10 },
                      { 50, 1.5f, 1.5f, 0, 0 },
                      { 50, 1.1F, 1.3F, 30, -20 },
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

        public static readonly TheoryData<string> Transform_DoesNotCreateEdgeArtifacts_ResamplerNames =
            new TheoryData<string>
                {
                    nameof(KnownResamplers.NearestNeighbor),
                    nameof(KnownResamplers.Triangle),
                    nameof(KnownResamplers.Bicubic),
                    nameof(KnownResamplers.Lanczos8),
                };

        public TransformTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        /// <summary>
        /// The output of an "all white" image should be "all white" or transparent, regardless of the transformation and the resampler.
        /// </summary>
        [Theory]
        [WithSolidFilledImages(nameof(Transform_DoesNotCreateEdgeArtifacts_ResamplerNames), 5, 5, 255, 255, 255, 255, PixelTypes.Rgba32)]
        public void Transform_DoesNotCreateEdgeArtifacts<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler resampler = GetResampler(resamplerName);
            using (Image<TPixel> image = provider.GetImage())
            {
                var rotate = Matrix3x2.CreateRotation((float)Math.PI / 4F, new Vector2(5 / 2F, 5 / 2F));
                var translate = Matrix3x2.CreateTranslation((7 - 5) / 2F, (7 - 5) / 2F);

                Rectangle sourceRectangle = image.Bounds();
                Matrix3x2 matrix = rotate * translate;

                Rectangle destRectangle = TransformHelpers.GetTransformedBoundingRectangle(sourceRectangle, matrix);

                image.Mutate(c => c.Transform(matrix, resampler, destRectangle));
                image.DebugSave(provider, resamplerName);

                VerifyAllPixelsAreWhiteOrTransparent(image);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(TransformValues), 100, 50, PixelTypes.Rgba32)]
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
                var translate = Matrix3x2.CreateTranslation(tx, ty);
                var scale = Matrix3x2.CreateScale(sx, sy);
                Matrix3x2 m = rotate * scale * translate;

                this.PrintMatrix(m);

                image.Mutate(i => i.Transform(m, KnownResamplers.Bicubic));

                string testOutputDetails = $"R({angleDeg})_S({sx},{sy})_T({tx},{ty})";
                image.DebugSave(provider, testOutputDetails);
                image.CompareToReferenceOutput(provider, testOutputDetails);
            }
        }
        
        [Theory]
        [WithTestPatternImages(96, 96, PixelTypes.Rgba32, 50, 0.8f)]
        public void Transform_RotateScale_ManuallyCentered<TPixel>(TestImageProvider<TPixel> provider, float angleDeg, float s)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix3x2 m = this.MakeManuallyCenteredMatrix(angleDeg, s, image);

                image.Mutate(i => i.Transform(m, KnownResamplers.Bicubic));
                image.DebugSave(provider, $"R({angleDeg})_S({s})");
            }
        }

        public static readonly TheoryData<int, int, int, int> Transform_IntoRectangle_Data =
            new TheoryData<int, int, int, int>
                {
                    { 0, 0, 10, 10 },
                    { 0, 0, 5, 10 },
                    { 0, 0, 10, 5 },
                    { 5, 0, 5, 10 },
                    {-5,-5, 15, 15 }
                };

        [Theory]
        [WithSolidFilledImages(nameof(Transform_IntoRectangle_Data), 10, 10, nameof(Rgba32.Red), PixelTypes.Rgba32)]
        public void Transform_IntoRectangle<TPixel>(TestImageProvider<TPixel> provider, int x0, int y0, int w, int h)
            where TPixel : struct, IPixel<TPixel>
        {
            var rectangle = new Rectangle(x0, y0, w, h);

            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix3x2 m = this.MakeManuallyCenteredMatrix(45, 0.8f, image);

                image.Mutate(i => i.Transform(m, KnownResamplers.Spline, rectangle));
                string testDetails = $"({x0},{y0}-W{w},H{h})";
                image.DebugSave(provider, testDetails);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(ResamplerNames), 100, 200, PixelTypes.Rgba32)]
        public void Transform_WithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler sampler = GetResampler(resamplerName);
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(50);
                Matrix3x2 scale = Matrix3x2Extensions.CreateScale(new SizeF(.5F, .5F));
                var translate = Matrix3x2.CreateTranslation(75, 0);
                
                image.Mutate(i => i.Transform(rotate * scale * translate, sampler));
                image.DebugSave(provider, resamplerName);
            }
        }

        private Matrix3x2 MakeManuallyCenteredMatrix<TPixel>(float angleDeg, float s, Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(angleDeg);
            Vector2 toCenter = 0.5f * new Vector2(image.Width, image.Height);
            var translate = Matrix3x2.CreateTranslation(-toCenter);
            var translateBack = Matrix3x2.CreateTranslation(toCenter);
            var scale = Matrix3x2.CreateScale(s);

            Matrix3x2 m = translate * rotate * scale * translateBack;

            this.PrintMatrix(m);
            return m;
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

        private static void VerifyAllPixelsAreWhiteOrTransparent<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel[] data = new TPixel[image.Width * image.Height];
            image.Frames.RootFrame.SavePixelData(data);
            var rgba = default(Rgba32);
            var white = new Rgb24(255, 255, 255);
            foreach (TPixel pixel in data)
            {
                pixel.ToRgba32(ref rgba);
                if (rgba.A == 0) continue;

                Assert.Equal(white, rgba.Rgb);
            }
        }

        private void PrintMatrix(Matrix3x2 a)
        {
            string s = $"{a.M11:F10},{a.M12:F10},{a.M21:F10},{a.M22:F10},{a.M31:F10},{a.M32:F10}";
            this.Output.WriteLine(s);
        }
    }
}