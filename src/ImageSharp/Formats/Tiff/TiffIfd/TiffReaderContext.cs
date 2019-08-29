// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal class TiffReaderContext
    {
        public List<(TiffTagId, uint)> ExtOffsets { get; } = new List<(TiffTagId, uint)>();
    }
}
