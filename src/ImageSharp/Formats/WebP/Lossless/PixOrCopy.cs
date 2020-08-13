// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    [DebuggerDisplay("Mode: {Mode}, Len: {Len}, BgraOrDistance: {BgraOrDistance}")]
    internal class PixOrCopy
    {
        public PixOrCopyMode Mode { get; set; }

        public ushort Len { get; set; }

        public uint BgraOrDistance { get; set; }

        public static PixOrCopy CreateCacheIdx(int idx)
        {
            var retval = new PixOrCopy()
            {
                Mode = PixOrCopyMode.CacheIdx,
                BgraOrDistance = (uint)idx,
                Len = 1
            };

            return retval;
        }

        public static PixOrCopy CreateLiteral(uint bgra)
        {
            var retval = new PixOrCopy()
            {
                Mode = PixOrCopyMode.Literal,
                BgraOrDistance = bgra,
                Len = 1
            };

            return retval;
        }

        public static PixOrCopy CreateCopy(uint distance, ushort len)
        {
            var retval = new PixOrCopy()
            {
                Mode = PixOrCopyMode.Copy,
                BgraOrDistance = distance,
                Len = len
            };

            return retval;
        }

        public uint Literal(int component)
        {
            return (this.BgraOrDistance >> (component * 8)) & 0xff;
        }

        public uint CacheIdx()
        {
            return this.BgraOrDistance;
        }

        public ushort Length()
        {
            return this.Len;
        }

        public uint Distance()
        {
            return this.BgraOrDistance;
        }

        public bool IsLiteral()
        {
            return this.Mode == PixOrCopyMode.Literal;
        }

        public bool IsCacheIdx()
        {
            return this.Mode == PixOrCopyMode.CacheIdx;
        }

        public bool IsCopy()
        {
            return this.Mode == PixOrCopyMode.Copy;
        }
    }
}
