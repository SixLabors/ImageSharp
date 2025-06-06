// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

[DebuggerDisplay("Mode: {Mode}, Len: {Len}, BgraOrDistance: {BgraOrDistance}")]
internal readonly struct PixOrCopy
{
    public readonly PixOrCopyMode Mode;
    public readonly ushort Len;
    public readonly uint BgraOrDistance;

    private PixOrCopy(PixOrCopyMode mode, ushort len, uint bgraOrDistance)
    {
        this.Mode = mode;
        this.Len = len;
        this.BgraOrDistance = bgraOrDistance;
    }

    public static PixOrCopy CreateCacheIdx(int idx) => new(PixOrCopyMode.CacheIdx, 1, (uint)idx);

    public static PixOrCopy CreateLiteral(uint bgra) => new(PixOrCopyMode.Literal, 1, bgra);

    public static PixOrCopy CreateCopy(uint distance, ushort len) => new(PixOrCopyMode.Copy, len, distance);

    public int Literal(int component) => (int)(this.BgraOrDistance >> (component * 8)) & 0xFF;

    public uint CacheIdx() => this.BgraOrDistance;

    public ushort Length() => this.Len;

    public uint Distance() => this.BgraOrDistance;

    public bool IsLiteral() => this.Mode == PixOrCopyMode.Literal;

    public bool IsCacheIdx() => this.Mode == PixOrCopyMode.CacheIdx;

    public bool IsCopy() => this.Mode == PixOrCopyMode.Copy;
}
