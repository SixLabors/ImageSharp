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

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                ConvertCore(values, result, this.MaximumValue);
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ConvertCoreInplace(values, this.MaximumValue);
            }

            internal static void ConvertCoreInplace(ComponentValues values, float maxValue)
            {
                // TODO: Optimize this
                ConvertComponent(values.Component0, maxValue);
                ConvertComponent(values.Component1, maxValue);
                ConvertComponent(values.Component2, maxValue);

                static void ConvertComponent(Span<float> values, float maxValue)
                {
                    Span<Vector4> vecValues = MemoryMarshal.Cast<float, Vector4>(values);

                    var scaleVector = new Vector4(1 / maxValue);

                    for (int i = 0; i < vecValues.Length; i++)
                    {
                        vecValues[i] *= scaleVector;
                    }

                    values = values.Slice(vecValues.Length * 4);
                    if (values.Length > 0)
                    {
                        float scaleValue = 1f / maxValue;
                        values[0] *= scaleValue;

                        if (values.Length > 1)
                        {
                            values[1] *= scaleValue;

                            if (values.Length > 2)
                            {
                                values[2] *= scaleValue;
                            }
                        }
                    }
                }
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
