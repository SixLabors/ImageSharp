// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class Vector8JpegColorConverter : VectorizedJpegColorConverter
        {
            protected Vector8JpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision, 8)
            {
            }

            protected sealed override bool IsAvailable => SimdUtils.HasVector8;
        }
    }
}
