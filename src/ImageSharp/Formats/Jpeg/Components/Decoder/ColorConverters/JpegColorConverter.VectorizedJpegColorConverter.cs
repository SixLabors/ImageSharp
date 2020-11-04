// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class VectorizedJpegColorConverter : JpegColorConverter
        {
            private readonly int vectorSize;

            protected VectorizedJpegColorConverter(JpegColorSpace colorSpace, int precision, int vectorSize)
                : base(colorSpace, precision)
            {
                this.vectorSize = vectorSize;
            }

            public sealed override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                int remainder = result.Length % this.vectorSize;
                int simdCount = result.Length - remainder;
                if (simdCount > 0)
                {
                    // This implementation is actually AVX specific.
                    // An AVX register is capable of storing 8 float-s.
                    if (!this.IsAvailable)
                    {
                        throw new InvalidOperationException(
                            "This converter can be used only on architecture having 256 byte floating point SIMD registers!");
                    }

                    this.ConvertCoreVectorized(values.Slice(0, simdCount), result.Slice(0, simdCount));
                }

                this.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder));
            }

            protected abstract void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result);

            protected abstract void ConvertCore(in ComponentValues values, Span<Vector4> result);
        }
    }
}
