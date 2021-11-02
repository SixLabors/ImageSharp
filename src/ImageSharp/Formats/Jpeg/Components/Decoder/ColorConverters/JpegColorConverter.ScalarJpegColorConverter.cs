// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        /// <summary>
        /// <see cref="JpegColorConverter"/> abstract base for pure scalar implementations.
        /// </summary>
        internal abstract class ScalarJpegColorConverter : JpegColorConverter
        {
            protected ScalarJpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            protected override bool IsAvailable => true;
        }
    }
}
