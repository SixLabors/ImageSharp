// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromRgbBasic : BasicJpegColorConverter
        {
            public FromRgbBasic(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ConvertCoreInplace(values, this.MaximumValue);
            }

            internal static void ConvertCoreInplace(ComponentValues values, float maxValue)
            {
                FromGrayscaleBasic.ScaleValues(values.Component0, maxValue);
                FromGrayscaleBasic.ScaleValues(values.Component1, maxValue);
                FromGrayscaleBasic.ScaleValues(values.Component2, maxValue);
            }

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue)
            {
                ReadOnlySpan<float> rVals = values.Component0;
                ReadOnlySpan<float> gVals = values.Component1;
                ReadOnlySpan<float> bVals = values.Component2;

                var v = new Vector4(0, 0, 0, 1);

                var maximum = 1 / maxValue;
                var scale = new Vector4(maximum, maximum, maximum, 1F);

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
