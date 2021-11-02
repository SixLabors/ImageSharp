// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class AutoVectorizedJpegColorConverter : JpegColorConverter
        {
            protected AutoVectorizedJpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            protected sealed override bool IsAvailable => Vector.IsHardwareAccelerated;

            public sealed override void ConvertToRgbInplace(in ComponentValues values)
            {
                DebugGuard.IsTrue(this.IsAvailable, $"Used unsupported color converter: {this.GetType()}");

                int length = values.Component0.Length;
                int remainder = values.Component0.Length % Vector<float>.Count;
                int simdCount = length - remainder;
                if (simdCount > 0)
                {
                    this.ConvertCoreVectorizedInplace(values.Slice(0, simdCount));
                }

                this.ConvertCoreInplace(values.Slice(simdCount, remainder));
            }

            protected abstract void ConvertCoreVectorizedInplace(in ComponentValues values);

            protected abstract void ConvertCoreInplace(in ComponentValues values);
        }
    }
}
