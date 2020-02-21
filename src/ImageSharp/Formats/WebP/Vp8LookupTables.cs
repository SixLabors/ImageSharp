// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal static class Vp8LookupTables
    {
        private static readonly byte[] abs0;

        private static readonly byte[] clip1;

        private static readonly sbyte[] sclip1;

        private static readonly sbyte[] sclip2;

        static Vp8LookupTables()
        {
            // TODO: maybe use hashset here
            abs0 = new byte[511];
            for (int i = -255; i <= 255; ++i)
            {
                abs0[255 + i] = (byte)((i < 0) ? -i : i);
            }

            clip1 = new byte[766];
            for (int i = -255; i <= 255 + 255; ++i)
            {
                clip1[255 + i] = (byte)((i < 0) ? 0 : (i > 255) ? 255 : i);
            }

            sclip1 = new sbyte[2041];
            for (int i = -1020; i <= 1020; ++i)
            {
                sclip1[1020 + i] = (sbyte)((i < -128) ? -128 : (i > 127) ? 127 : i);
            }

            sclip2 = new sbyte[225];
            for (int i = -112; i <= 112; ++i)
            {
                sclip2[112 + i] = (sbyte)((i < -16) ? -16 : (i > 15) ? 15 : i);
            }
        }

        public static byte Abs0(int v)
        {
            return abs0[v + 255];
        }

        public static byte Clip1(int v)
        {
            return clip1[v + 255];
        }

        public static sbyte Sclip1(int v)
        {
            return sclip1[v + 1020];
        }

        public static sbyte Sclip2(int v)
        {
            return sclip2[v + 112];
        }
    }
}
