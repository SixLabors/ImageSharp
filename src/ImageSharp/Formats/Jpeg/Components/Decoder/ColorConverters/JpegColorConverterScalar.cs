// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        /// <summary>
        /// <see cref="JpegColorConverterBase"/> abstract base for implementations
        /// based on scalar instructions.
        /// </summary>
        internal abstract class JpegColorConverterScalar : JpegColorConverterBase
        {
            protected JpegColorConverterScalar(JpegColorSpace colorSpace, int precision)
                : base(colorSpace, precision)
            {
            }

            public sealed override bool IsAvailable => true;

            public sealed override int ElementsPerBatch => 1;
        }
    }
}
