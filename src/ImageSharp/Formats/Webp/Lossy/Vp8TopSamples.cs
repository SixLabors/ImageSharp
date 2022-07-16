// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8TopSamples
    {
        public byte[] Y { get; } = new byte[16];

        public byte[] U { get; } = new byte[8];

        public byte[] V { get; } = new byte[8];
    }
}
