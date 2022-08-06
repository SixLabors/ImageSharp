// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    [DebuggerDisplay("Type: {MacroBlockType}, Alpha: {Alpha}, UvMode: {UvMode}")]
    internal class Vp8MacroBlockInfo
    {
        public Vp8MacroBlockType MacroBlockType { get; set; }

        public int UvMode { get; set; }

        public bool Skip { get; set; }

        public int Segment { get; set; }

        public int Alpha { get; set; }
    }
}
