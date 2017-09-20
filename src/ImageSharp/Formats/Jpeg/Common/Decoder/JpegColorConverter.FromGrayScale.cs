﻿namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    using System;
    using System.Numerics;

    internal abstract partial class JpegColorConverter
    {
        internal class FromGrayScale : JpegColorConverter
        {
            public FromGrayScale()
                : base(JpegColorSpace.GrayScale)
            {
            }

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> yVals = values.Component0;

                var v = new Vector4(0, 0, 0, 1);

                var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float y = yVals[i];

                    v.X = y;
                    v.Y = y;
                    v.Z = y;

                    v *= scale;

                    result[i] = v;
                }
            }
        }
    }
}