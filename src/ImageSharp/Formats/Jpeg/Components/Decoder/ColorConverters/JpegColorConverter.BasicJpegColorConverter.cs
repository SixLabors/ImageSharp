// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class BasicJpegColorConverter : JpegColorConverter
        {
            protected BasicJpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            protected override bool IsAvailable => true;
        }
    }
}
