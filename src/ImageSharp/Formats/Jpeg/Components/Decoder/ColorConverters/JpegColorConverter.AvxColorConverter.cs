// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        /// <summary>
        /// <see cref="JpegColorConverter"/> abstract base for implementations
        /// based on <see cref="Avx"/> instructions.
        /// </summary>
        /// <remarks>
        /// Converters of this family would expect input buffers lengths to be
        /// divisible by 8 without a remainder.
        /// This is guaranteed by real-life data as jpeg stores pixels via 8x8 blocks.
        /// DO NOT pass test data of invalid size to these converters as they
        /// potentially won't do a bound check and return a false positive result.
        /// </remarks>
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
