// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromGrayscaleBasic : BasicJpegColorConverter
        {
            public FromGrayscaleBasic(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ScaleValues(values.Component0, this.MaximumValue);

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue)
            {
                var maximum = 1 / maxValue;
                var scale = new Vector4(maximum, maximum, maximum, 1F);

                ref float sBase = ref MemoryMarshal.GetReference(values.Component0);
                ref Vector4 dBase = ref MemoryMarshal.GetReference(result);

                for (int i = 0; i < result.Length; i++)
                {
                    var v = new Vector4(Unsafe.Add(ref sBase, i));
                    v.W = 1f;
                    v *= scale;
                    Unsafe.Add(ref dBase, i) = v;
                }
            }

            internal static void ScaleValues(Span<float> values, float maxValue)
            {
                // TODO: Optimize this
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
    }
}
