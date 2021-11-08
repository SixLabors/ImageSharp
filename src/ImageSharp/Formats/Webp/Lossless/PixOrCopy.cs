// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    [DebuggerDisplay("Mode: {Mode}, Len: {Len}, BgraOrDistance: {BgraOrDistance}")]
    internal class PixOrCopy
    {
        public PixOrCopyMode Mode { get; set; }

        public ushort Len { get; set; }

        public uint BgraOrDistance { get; set; }

        public static PixOrCopy CreateCacheIdx(int idx) =>
            new()
            {
                Mode = PixOrCopyMode.CacheIdx,
                BgraOrDistance = (uint)idx,
                Len = 1
            };

        public static PixOrCopy CreateLiteral(uint bgra) =>
            new()
            {
                Mode = PixOrCopyMode.Literal,
                BgraOrDistance = bgra,
                Len = 1
            };

        public static PixOrCopy CreateCopy(uint distance, ushort len) => new()
        {
            Mode = PixOrCopyMode.Copy,
            BgraOrDistance = distance,
            Len = len
        };

        public uint Literal(int component) => (this.BgraOrDistance >> (component * 8)) & 0xff;

        public uint CacheIdx() => this.BgraOrDistance;

        public ushort Length() => this.Len;

        public uint Distance() => this.BgraOrDistance;

        public bool IsLiteral() => this.Mode == PixOrCopyMode.Literal;

        public bool IsCacheIdx() => this.Mode == PixOrCopyMode.CacheIdx;

        public bool IsCopy() => this.Mode == PixOrCopyMode.Copy;
    }
}
