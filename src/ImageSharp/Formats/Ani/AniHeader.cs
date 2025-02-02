// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Ani;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
internal readonly struct AniHeader
{
    public uint Size { get; }

    public uint Frames { get; }

    public uint Steps { get; }

    public uint Width { get; }

    public uint Height { get; }

    public uint BitCount { get; }

    public uint Planes { get; }

    public uint DisplayRate { get; }

    public AniHeaderFlags Flags { get; }

    public static AniHeader Parse(in ReadOnlySpan<byte> data)
    => MemoryMarshal.Cast<byte, AniHeader>(data)[0];

    public readonly unsafe void WriteTo(in Stream stream)
    => stream.Write(MemoryMarshal.Cast<AniHeader, byte>([this]));
}

[Flags]
public enum AniHeaderFlags : uint
{
    IsIcon = 1,
    ContainsSeq = 2
}
