// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromGrayscaleAvx : JpegColorConverterAvx
        {
            public FromGrayscaleAvx(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
            }

            public override void ConvertFromRgbInplace(in ComponentValues values)
            {
            }
        }
    }
}
#endif
