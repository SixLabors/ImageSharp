// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8TopSamples
    {
        public byte[] Y { get; } = new byte[16];

        public byte[] U { get; } = new byte[8];

        public byte[] V { get; } = new byte[8];
    }
}
