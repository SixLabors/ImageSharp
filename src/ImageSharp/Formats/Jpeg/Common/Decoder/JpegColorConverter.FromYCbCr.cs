using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal abstract partial class JpegColorConverter
    {
        private class FromYCbCr : JpegColorConverter
        {
            private static readonly YCbCrAndRgbConverter Converter = new YCbCrAndRgbConverter();

            public FromYCbCr()
                : base(JpegColorSpace.YCbCr)
            {
            }

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> yVals = values.Component0;
                ReadOnlySpan<float> cbVals = values.Component1;
                ReadOnlySpan<float> crVals = values.Component2;

                Vector4 rgbaVector = new Vector4(0, 0, 0, 1);

                for (int i = 0; i < result.Length; i++)
                {
                    float colY = yVals[i];
                    float colCb = cbVals[i];
                    float colCr = crVals[i];

                    YCbCr yCbCr = new YCbCr(colY, colCb, colCr);

                    // Slow conversion for now:
                    Rgb rgb = Converter.Convert(yCbCr);

                    Unsafe.As<Vector4, Vector3>(ref rgbaVector) = rgb.Vector;
                    result[i] = rgbaVector;
                }
            }
        }
    }
}