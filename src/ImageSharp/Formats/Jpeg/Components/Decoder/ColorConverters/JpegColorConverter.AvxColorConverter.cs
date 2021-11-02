// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        /// <summary>
        /// <see cref="JpegColorConverter"/> abstract base for <see cref="Avx"/>
        /// implementations.
        /// </summary>
        internal abstract class AvxColorConverter : JpegColorConverter
        {
            protected AvxColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            protected override bool IsAvailable => Avx.IsSupported;
        }
    }
}
