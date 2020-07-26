// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    [DebuggerDisplay("Start: {Start}, End: {End}, Cost: {Cost}")]
    internal class CostInterval
    {
        public float Cost { get; set; }

        public int Start { get; set; }

        public int End { get; set; }

        public int Index { get; set; }
    }
}
