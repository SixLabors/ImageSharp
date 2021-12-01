// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        /// <summary>
        /// <see cref="JpegColorConverterBase"/> abstract base for implementations
        /// based on <see cref="System.Numerics.Vector"/> API.
        /// </summary>
        /// <remarks>
        /// Converters of this family can work with data of any size.
        /// Even though real life data is guaranteed to be of size
        /// divisible by 8 newer SIMD instructions like AVX512 won't work with
        /// such data out of the box. These converters have fallback code
        /// for 'remainder' data.
        /// </remarks>
        internal abstract class VectorizedJpegColorConverter : JpegColorConverterBase
        {
            protected VectorizedJpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            protected sealed override bool IsAvailable => SimdUtils.HasVector8;

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                int length = values.Component0.Length;
                int remainder = values.Component0.Length % 8;
                int simdCount = length - remainder;
                if (simdCount > 0)
                {
                    // This implementation is actually AVX specific.
                    // An AVX register is capable of storing 8 float-s.
                    if (!this.IsAvailable)
                    {
                        throw new InvalidOperationException(
                            "This converter can be used only on architecture having 256 byte floating point SIMD registers!");
                    }

                    this.ConvertCoreVectorizedInplace(values.Slice(0, simdCount));
                }

                this.ConvertCoreInplace(values.Slice(simdCount, remainder));
            }

            protected virtual void ConvertCoreVectorizedInplace(in ComponentValues values) => throw new NotImplementedException();

            protected virtual void ConvertCoreInplace(in ComponentValues values) => throw new NotImplementedException();
        }
    }
}
