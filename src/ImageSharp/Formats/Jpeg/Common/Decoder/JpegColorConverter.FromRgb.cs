using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromRgb : JpegColorConverter
        {
            public FromRgb()
                : base(JpegColorSpace.RGB)
            {
            }

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> rVals = values.Component0;
                ReadOnlySpan<float> gVals = values.Component1;
                ReadOnlySpan<float> bVals = values.Component2;

                var v = new Vector4(0, 0, 0, 1);

                var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float r = rVals[i];
                    float g = gVals[i];
                    float b = bVals[i];

                    v.X = r;
                    v.Y = g;
                    v.Z = b;

                    v *= scale;

                    result[i] = v;
                }
            }
        }
    }
}