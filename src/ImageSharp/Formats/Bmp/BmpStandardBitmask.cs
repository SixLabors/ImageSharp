// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    internal sealed class BmpStandardBitmask
    {
        // R5G5B5A0X1
        public const uint RGB555RedMask = 0x00007C00;    /* 0000 0000 0000 0000 0111 1100 0000 0000 */
        public const uint RGB555GreenMask = 0x000003E0;  /* 0000 0000 0000 0000 0000 0011 1110 0000 */
        public const uint RGB555BlueMask = 0x0000001F;   /* 0000 0000 0000 0000 0000 0000 0001 1111 */
        public const uint RGB555AlphaMask = 0x00000000;  /* 0000 0000 0000 0000 0000 0000 0000 0000 */

        // R5G5B5A1X0
        public const uint RGB5551RedMask = 0x00007C00;   /* 0000 0000 0000 0000 0111 1100 0000 0000 */
        public const uint RGB5551GreenMask = 0x000003E0; /* 0000 0000 0000 0000 0000 0011 1110 0000 */
        public const uint RGB5551BlueMask = 0x0000001F;  /* 0000 0000 0000 0000 0000 0000 0001 1111 */
        public const uint RGB5551AlphaMask = 0x00008000; /* 0000 0000 0000 0000 1000 0000 0000 0000 */

        // R5G6B5A0X0
        public const uint RGB565RedMask = 0x0000F800;    /* 0000 0000 0000 0000 1111 1000 0000 0000 */
        public const uint RGB565GreenMask = 0x000007E0;  /* 0000 0000 0000 0000 0000 0111 1110 0000 */
        public const uint RGB565BlueMask = 0x0000001F;   /* 0000 0000 0000 0000 0000 0000 0001 1111 */
        public const uint RGB565AlphaMask = 0x00000000;  /* 0000 0000 0000 0000 0000 0000 0000 0000 */

        // R8G8B8A0X8
        public const uint RGB888RedMask = 0x00FF0000;    /* 0000 0000 1111 1111 0000 0000 0000 0000 */
        public const uint RGB888GreenMask = 0x0000FF00;  /* 0000 0000 0000 0000 1111 1111 0000 0000 */
        public const uint RGB888BlueMask = 0x000000FF;   /* 0000 0000 0000 0000 0000 0000 1111 1111 */
        public const uint RGB888AlphaMask = 0x00000000;  /* 0000 0000 0000 0000 0000 0000 0000 0000 */

        // R8G8B8A8X0
        public const uint RGB8888RedMask = 0x00FF0000;   /* 0000 0000 1111 1111 0000 0000 0000 0000 */
        public const uint RGB8888GreenMask = 0x0000FF00; /* 0000 0000 0000 0000 1111 1111 0000 0000 */
        public const uint RGB8888BlueMask = 0x000000FF;  /* 0000 0000 0000 0000 0000 0000 1111 1111 */
        public const uint RGB8888AlphaMask = 0xFF000000; /* 1111 1111 0000 0000 0000 0000 0000 0000 */

        // R10G10B10A0X2
        public const uint RGB101010RedMask = 0x3FF00000;   /* 0011 1111 1111 0000 0000 0000 0000 0000 */
        public const uint RGB101010GreenMask = 0x000FFC00; /* 0000 0000 0000 1111 1111 1100 0000 0000 */
        public const uint RGB101010BlueMask = 0x000003FF;  /* 0000 0000 0000 0000 0000 0011 1111 1111 */
        public const uint RGB101010AlphaMask = 0x00000000; /* 0000 0000 0000 0000 0000 0000 0000 0000 */

        // R10G10B10A2X0
        public const uint RGB1010102RedMask = 0x3FF00000;   /* 0011 1111 1111 0000 0000 0000 0000 0000 */
        public const uint RGB1010102GreenMask = 0x000FFC00; /* 0000 0000 0000 1111 1111 1100 0000 0000 */
        public const uint RGB1010102BlueMask = 0x000003FF;  /* 0000 0000 0000 0000 0000 0011 1111 1111 */
        public const uint RGB1010102AlphaMask = 0xC0000000; /* 1100 0000 0000 0000 0000 0000 0000 0000 */
    }
}
