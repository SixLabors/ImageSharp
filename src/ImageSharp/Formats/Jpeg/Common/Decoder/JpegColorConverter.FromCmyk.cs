using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromCmyk : JpegColorConverter
        {
            public FromCmyk()
                : base(JpegColorSpace.Cmyk)
            {
            }

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> cVals = values.Component0;
                ReadOnlySpan<float> mVals = values.Component1;
                ReadOnlySpan<float> yVals = values.Component2;
                ReadOnlySpan<float> kVals = values.Component3;

                var v = new Vector4(0, 0, 0, 1F);

                var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float c = cVals[i];
                    float m = mVals[i];
                    float y = yVals[i];
                    float k = kVals[i] / 255F;

                    v.X = c * k;
                    v.Y = m * k;
                    v.Z = y * k;
                    v.W = 1F;

                    v *= scale;

                    result[i] = v;
                }
            }
        }
    }
}