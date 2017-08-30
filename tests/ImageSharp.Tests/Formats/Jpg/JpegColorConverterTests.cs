using System;
using System.Numerics;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce;
using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Colorspaces;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

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

        private readonly Vector4[] Result = new Vector4[ResultBufferLength];

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
            
            converter.ConvertToRGBA(values, this.Result);
            
            for (int i = 0; i < ResultBufferLength; i++)
            {
                float y = values.Component0[i];
                float cb = values.Component1[i];
                float cr = values.Component2[i];
                YCbCr ycbcr = new YCbCr(y, cb, cr);

                Vector4 rgba = this.Result[i];
                Rgb actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                Rgb expected = ColorSpaceConverter.ToRgb(ycbcr);
                
                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }
    }
}