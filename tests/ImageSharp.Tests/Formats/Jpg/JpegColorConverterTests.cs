using System;
using System.Numerics;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
using SixLabors.ImageSharp.Memory;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegColorConverterTests
    {
        private const float Precision = 0.1f;

        private const int InputBufferLength = 42;

        // The result buffer could be shorter
        private const int ResultBufferLength = 40;

        private readonly Vector4[] result = new Vector4[ResultBufferLength];

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        public JpegColorConverterTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private static JpegColorConverter.ComponentValues CreateRandomValues(int componentCount, float maxVal = 255f)
        {
            var rnd = new Random(42);
            Buffer2D<float>[] buffers = new Buffer2D<float>[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                float[] values = new float[InputBufferLength];

                for (int j = 0; j < InputBufferLength; j++)
                {
                    values[j] = (float)rnd.NextDouble() * maxVal;
                }

                // no need to dispose when buffer is not array owner
                buffers[i] = new Buffer2D<float>(values, values.Length, 1);
            }
            return new JpegColorConverter.ComponentValues(buffers, 0);
        }

        [Fact]
        public void ConvertFromYCbCr()
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.YCbCr);

            JpegColorConverter.ComponentValues values = CreateRandomValues(3);

            converter.ConvertToRGBA(values, this.result);

            for (int i = 0; i < ResultBufferLength; i++)
            {
                float y = values.Component0[i];
                float cb = values.Component1[i];
                float cr = values.Component2[i];
                var ycbcr = new YCbCr(y, cb, cr);

                Vector4 rgba = this.result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = ColorSpaceConverter.ToRgb(ycbcr);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Fact]
        public void ConvertFromCmyk()
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.Cmyk);

            JpegColorConverter.ComponentValues values = CreateRandomValues(4);

            converter.ConvertToRGBA(values, this.result);

            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

            for (int i = 0; i < ResultBufferLength; i++)
            {
                float c = values.Component0[i];
                float m = values.Component1[i];
                float y = values.Component2[i];
                float k = values.Component3[i] / 255F;

                v.X = c * k;
                v.Y = m * k;
                v.Z = y * k;
                v.W = 1F;

                v *= scale;

                Vector4 rgba = this.result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(v.X, v.Y, v.Z);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Fact]
        public void ConvertFromYcck()
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.Ycck);

            JpegColorConverter.ComponentValues values = CreateRandomValues(4);

            converter.ConvertToRGBA(values, this.result);

            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

            for (int i = 0; i < ResultBufferLength; i++)
            {
                float y = values.Component0[i];
                float cb = values.Component1[i] - 128F;
                float cr = values.Component2[i] - 128F;
                float k = values.Component3[i] / 255F;

                v.X = (255F - MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero)) * k;
                v.Y = (255F - MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero)) * k;
                v.Z = (255F - MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero)) * k;
                v.W = 1F;

                v *= scale;

                Vector4 rgba = this.result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(v.X, v.Y, v.Z);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Fact]
        public void ConvertFromGrayScale()
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.GrayScale);

            JpegColorConverter.ComponentValues values = CreateRandomValues(1);

            converter.ConvertToRGBA(values, this.result);

            for (int i = 0; i < ResultBufferLength; i++)
            {
                float y = values.Component0[i];
                Vector4 rgba = this.result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(y / 255F, y / 255F, y / 255F);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Fact]
        public void ConvertFromRgb()
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.RGB);

            JpegColorConverter.ComponentValues values = CreateRandomValues(3);

            converter.ConvertToRGBA(values, this.result);

            for (int i = 0; i < ResultBufferLength; i++)
            {
                float r = values.Component0[i];
                float g = values.Component1[i];
                float b = values.Component2[i];
                Vector4 rgba = this.result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(r / 255F, g / 255F, b / 255F);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }
    }
}