// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.WebP
{
    class SimpleLossyDecoder : WebPDecoderCoreBase
    {
        public override Image<TPixel> Decode<TPixel>(Stream stream)
            => throw new NotImplementedException();
    }
}
