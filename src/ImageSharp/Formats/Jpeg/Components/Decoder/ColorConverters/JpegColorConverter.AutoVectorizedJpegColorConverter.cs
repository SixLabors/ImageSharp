// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal abstract class AutoVectorizedJpegColorConverter : VectorizedJpegColorConverter
        {
            protected AutoVectorizedJpegColorConverter(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision, Vector<float>.Count)
            {
            }

            protected sealed override bool IsAvailable => Vector.IsHardwareAccelerated;
        }
    }
}
