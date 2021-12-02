// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        /// <summary>
        /// <see cref="JpegColorConverterBase"/> abstract base for implementations
        /// based on <see cref="Vector"/> API.
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

            public sealed override bool IsAvailable => Vector<float>.Count % 4 == 0;

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                DebugGuard.IsTrue(this.IsAvailable, $"{this.GetType().Name} converter is not supported on current hardware");

                int length = values.Component0.Length;
                int remainder = values.Component0.Length % Vector<float>.Count;

                // Jpeg images are guaranteed to have pixel strides at least 8 pixels wide
                // Thus there's no need to check whether simdCount is greater than zero
                int simdCount = length - remainder;
                this.ConvertCoreVectorizedInplace(values.Slice(0, simdCount));

                // There's actually a lot of image/photo resolutions which won't have
                // a remainder so it's better to check here than spend useless virtual call
                if (remainder > 0)
                {
                    this.ConvertCoreInplace(values.Slice(simdCount, remainder));
                }
            }

            protected virtual void ConvertCoreVectorizedInplace(in ComponentValues values) => throw new NotImplementedException();

            protected virtual void ConvertCoreInplace(in ComponentValues values) => throw new NotImplementedException();
        }
    }
}
