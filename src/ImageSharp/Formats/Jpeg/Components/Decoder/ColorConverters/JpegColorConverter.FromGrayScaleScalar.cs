// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromGrayscaleScalar : BasicJpegColorConverter
        {
            public FromGrayscaleScalar(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ScaleValues(values.Component0, this.MaximumValue);

            internal static void ScaleValues(Span<float> values, float maxValue)
            {
                Span<Vector4> vecValues = MemoryMarshal.Cast<float, Vector4>(values);

                var scaleVector = new Vector4(1 / maxValue);

                for (int i = 0; i < vecValues.Length; i++)
                {
                    vecValues[i] *= scaleVector;
                }

                values = values.Slice(vecValues.Length * 4);
                if (!values.IsEmpty)
                {
                    float scaleValue = 1f / maxValue;
                    values[0] *= scaleValue;

                    if ((uint)values.Length > 1)
                    {
                        values[1] *= scaleValue;

                        if ((uint)values.Length > 2)
                        {
                            values[2] *= scaleValue;
                        }
                    }
                }
            }
        }
    }
}
