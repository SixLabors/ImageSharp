// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    internal sealed class BmpStandardBitmask
    {
        public const uint RGB555RedMask = 0x0000F800;    /* 0000 0000 0000 0000 1111 1000 0000 0000 */
        public const uint RGB555GreenMask = 0x000007E0;  /* 0000 0000 0000 0000 0000 0111 1110 0000 */
        public const uint RGB555BlueMask = 0x0000001F;   /* 0000 0000 0000 0000 0000 0000 0001 1111 */
        public const uint RGB5551AlphaMask = 0x00008000; /* 0000 0000 0000 0000 1000 0000 0000 0000 */
    }
}
