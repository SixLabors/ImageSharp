// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegComponentPostProcessor : IDisposable
    {
        private readonly JpegComponent component;

        private readonly Block8x8F dequantTable;

        public JpegComponentPostProcessor(JpegComponent component, Block8x8F dequantTable)
        {
            this.component = component;
            this.dequantTable = dequantTable;
        }

        public void Dispose()
        {
        }
    }
}
