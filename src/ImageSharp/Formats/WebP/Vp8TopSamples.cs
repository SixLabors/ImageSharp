// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class Vp8TopSamples
    {
        public byte[] Y { get; } = new byte[16];

        public byte[] U { get; } = new byte[8];

        public byte[] V { get; } = new byte[8];
    }
}
