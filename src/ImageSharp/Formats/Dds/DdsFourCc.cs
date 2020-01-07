// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary>
    /// Four character codes constants used in DDS files.
    /// </summary>
    internal static class DdsFourCC
    {
        public const uint DdsMagicWord = 'D' | 'D' << 8 | 'S' << 16 | ' ' << 24;

        public const uint None = 0;

        public const uint DX10 = 'D' | 'X' << 8 | '1' << 16 | '0' << 24;

        public const uint UYVY = 'U' | 'Y' << 8 | 'V' << 16 | 'Y' << 24;

        public const uint RGBG = 'R' | 'G' << 8 | 'B' << 16 | 'G' << 24;

        public const uint YUY2 = 'Y' | 'U' << 8 | 'Y' << 16 | '2' << 24;

        public const uint GRGB = 'G' | 'R' << 8 | 'G' << 16 | 'B' << 24;

        public const uint DXT1 = 'D' | 'X' << 8 | 'T' << 16 | '1' << 24;

        public const uint DXT2 = 'D' | 'X' << 8 | 'T' << 16 | '2' << 24;

        public const uint DXT3 = 'D' | 'X' << 8 | 'T' << 16 | '3' << 24;

        public const uint DXT4 = 'D' | 'X' << 8 | 'T' << 16 | '4' << 24;

        public const uint DXT5 = 'D' | 'X' << 8 | 'T' << 16 | '5' << 24;

        public const uint MET1 = 'M' | 'E' << 8 | 'T' << 16 | '1' << 24;

        public const uint BC4U = 'B' | 'C' << 8 | '4' << 16 | 'U' << 24;

        public const uint BC4S = 'B' | 'C' << 8 | '4' << 16 | 'S' << 24;

        public const uint BC5U = 'B' | 'C' << 8 | '5' << 16 | 'U' << 24;

        public const uint BC5S = 'B' | 'C' << 8 | '5' << 16 | 'S' << 24;

        public const uint ATI1 = 'A' | 'T' << 8 | 'I' << 16 | '1' << 24;

        public const uint ATI2 = 'A' | 'T' << 8 | 'I' << 16 | '2' << 24;
    }
}
