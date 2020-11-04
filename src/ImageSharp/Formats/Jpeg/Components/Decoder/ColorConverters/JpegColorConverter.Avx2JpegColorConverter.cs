// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class Avx2JpegColorConverter : VectorizedJpegColorConverter
        {
            protected Avx2JpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision, 8)
            {
            }

            protected sealed override bool IsAvailable => SimdUtils.HasAvx2;
        }
    }
}
