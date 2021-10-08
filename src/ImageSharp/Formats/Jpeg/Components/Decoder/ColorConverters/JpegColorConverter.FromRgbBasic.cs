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
        }
    }
}
