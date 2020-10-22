// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8MacroBlockInfo
    {
        public Vp8MacroBlockType MacroBlockType { get; set; }

        public int UvMode { get; set; }

        public bool Skip { get; set; }

        public int Segment { get; set; }

        public int Alpha { get; set; }
    }
}
